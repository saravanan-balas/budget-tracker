using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Models;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text;

namespace BudgetTracker.Worker;

public class TestPdfReader
{
    private readonly ILogger<TestPdfReader> _logger;
    
    // Simple category mapping for testing
    private readonly Dictionary<string, string> _categoryMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Food & Dining
        { "uber eats", "Food & Dining" },
        { "doordash", "Food & Dining" },
        { "grubhub", "Food & Dining" },
        { "restaurant", "Food & Dining" },
        { "starbucks", "Food & Dining" },
        { "mcdonald", "Food & Dining" },
        { "pizza", "Food & Dining" },
        { "coffee", "Food & Dining" },
        
        // Transportation
        { "uber", "Transportation" },
        { "lyft", "Transportation" },
        { "gas station", "Transportation" },
        { "shell", "Transportation" },
        { "chevron", "Transportation" },
        { "parking", "Transportation" },
        
        // Entertainment & Subscriptions
        { "netflix", "Entertainment" },
        { "spotify", "Entertainment" },
        { "hulu", "Entertainment" },
        { "disney", "Entertainment" },
        { "apple music", "Entertainment" },
        { "amazon prime", "Entertainment" },
        { "youtube", "Entertainment" },
        { "cinemark", "Entertainment" },
        { "amc", "Entertainment" },
        { "theater", "Entertainment" },
        { "cinema", "Entertainment" },
        
        // Shopping
        { "amazon", "Shopping" },
        { "walmart", "Shopping" },
        { "target", "Shopping" },
        { "best buy", "Shopping" },
        { "costco", "Shopping" },
        
        // Groceries
        { "whole foods", "Groceries" },
        { "trader joe", "Groceries" },
        { "safeway", "Groceries" },
        { "kroger", "Groceries" },
        { "giant", "Groceries" },
        { "liquor", "Groceries" },
        { "market", "Groceries" },
        
        // Utilities & Services
        { "comcast", "Utilities" },
        { "verizon", "Phone & Internet" },
        { "at&t", "Phone & Internet" },
        { "t-mobile", "Phone & Internet" },
        { "fi", "Phone & Internet" },
        { "google fi", "Phone & Internet" },
        { "electric", "Utilities" },
        { "water", "Utilities" },
        { "gas company", "Utilities" },
        
        // Healthcare
        { "pharmacy", "Healthcare" },
        { "cvs", "Healthcare" },
        { "walgreens", "Healthcare" },
        { "doctor", "Healthcare" },
        { "hospital", "Healthcare" },
        { "medical", "Healthcare" },
        
