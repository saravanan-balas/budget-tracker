using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BudgetTracker.Common.Services.Embeddings;

public class OpenAIEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIEmbeddingService> _logger;
    private readonly string _apiKey;
    private const string EmbeddingModel = "text-embedding-3-small"; // More cost-effective than text-embedding-ada-002
    private const int EmbeddingDimensions = 1536;

    public OpenAIEmbeddingService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAIEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["OPENAI_API_KEY"] ?? throw new ArgumentException("OpenAI API key not configured");
        
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "BudgetTracker/1.0");
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text)
    {
        var embeddings = await GenerateEmbeddingsAsync(new[] { text });
        return embeddings[0];
    }

    public async Task<Vector[]> GenerateEmbeddingsAsync(string[] texts)
    {
        if (texts == null || texts.Length == 0)
            throw new ArgumentException("Texts cannot be null or empty");

        try
        {
            // Normalize texts
            var normalizedTexts = texts.Select(NormalizeMerchantName).ToArray();
            
            var request = new
            {
                input = normalizedTexts,
                model = EmbeddingModel,
                dimensions = EmbeddingDimensions
            };

            var jsonRequest = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            _logger.LogDebug("Generating embeddings for {Count} texts", texts.Length);
            
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/embeddings", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                throw new HttpRequestException($"OpenAI API request failed: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var embeddingResponse = JsonSerializer.Deserialize<OpenAIEmbeddingResponse>(responseContent);

            if (embeddingResponse?.Data == null || embeddingResponse.Data.Length == 0)
            {
                throw new InvalidOperationException("No embeddings returned from OpenAI API");
            }

            var vectors = embeddingResponse.Data
                .OrderBy(d => d.Index)
                .Select(d => new Vector(d.Embedding))
                .ToArray();

            _logger.LogDebug("Generated {Count} embeddings successfully", vectors.Length);
            
            // Log usage for cost tracking
            if (embeddingResponse.Usage != null)
            {
                _logger.LogInformation("OpenAI Embedding Usage: {Tokens} tokens, estimated cost: ${Cost:F4}", 
                    embeddingResponse.Usage.TotalTokens, 
                    CalculateEmbeddingCost(embeddingResponse.Usage.TotalTokens));
            }

            return vectors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embeddings for texts: {Texts}", string.Join(", ", texts));
            throw;
        }
    }

    public double CalculateSimilarity(Vector vector1, Vector vector2)
    {
        if (vector1 == null || vector2 == null)
            return 0.0;

        // Calculate cosine similarity manually
        // Cosine similarity = (A · B) / (||A|| * ||B||)
        var dotProduct = 0.0;
        var magnitudeA = 0.0;
        var magnitudeB = 0.0;

        var arrayA = vector1.ToArray();
        var arrayB = vector2.ToArray();

        if (arrayA.Length != arrayB.Length)
            return 0.0;

        for (int i = 0; i < arrayA.Length; i++)
        {
            dotProduct += arrayA[i] * arrayB[i];
            magnitudeA += arrayA[i] * arrayA[i];
            magnitudeB += arrayB[i] * arrayB[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0.0 || magnitudeB == 0.0)
            return 0.0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    public string NormalizeMerchantName(string merchantName)
    {
        if (string.IsNullOrWhiteSpace(merchantName))
            return string.Empty;

        var normalized = merchantName.Trim().ToUpperInvariant();

        // Remove common prefixes/suffixes that don't help with identification
        normalized = Regex.Replace(normalized, @"^(PURCHASE\s+|POS\s+|DEBIT\s+|CREDIT\s+|ATM\s+)", "", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+(INC|LLC|CORP|CO|LTD|LIMITED)$", "", RegexOptions.IgnoreCase);
        
        // Remove transaction IDs, numbers, and dates
        normalized = Regex.Replace(normalized, @"\b\d{4,}\b", ""); // Remove 4+ digit numbers
        normalized = Regex.Replace(normalized, @"\b\d{1,2}\/\d{1,2}(?:\/\d{2,4})?\b", ""); // Remove dates
        normalized = Regex.Replace(normalized, @"#\d+", ""); // Remove transaction IDs like #1234
        
        // Remove location information
        normalized = Regex.Replace(normalized, @"\b[A-Z]{2}\b$", ""); // Remove state codes at end
        normalized = Regex.Replace(normalized, @"\b\d{5}(?:-\d{4})?\b", ""); // Remove ZIP codes
        
        // Clean up common payment processor prefixes
        normalized = Regex.Replace(normalized, @"^(SQ\s*\*|SP\s*\*|PP\s*\*|PAYPAL\s*\*)", "", RegexOptions.IgnoreCase);
        
        // Remove extra whitespace
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        
        // Ensure we have something meaningful left
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 2)
        {
            // Fall back to original if normalization removed too much
            normalized = merchantName.Trim();
        }

        _logger.LogDebug("Normalized '{Original}' → '{Normalized}'", merchantName, normalized);
        
        return normalized;
    }

    private static decimal CalculateEmbeddingCost(int tokens)
    {
        // text-embedding-3-small pricing: $0.00002 per 1K tokens
        return (decimal)tokens / 1000 * 0.00002m;
    }

    // Response models for OpenAI API
    private class OpenAIEmbeddingResponse
    {
        public string Object { get; set; } = "";
        public EmbeddingData[] Data { get; set; } = Array.Empty<EmbeddingData>();
        public string Model { get; set; } = "";
        public Usage? Usage { get; set; }
    }

    private class EmbeddingData
    {
        public string Object { get; set; } = "";
        public int Index { get; set; }
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }

    private class Usage
    {
        public int PromptTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}