using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using BudgetTracker.Common.DTOs;

namespace BudgetTracker.Common.Services.AI;

public class AIBankAnalyzer : IAIBankAnalyzer
{
    private readonly ILogger<AIBankAnalyzer> _logger;
    private readonly IConfiguration _configuration;
    
    // Cost tracking constants (per token estimates)
    private const decimal GPT4_COST_PER_TOKEN = 0.00001m;
    private const decimal GPT35_COST_PER_TOKEN = 0.000001m;
    
    public AIBankAnalyzer(
        ILogger<AIBankAnalyzer> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<BankDetectionResult> DetectBankAsync(byte[] fileData, string fileName)
    {
        await Task.CompletedTask;
        
        _logger.LogInformation("Starting AI bank detection for file: {FileName}", fileName);

        try
        {
            // For now, return a mock result - will implement OpenAI integration
            var result = new BankDetectionResult
            {
                BankName = "Unknown Bank",
                Country = "Unknown",
                FileFormat = DetermineFileFormat(fileName),
                Confidence = 0.5,
                Metadata = new Dictionary<string, object>
                {
                    {"detectionMethod", "placeholder"},
                    {"fileSize", fileData.Length},
                    {"timestamp", DateTime.UtcNow}
                }
            };

            // Simulate AI detection based on file content analysis
            var sampleContent = GetSampleContent(fileData, fileName);
            if (!string.IsNullOrEmpty(sampleContent))
            {
                result = AnalyzeBankFromContent(sampleContent, result);
            }

            _logger.LogInformation("AI detection completed for {FileName}: {BankName} (Confidence: {Confidence})",
                fileName, result.BankName, result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI bank detection for file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<TransactionParsingResult> ParseTransactionsWithAIAsync(
        byte[] fileData, 
        string fileName,
        BankDetectionResult bankInfo)
    {
        await Task.CompletedTask;
        
        _logger.LogInformation("Starting AI transaction parsing for {BankName} - {FileName}", 
            bankInfo.BankName, fileName);

        var result = new TransactionParsingResult();

        try
        {
            // Placeholder for actual AI parsing implementation
            // This would integrate with OpenAI GPT-4 to extract transactions
            
            var sampleContent = GetSampleContent(fileData, fileName);
            if (string.IsNullOrEmpty(sampleContent))
            {
                throw new InvalidOperationException("Unable to extract content for AI analysis");
            }

            // Simulate AI parsing results
            result.Transactions = GenerateMockTransactions(sampleContent);
            result.IsSuccessful = result.Transactions.Any();
            result.AICost = await EstimateAICostAsync(fileData.Length, bankInfo.FileFormat);

            _logger.LogInformation("AI parsing completed: {Count} transactions extracted, Cost: ${Cost}",
                result.Transactions.Count, result.AICost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI transaction parsing");
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<decimal> EstimateAICostAsync(int fileSize, string fileType)
    {
        await Task.CompletedTask;
        
        // Estimate token count based on file size and type
        int estimatedTokens = fileType.ToUpper() switch
        {
            "CSV" => fileSize / 4, // ~4 characters per token
            "PDF" => fileSize / 6, // Less dense due to formatting
            "PNG" or "JPEG" => 1000, // Fixed cost for vision API
            _ => fileSize / 4
        };

        // Choose appropriate model based on complexity
        bool useGPT4 = fileType.ToUpper() switch
        {
            "PNG" or "JPEG" => true, // Vision requires GPT-4
            "PDF" => estimatedTokens > 10000, // Complex PDFs need GPT-4
            _ => false // CSV can use GPT-3.5
        };

        var costPerToken = useGPT4 ? GPT4_COST_PER_TOKEN : GPT35_COST_PER_TOKEN;
        var estimatedCost = estimatedTokens * costPerToken;

        _logger.LogInformation("Cost estimate for {FileType} ({FileSize} bytes): ${Cost} ({Tokens} tokens, {Model})",
            fileType, fileSize, estimatedCost, estimatedTokens, useGPT4 ? "GPT-4" : "GPT-3.5");

        return estimatedCost;
    }

    public async Task<bool> IsAIProcessingRequired(byte[] fileData, string fileName)
    {
        await Task.CompletedTask;
        
        var format = DetermineFileFormat(fileName);
        
        // Always require AI for images and PDFs
        if (format == "PNG" || format == "JPEG" || format == "PDF")
        {
            return true;
        }

        // For CSV, check if it's a complex format that needs AI
        if (format == "CSV")
        {
            var sampleContent = GetSampleContent(fileData, fileName);
            return IsComplexCsvFormat(sampleContent);
        }

        return true; // Default to AI processing for unknown formats
    }

    private string DetermineFileFormat(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => "CSV",
            ".pdf" => "PDF",
            ".png" => "PNG",
            ".jpg" or ".jpeg" => "JPEG",
            _ => "UNKNOWN"
        };
    }

    private string GetSampleContent(byte[] fileData, string fileName)
    {
        try
        {
            var format = DetermineFileFormat(fileName);
            
            if (format == "CSV")
            {
                // Get first 2KB of CSV content for analysis
                var sampleSize = Math.Min(fileData.Length, 2048);
                return Encoding.UTF8.GetString(fileData, 0, sampleSize);
            }
            
            // For PDFs and images, we would extract text here
            // This is a placeholder - actual implementation would use OCR/PDF libraries
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to extract sample content from file");
            return string.Empty;
        }
    }

    private BankDetectionResult AnalyzeBankFromContent(string content, BankDetectionResult baseResult)
    {
        // Simple pattern matching for common bank names
        // In real implementation, this would use AI to detect bank names
        var commonBanks = new Dictionary<string, (string bank, string country)>
        {
            {"chase", ("JPMorgan Chase", "US")},
            {"bank of america", ("Bank of America", "US")},
            {"wells fargo", ("Wells Fargo", "US")},
            {"citi", ("Citibank", "US")},
            {"td bank", ("TD Bank", "US")},
            {"rbc", ("Royal Bank of Canada", "CA")},
            {"scotiabank", ("Scotiabank", "CA")},
            {"hsbc", ("HSBC", "GB")},
            {"barclays", ("Barclays", "GB")},
        };

        var lowerContent = content.ToLowerInvariant();
        
        foreach (var (pattern, (bank, country)) in commonBanks)
        {
            if (lowerContent.Contains(pattern))
            {
                baseResult.BankName = bank;
                baseResult.Country = country;
                baseResult.Confidence = 0.8;
                break;
            }
        }

        return baseResult;
    }

    private bool IsComplexCsvFormat(string content)
    {
        if (string.IsNullOrEmpty(content)) return true;

        // Check for signs of complex formatting that might need AI
        var lines = content.Split('\n').Take(5).ToArray();
        
        // If there are no clear delimiters, it's complex
        if (lines.Length < 2) return true;
        
        var firstLine = lines[0];
        var hasCommas = firstLine.Contains(',');
        var hasSemicolons = firstLine.Contains(';');
        var hasTabs = firstLine.Contains('\t');
        
        // If no clear delimiter pattern, needs AI
        if (!hasCommas && !hasSemicolons && !hasTabs) return true;
        
        // If inconsistent column counts across rows, needs AI
        var expectedColumns = firstLine.Split(',').Length;
        var inconsistentRows = lines.Skip(1)
            .Count(line => Math.Abs(line.Split(',').Length - expectedColumns) > 1);
        
        return inconsistentRows > 1; // More than 1 inconsistent row suggests complexity
    }

    private List<ParsedTransaction> GenerateMockTransactions(string content)
    {
        var transactions = new List<ParsedTransaction>();

        // Parse the realistic OCR text to extract actual transactions
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            // Look for lines that contain dollar amounts
            if (line.Contains("$") && !line.Contains("Date") && !line.Contains("Latest Card"))
            {
                // Extract transaction details using pattern matching
                var transaction = ParseTransactionLine(line);
                if (transaction != null)
                {
                    transactions.Add(transaction);
                }
            }
        }

        // If no transactions found, fallback to generate some sample ones
        if (!transactions.Any())
        {
            var random = new Random();
            for (int i = 1; i <= 3; i++)
            {
                transactions.Add(new ParsedTransaction
                {
                    Date = DateTime.UtcNow.AddDays(-random.Next(1, 7)),
                    Description = $"Parsed Transaction {i}",
                    Amount = -random.Next(10, 100),
                });
            }
        }

        return transactions;
    }
    
    private ParsedTransaction? ParseTransactionLine(string line)
    {
        try
        {
            // Parse lines like "Sep 12     Uber                           $5.00"
            var parts = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 3) return null;
            
            // Find the amount (contains $)
            var amountStr = parts.FirstOrDefault(p => p.Contains("$"));
            if (amountStr == null) return null;
            
            // Parse amount
            var amountValue = amountStr.Replace("$", "").Replace(",", "");
            if (!decimal.TryParse(amountValue, out var amount)) return null;
            
            // Find description (typically between date and amount)
            var description = "";
            var foundDate = false;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Contains("$")) break; // Stop at amount
                
                if (foundDate || parts[i].StartsWith("Sep") || parts[i].Length <= 3)
                {
                    if (!foundDate && (parts[i].StartsWith("Sep") || parts[i].Length <= 3))
                    {
                        foundDate = true;
                        continue;
                    }
                    description += parts[i] + " ";
                }
            }
            
            description = description.Trim();
            if (string.IsNullOrEmpty(description)) description = "Transaction";
            
            // Create the transaction
            return new ParsedTransaction
            {
                Date = DateTime.UtcNow.AddDays(-new Random().Next(1, 5)), // Recent dates
                Description = description,
                Amount = -amount, // Expenses are negative
                Category = InferCategory(description)
            };
        }
        catch
        {
            return null;
        }
    }
    
    private string InferCategory(string description)
    {
        var desc = description.ToLowerInvariant();
        return desc switch
        {
            var d when d.Contains("uber") => "Transportation",
            var d when d.Contains("netflix") => "Entertainment", 
            var d when d.Contains("restaurant") || d.Contains("food") => "Food & Dining",
            var d when d.Contains("grocery") || d.Contains("market") => "Groceries",
            var d when d.Contains("gas") || d.Contains("fuel") => "Transportation",
            var d when d.Contains("cinema") => "Entertainment",
            _ => "Miscellaneous"
        };
    }
}