        // Finance
        { "atm", "Banking" },
        { "bank", "Banking" },
        { "interest", "Banking" },
        { "fee", "Banking" },
        { "transfer", "Transfer" }
    };

    public TestPdfReader(ILogger<TestPdfReader> logger)
    {
        _logger = logger;
    }

    public async Task TestReadPdf(string pdfFilePath)
    {
        try
        {
            _logger.LogInformation("Reading PDF file: {FilePath}", pdfFilePath);
            
            // Read the PDF file
            var fileBytes = await File.ReadAllBytesAsync(pdfFilePath);
            _logger.LogInformation("PDF file size: {Size} bytes", fileBytes.Length);
            
            // Extract text from PDF
            var extractedText = ExtractTextFromPdf(fileBytes);
            
            _logger.LogInformation("=== EXTRACTED PDF TEXT ===");
            Console.WriteLine(extractedText);
            _logger.LogInformation("=== END OF PDF TEXT ===");
            
            // Parse transactions from the text
            var transactions = ParseTransactionsFromText(extractedText);
            
            // Categorize transactions
            CategorizeTransactions(transactions);
            
            _logger.LogInformation("\n=== PARSED TRANSACTIONS WITH CATEGORIES ===");
            _logger.LogInformation("Found {Count} transactions", transactions.Count);
            
            // Group by category for summary
            var categorySummary = transactions
                .GroupBy(t => t.Category ?? "Uncategorized")
                .Select(g => new { Category = g.Key, Count = g.Count(), Total = g.Sum(t => Math.Abs(t.Amount)) })
                .OrderByDescending(c => c.Total);
            
            foreach (var txn in transactions)
            {
                Console.WriteLine($"Date: {txn.Date:yyyy-MM-dd}");
                Console.WriteLine($"Description: {txn.Description}");
                Console.WriteLine($"Amount: ${txn.Amount:F2}");
                Console.WriteLine($"Category: {txn.Category ?? "Uncategorized"}");
                Console.WriteLine("---");
            }
            
            _logger.LogInformation("\n=== CATEGORY SUMMARY ===");
            Console.WriteLine("\n=== SPENDING BY CATEGORY ===");
            foreach (var cat in categorySummary)
            {
                Console.WriteLine($"{cat.Category}: ${cat.Total:F2} ({cat.Count} transactions)");
            }
            Console.WriteLine($"TOTAL: ${categorySummary.Sum(c => c.Total):F2}");
            
            _logger.LogInformation("=== END OF TRANSACTIONS ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading PDF file");
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
    
    private string ExtractTextFromPdf(byte[] pdfData)
    {
        var text = new StringBuilder();
        
        try
        {
            using (var reader = new PdfReader(pdfData))
            {
                _logger.LogInformation("PDF has {PageCount} pages", reader.NumberOfPages);
                
                for (int page = 1; page <= reader.NumberOfPages; page++)
                {
                    _logger.LogInformation("Extracting text from page {Page}", page);
                    var pageText = PdfTextExtractor.GetTextFromPage(reader, page);
                    text.AppendLine(pageText);
                    _logger.LogInformation("Page {Page} text length: {Length} characters", page, pageText.Length);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF");
            throw;
        }
        
        return text.ToString();
    }
    
    private List<ParsedTransaction> ParseTransactionsFromText(string text)
    {
        var transactions = new List<ParsedTransaction>();
        
        // Simple pattern matching for transactions
        // Look for patterns like: MM/DD Description Amount
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            try
            {
                // Skip header/footer lines
                if (line.Contains("BANK OF AMERICA") || 
                    line.Contains("Statement") || 
                    line.Contains("Page") ||
                    line.Contains("Account") ||
                    line.Contains("Previous Balance") ||
                    line.Contains("Total") ||
                    line.Length < 10)
                {
                    continue;
                }
                
                // Try to parse date at the beginning of the line (MM/DD format)
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d{2}/\d{2}"))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length >= 2)
                    {
                        // Parse date
                        var dateStr = parts[0];
                        var dateParts = dateStr.Split('/');
                        if (dateParts.Length == 2)
                        {
                            var month = int.Parse(dateParts[0]);
                            var day = int.Parse(dateParts[1]);
                            var year = DateTime.Now.Year; // Use current year as default
                            var transactionDate = new DateTime(year, month, day);
                            
                            // Find amount (last numeric value in the line)
                            decimal? amount = null;
                            string amountStr = "";
                            
                            for (int i = parts.Length - 1; i >= 1; i--)
                            {
                                var cleaned = parts[i].Replace("$", "").Replace(",", "").Replace("-", "");
                                if (decimal.TryParse(cleaned, out var parsedAmount))
                                {
                                    amount = parts[i].Contains("-") ? -parsedAmount : parsedAmount;
                                    amountStr = parts[i];
                                    
                                    // Description is everything between date and amount
                                    var description = string.Join(" ", parts.Skip(1).Take(i - 1));
                                    
                                    if (!string.IsNullOrWhiteSpace(description))
                                    {
                                        transactions.Add(new ParsedTransaction
                                        {
                                            Date = transactionDate,
                                            Description = description.Trim(),
                                            Amount = amount.Value
                                        });
                                        
                                        _logger.LogDebug("Parsed transaction from line: {Line}", line);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not parse line as transaction: {Line}, Error: {Error}", line, ex.Message);
            }
        }
        
        return transactions;
    }
    
    private void CategorizeTransactions(List<ParsedTransaction> transactions)
    {
        foreach (var transaction in transactions)
        {
            if (transaction.Category == null)
            {
                transaction.Category = DetermineCategory(transaction.Description);
            }
        }
    }
    
    private string DetermineCategory(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "Uncategorized";
        
        var lowerDesc = description.ToLower();
        
        // Check each category mapping
        foreach (var mapping in _categoryMappings)
        {
            if (lowerDesc.Contains(mapping.Key.ToLower()))
            {
                return mapping.Value;
            }
        }
        
        // Default categorization based on common patterns
        if (lowerDesc.Contains("payment") || lowerDesc.Contains("transfer"))
            return "Transfer";
        
        if (lowerDesc.Contains("deposit") || lowerDesc.Contains("credit"))
            return "Income";
        
        if (lowerDesc.Contains("fee") || lowerDesc.Contains("charge"))
            return "Banking";
        
        return "Uncategorized";
    }
}