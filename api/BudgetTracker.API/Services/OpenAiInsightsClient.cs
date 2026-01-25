using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BudgetTracker.Common.DTOs;

namespace BudgetTracker.API.Services;

public class OpenAiInsightsClient : IOpenAiInsightsClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiInsightsClient> _logger;

    public OpenAiInsightsClient(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiInsightsClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AiMonthlyInsightsDto?> GenerateMonthlyInsightsAsync(
        MonthlyInsightsResponseDto computed,
        CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogInformation("OPENAI_API_KEY not configured; skipping AI insights.");
            return null;
        }

        // If there is no data, skip.
        if (computed.TransactionCount <= 0)
        {
            return new AiMonthlyInsightsDto
            {
                UsedAi = true,
                Summary = "No transactions found for the selected month, so there is nothing to analyze yet.",
                Highlights = new List<string>(),
                Suggestions = new List<string> { "Import transactions or add a few transactions to start seeing insights." },
                Watchouts = new List<string>()
            };
        }

        try
        {
            var monthLabel = computed.PeriodStartUtc.ToString("MMMM yyyy");

            var payload = new
            {
                period = new
                {
                    month = monthLabel,
                    startUtc = computed.PeriodStartUtc,
                    endUtc = computed.PeriodEndUtc
                },
                totals = new
                {
                    income = computed.TotalIncome,
                    expenses = computed.TotalExpenses,
                    net = computed.Net,
                    transactionCount = computed.TransactionCount
                },
                spendingByCategory = computed.SpendingByCategory
                    .Select(c => new { category = c.Label, amount = c.Value })
                    .ToList(),
                topMerchants = computed.TopMerchants
                    .Select(m => new { merchant = m.Merchant, amount = m.Amount, count = m.Count })
                    .ToList(),
                sampleTransactions = computed.SampleTransactions
                    .Select(t => new
                    {
                        dateUtc = t.TransactionDateUtc,
                        merchant = t.Merchant,
                        amount = t.Amount,
                        category = t.Category
                    })
                    .ToList()
            };

            var system =
                "You are a personal finance analyst. You will be given monthly transaction aggregates.\n" +
                "Return ONLY valid JSON with keys: summary (string), highlights (string[]), suggestions (string[]), watchouts (string[]).\n" +
                "Constraints:\n" +
                "- summary: 1 short paragraph (max 3 sentences)\n" +
                "- highlights: 3-5 bullets\n" +
                "- suggestions: 3-5 actionable tips\n" +
                "- watchouts: 0-5 items pointing to potential issues/anomalies\n" +
                "- Be concise. No markdown. No extra keys.\n";

            var user =
                "Analyze this monthly spending and identify where the user is spending the most.\n" +
                "Consider category mix, top merchants, and sample transactions.\n" +
                "If net is negative, include a suggestion to reduce top categories.\n\n" +
                "Data (JSON):\n" + JsonSerializer.Serialize(payload);

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                },
                temperature = 0.2,
                max_tokens = 600
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OpenAI API error: {StatusCode} {Error}", response.StatusCode, err);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            using var jsonDoc = JsonDocument.Parse(responseContent);

            var content = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("OpenAI returned empty content");
                return null;
            }

            var clean = CleanJson(content);
            var parsed = JsonSerializer.Deserialize<AiResponse>(clean, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Summary))
            {
                _logger.LogWarning("Failed to parse AI insights JSON");
                return null;
            }

            return new AiMonthlyInsightsDto
            {
                UsedAi = true,
                Summary = parsed.Summary.Trim(),
                Highlights = (parsed.Highlights ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
                Suggestions = (parsed.Suggestions ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
                Watchouts = (parsed.Watchouts ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generating AI monthly insights");
            return null;
        }
    }

    private static string CleanJson(string text)
    {
        var s = text.Trim();
        if (s.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(7);
        }
        if (s.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(3);
        }
        if (s.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(0, s.Length - 3);
        }
        return s.Trim();
    }

    private sealed class AiResponse
    {
        public string Summary { get; set; } = string.Empty;
        public List<string>? Highlights { get; set; }
        public List<string>? Suggestions { get; set; }
        public List<string>? Watchouts { get; set; }
    }

    private string? GetApiKey()
    {
        // Prefer env var style key, but also support appsettings style key.
        // Note: don't log this value.
        var apiKey = _configuration["OPENAI_API_KEY"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey;
        }

        apiKey = _configuration["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey;
        }

        return null;
    }
}

