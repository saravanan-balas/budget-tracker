using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text;
using BudgetTracker.Common.DTOs;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace BudgetTracker.Common.Services.Parsing;

public class UniversalBankParser : IUniversalBankParser
{
    private readonly IFormatDetectionService _formatDetection;
    private readonly ILogger<UniversalBankParser> _logger;
    private readonly IServiceProvider _serviceProvider;

    public UniversalBankParser(
        IFormatDetectionService formatDetection,
        ILogger<UniversalBankParser> logger,
        IServiceProvider serviceProvider)
    {
        _formatDetection = formatDetection;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<TransactionParsingResult> ParseFileAsync(
        byte[] fileData, 
        string fileName, 
        BankDetectionResult? bankInfo = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TransactionParsingResult();

        try
        {
            var format = await _formatDetection.DetectFormatAsync(fileData, fileName);
            _logger.LogInformation("Parsing file {FileName} with format {Format}", fileName, format);

            result = format switch
            {
                "CSV" => await ParseCsvAsync(fileData),
                "PDF" => await ParsePdfAsync(fileData),
                "PNG" or "JPEG" => await ParseImageAsync(fileData),
                _ => throw new NotSupportedException($"File format {format} is not supported")
            };

            result.IsSuccessful = true;
            _logger.LogInformation("Successfully parsed {Count} transactions from {FileName}", 
                result.Transactions.Count, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing file {FileName}", fileName);
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<ImportPreviewDto> GeneratePreviewAsync(byte[] fileData, string fileName)
    {
        var format = await _formatDetection.DetectFormatAsync(fileData, fileName);
        
        return format switch
        {
            "CSV" => await GenerateCsvPreviewAsync(fileData),
            "PDF" => await GeneratePdfPreviewAsync(fileData),
            "PNG" or "JPEG" => await GenerateImagePreviewAsync(fileData),
            _ => new ImportPreviewDto()
        };
    }

    public async Task<bool> CanProcessSynchronouslyAsync(byte[] fileData, string fileName)
    {
        var analysis = await _formatDetection.AnalyzeFileAsync(fileData, fileName);
        return analysis.CanProcessSynchronously;
    }

    private async Task<TransactionParsingResult> ParseCsvAsync(byte[] fileData)
    {
        await Task.CompletedTask;
        var result = new TransactionParsingResult();

        try
        {
            var csvContent = Encoding.UTF8.GetString(fileData);
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                throw new InvalidDataException("CSV file must contain at least header and one data row");
            }

            // Find the actual header line (look for line with "Date" column)
            int headerLineIndex = -1;
            string[] headers = null;
            
            for (int i = 0; i < lines.Length; i++)
            {
                var potentialHeaders = lines[i].Split(',').Select(h => h.Trim('"')).ToArray();
                if (potentialHeaders.Any(h => h.ToLowerInvariant().Contains("date")) && 
                    potentialHeaders.Any(h => h.ToLowerInvariant().Contains("amount") || h.ToLowerInvariant().Contains("description")))
                {
                    headerLineIndex = i;
                    headers = potentialHeaders;
                    break;
                }
            }

            if (headerLineIndex == -1 || headers == null)
            {
                throw new InvalidDataException("Could not find valid header line in CSV");
            }

            _logger.LogInformation("Found header line at index {Index}: {Headers}", headerLineIndex, string.Join(", ", headers));

            var transactions = new List<ParsedTransaction>();

            // Try to identify column indices
            var dateIndex = FindColumnIndex(headers, new[] { "date", "transaction date", "posting date" });
            var amountIndex = FindColumnIndex(headers, new[] { "amount", "transaction amount", "debit", "credit" });
            var descriptionIndex = FindColumnIndex(headers, new[] { "description", "memo", "details", "merchant" });
            var balanceIndex = FindColumnIndex(headers, new[] { "balance", "running balance", "running bal" });

            for (int i = headerLineIndex + 1; i < lines.Length; i++)
            {
                try
                {
                    var fields = ParseCsvLine(lines[i]);
                    if (fields.Length < Math.Max(dateIndex + 1, amountIndex + 1)) continue;

                    var transaction = new ParsedTransaction();

                    // Parse date
                    if (dateIndex >= 0 && DateTime.TryParse(fields[dateIndex], out var date))
                    {
                        transaction.Date = date;
                    }

                    // Parse amount
                    if (amountIndex >= 0 && decimal.TryParse(fields[amountIndex].Replace("$", "").Replace(",", ""), out var amount))
                    {
                        transaction.Amount = amount;
                    }

                    // Parse description
                    if (descriptionIndex >= 0 && descriptionIndex < fields.Length)
                    {
                        transaction.Description = fields[descriptionIndex].Trim('"');
                    }

                    // Parse balance
                    if (balanceIndex >= 0 && balanceIndex < fields.Length && 
                        decimal.TryParse(fields[balanceIndex].Replace("$", "").Replace(",", ""), out var balance))
                    {
                        transaction.Balance = balance;
                    }

                    // Filter out summary lines and invalid transactions
                    if (transaction.Date != default && transaction.Amount != 0 && 
                        !string.IsNullOrWhiteSpace(transaction.Description) &&
                        !transaction.Description.ToLowerInvariant().Contains("beginning balance") &&
                        !transaction.Description.ToLowerInvariant().Contains("ending balance"))
                    {
                        transactions.Add(transaction);
                        _logger.LogDebug("Added transaction: Date={Date}, Description={Description}, Amount={Amount}", 
                            transaction.Date, transaction.Description, transaction.Amount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error parsing line {LineNumber}: {Error}", i + 1, ex.Message);
                }
            }

            // Apply AI categorization to parsed transactions
            await ApplyAICategorization(transactions);
            
            result.Transactions = transactions;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Failed to parse CSV: {ex.Message}");
        }

        return result;
    }

    private async Task<TransactionParsingResult> ParsePdfAsync(byte[] fileData)
    {
        var result = new TransactionParsingResult();
        
        try
        {
            _logger.LogInformation("Starting PDF parsing with text extraction");
            
            var extractedText = ExtractTextFromPdf(fileData);
            
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidOperationException("Unable to extract text from PDF");
            }
            
            _logger.LogInformation("PDF text extracted: {Length} characters", extractedText.Length);
            
            // Get AI analyzer for transaction parsing
            var aiAnalyzer = _serviceProvider.GetService<AI.IAIBankAnalyzer>();
            if (aiAnalyzer != null)
            {
                // Use AI to parse transactions from PDF text
                var bankInfo = new BankDetectionResult
                {
                    BankName = "Unknown",
                    Country = "US",
                    FileFormat = "PDF"
                };
                
                var aiResult = await aiAnalyzer.ParseTransactionsWithAIAsync(
                    System.Text.Encoding.UTF8.GetBytes(extractedText),
                    "pdf_text.txt", 
                    bankInfo);
                
                if (aiResult.IsSuccessful)
                {
                    result.Transactions = aiResult.Transactions;
                    result.AICost = aiResult.AICost;
                    
                    _logger.LogInformation("AI parsed {Count} transactions from PDF text", result.Transactions.Count);
                    
                    // Log each parsed transaction for debugging
                    foreach (var txn in result.Transactions)
                    {
                        _logger.LogInformation("PDF Transaction: Date={Date}, Description={Desc}, Amount={Amount}, Category={Category}",
                            txn.Date, txn.Description, txn.Amount, txn.Category ?? "N/A");
                    }
                }
                else
                {
                    _logger.LogWarning("AI parsing failed, falling back to basic pattern matching");
                    result.Transactions = ParseTransactionsFromText(extractedText);
                }
            }
            else
            {
                // Fallback to basic pattern matching if AI is not available
                _logger.LogInformation("AI analyzer not available, using basic pattern matching");
                result.Transactions = ParseTransactionsFromText(extractedText);
            }
            
            result.IsSuccessful = true;
            _logger.LogInformation("PDF parsing completed with {Count} transactions", result.Transactions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PDF");
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
        }
        
        return result;
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
                    try
                    {
                        var pageText = PdfTextExtractor.GetTextFromPage(reader, page);
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            text.AppendLine(pageText);
                            _logger.LogDebug("Extracted {Length} characters from page {Page}", 
                                pageText.Length, page);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error extracting text from page {Page}", page);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading PDF document");
            throw new InvalidOperationException($"Failed to read PDF: {ex.Message}", ex);
        }
        
        var extractedText = text.ToString();
        
        // Log sample of extracted text for debugging
        var sampleText = extractedText.Length > 500 ? extractedText.Substring(0, 500) + "..." : extractedText;
        _logger.LogDebug("PDF Text Sample: {Sample}", sampleText);
        
        return extractedText;
    }

    private async Task<TransactionParsingResult> ParseImageAsync(byte[] fileData)
    {
        var result = new TransactionParsingResult();
        
        try
        {
            _logger.LogInformation("Starting image parsing with OCR extraction");
            
            // Get OCR service
            var ocrService = _serviceProvider.GetService<OCR.IOCRService>();
            if (ocrService == null)
            {
                throw new InvalidOperationException("OCR service is not available");
            }
            
            // Extract text from image
            var ocrResult = await ocrService.ExtractTextWithConfidenceAsync(fileData, "image.png");
            
            if (!ocrResult.IsSuccessful)
            {
                throw new InvalidOperationException($"OCR extraction failed: {ocrResult.ErrorMessage}");
            }
            
            _logger.LogInformation("OCR extracted {Length} characters with {Confidence}% confidence", 
                ocrResult.ExtractedText.Length, ocrResult.OverallConfidence * 100);
            
            // Get AI analyzer
            var aiAnalyzer = _serviceProvider.GetService<AI.IAIBankAnalyzer>();
            if (aiAnalyzer != null)
            {
                // Use AI to parse transactions from OCR text
                var bankInfo = new BankDetectionResult
                {
                    BankName = "Unknown",
                    Country = "US",
                    FileFormat = "IMAGE"
                };
                
                var aiResult = await aiAnalyzer.ParseTransactionsWithAIAsync(
                    System.Text.Encoding.UTF8.GetBytes(ocrResult.ExtractedText),
                    "ocr_text.txt", 
                    bankInfo);
                
                if (aiResult.IsSuccessful)
                {
                    result.Transactions = aiResult.Transactions;
                    result.AICost = aiResult.AICost;
                    
                    _logger.LogInformation("AI parsed {Count} transactions from OCR text", result.Transactions.Count);
                    
                    // Log each parsed transaction for debugging
                    foreach (var txn in result.Transactions)
                    {
                        _logger.LogInformation("Parsed transaction: Date={Date}, Description={Desc}, Amount={Amount}, Category={Category}",
                            txn.Date, txn.Description, txn.Amount, txn.Category ?? "N/A");
                    }
                }
                else
                {
                    _logger.LogWarning("AI parsing failed, falling back to pattern matching");
                    result.Transactions = ParseTransactionsFromText(ocrResult.ExtractedText);
                }
            }
            else
            {
                // Fallback to pattern matching if AI is not available
                _logger.LogInformation("AI analyzer not available, using pattern matching");
                result.Transactions = ParseTransactionsFromText(ocrResult.ExtractedText);
            }
            
            result.IsSuccessful = true;
            _logger.LogInformation("Image parsing completed with {Count} transactions", result.Transactions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing image");
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
        }
        
        return result;
    }
    
    private List<ParsedTransaction> ParseTransactionsFromText(string text)
    {
        var transactions = new List<ParsedTransaction>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        _logger.LogInformation("Pattern matching on {Count} lines of OCR text", lines.Length);
        
        foreach (var line in lines)
        {
            // Skip header lines
            if (line.Contains("Date") || line.Contains("Description") || line.Contains("Amount") ||
                line.Contains("Latest Card") || line.Contains("Transactions") || line.Contains("Statement"))
                continue;
            
            // Look for lines with dollar amounts
            if (line.Contains("$"))
            {
                var transaction = ParseTransactionLine(line);
                if (transaction != null)
                {
                    transactions.Add(transaction);
                    _logger.LogDebug("Parsed line: '{Line}' -> Date={Date}, Desc={Desc}, Amount={Amount}",
                        line, transaction.Date, transaction.Description, transaction.Amount);
                }
            }
        }
        
        _logger.LogInformation("Pattern matching found {Count} transactions", transactions.Count);
        return transactions;
    }
    
    private ParsedTransaction? ParseTransactionLine(string line)
    {
        try
        {
            // Parse lines like "Sep 12     Uber                           $5.00"
            // Also handle lines with "Pending" or other status indicators
            
            var cleanLine = line.Trim();
            if (cleanLine.StartsWith("Pending") || cleanLine.Contains("ago"))
                return null; // Skip status lines
            
            // Extract amount
            var dollarIndex = cleanLine.IndexOf('$');
            if (dollarIndex == -1) return null;
            
            var amountEnd = dollarIndex + 1;
            while (amountEnd < cleanLine.Length && (char.IsDigit(cleanLine[amountEnd]) || cleanLine[amountEnd] == '.' || cleanLine[amountEnd] == ','))
                amountEnd++;
            
            var amountStr = cleanLine.Substring(dollarIndex + 1, amountEnd - dollarIndex - 1).Replace(",", "");
            if (!decimal.TryParse(amountStr, out var amount)) return null;
            
            // Extract date and description
            var beforeAmount = cleanLine.Substring(0, dollarIndex).Trim();
            var parts = beforeAmount.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 2) return null;
            
            // Try to parse date (format: "Sep 12")
            DateTime date = DateTime.UtcNow;
            var monthStr = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out var day))
            {
                var month = ParseMonth(monthStr);
                if (month > 0)
                {
                    var year = DateTime.UtcNow.Year;
                    if (month > DateTime.UtcNow.Month) year--; // Previous year if future month
                    date = new DateTime(year, month, day);
                }
            }
            
            // Extract description (everything between date and amount)
            var description = "";
            for (int i = 2; i < parts.Length; i++)
            {
                description += parts[i] + " ";
            }
            description = description.Trim();
            
            if (string.IsNullOrEmpty(description))
                description = "Transaction";
            
            return new ParsedTransaction
            {
                Date = date,
                Description = description,
                Amount = -amount, // Expenses are negative
                Category = null // Let AI handle categorization
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to parse line '{Line}': {Error}", line, ex.Message);
            return null;
        }
    }
    
    private int ParseMonth(string monthStr)
    {
        return monthStr.ToLower() switch
        {
            "jan" or "january" => 1,
            "feb" or "february" => 2,
            "mar" or "march" => 3,
            "apr" or "april" => 4,
            "may" => 5,
            "jun" or "june" => 6,
            "jul" or "july" => 7,
            "aug" or "august" => 8,
            "sep" or "september" => 9,
            "oct" or "october" => 10,
            "nov" or "november" => 11,
            "dec" or "december" => 12,
            _ => 0
        };
    }

    private async Task<ImportPreviewDto> GenerateCsvPreviewAsync(byte[] fileData)
    {
        await Task.CompletedTask;
        var preview = new ImportPreviewDto();

        try
        {
            var csvContent = Encoding.UTF8.GetString(fileData);
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0) return preview;

            // Find the actual header line (look for line with "Date" column)
            int headerLineIndex = -1;
            List<string> headers = null;
            
            for (int i = 0; i < lines.Length; i++)
            {
                var potentialHeaders = lines[i].Split(',').Select(h => h.Trim('"')).ToList();
                if (potentialHeaders.Any(h => h.ToLowerInvariant().Contains("date")) && 
                    potentialHeaders.Any(h => h.ToLowerInvariant().Contains("amount") || h.ToLowerInvariant().Contains("description")))
                {
                    headerLineIndex = i;
                    headers = potentialHeaders;
                    break;
                }
            }

            if (headerLineIndex == -1 || headers == null)
            {
                // Fallback to first line if no clear header is found
                headerLineIndex = 0;
                headers = lines[0].Split(',').Select(h => h.Trim('"')).ToList();
            }

            preview.Headers = headers;

            // Generate sample rows (up to 5)
            for (int i = headerLineIndex + 1; i < Math.Min(lines.Length, headerLineIndex + 6); i++)
            {
                var fields = ParseCsvLine(lines[i]);
                var row = new Dictionary<string, string>();

                for (int j = 0; j < Math.Min(headers.Count, fields.Length); j++)
                {
                    row[headers[j]] = fields[j].Trim('"');
                }

                preview.SampleRows.Add(row);
            }

            // Generate suggested mapping
            preview.SuggestedMapping = new ColumnMappingDto
            {
                DateColumn = FindColumnIndex(headers.ToArray(), new[] { "date", "transaction date", "posting date" }),
                AmountColumn = FindColumnIndex(headers.ToArray(), new[] { "amount", "transaction amount", "debit", "credit" }),
                DescriptionColumn = FindColumnIndex(headers.ToArray(), new[] { "description", "memo", "details", "merchant" }),
                DateFormat = "MM/dd/yyyy" // Default format, will be enhanced with AI detection
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CSV preview");
        }

        return preview;
    }

    private async Task<ImportPreviewDto> GeneratePdfPreviewAsync(byte[] fileData)
    {
        await Task.CompletedTask;
        return new ImportPreviewDto
        {
            Headers = new List<string> { "Note: PDF preview will be available after OCR processing" }
        };
    }

    private async Task<ImportPreviewDto> GenerateImagePreviewAsync(byte[] fileData)
    {
        await Task.CompletedTask;
        return new ImportPreviewDto
        {
            Headers = new List<string> { "Note: Image preview will be available after OCR processing" }
        };
    }

    private int FindColumnIndex(string[] headers, string[] possibleNames)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i].ToLowerInvariant().Trim();
            if (possibleNames.Any(name => header.Contains(name.ToLowerInvariant())))
            {
                return i;
            }
        }
        return -1;
    }

    private string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    private async Task ApplyAICategorization(List<ParsedTransaction> transactions)
    {
        if (!transactions.Any()) return;

        try
        {
            // Get AI analyzer from service provider
            var aiAnalyzer = _serviceProvider.GetService<AI.IAIBankAnalyzer>();
            if (aiAnalyzer == null)
            {
                _logger.LogWarning("AI analyzer not available for transaction categorization");
                return;
            }

            _logger.LogInformation("Applying AI categorization to {Count} transactions", transactions.Count);

            foreach (var transaction in transactions)
            {
                try
                {
                    // Use AI to categorize individual transaction
                    var category = await CategorizeTransactionWithAI(aiAnalyzer, transaction);
                    if (!string.IsNullOrEmpty(category))
                    {
                        transaction.Category = category;
                        _logger.LogDebug("Categorized '{Description}' as '{Category}'", 
                            transaction.Description, category);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to categorize transaction: {Description}", transaction.Description);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI categorization");
        }
    }

    private async Task<string?> CategorizeTransactionWithAI(AI.IAIBankAnalyzer aiAnalyzer, ParsedTransaction transaction)
    {
        // Create a simple categorization prompt for individual transactions
        var prompt = $@"Categorize this financial transaction into one of these categories:

Categories:
- Food & Dining
- Groceries  
- Transportation
- Shopping
- Entertainment
- Bills & Utilities
- Healthcare
- Education
- Travel
- Insurance
- Rent/Mortgage
- Personal Care
- Salary
- Freelance
- Investments
- Other Income
- Transfer

Transaction: {transaction.Description}
Amount: ${transaction.Amount:F2}
Date: {transaction.Date:yyyy-MM-dd}

Return only the category name, nothing else.";

        try
        {
            // Use the AI analyzer's HTTP client to make a categorization request
            var result = await aiAnalyzer.CategorizeTransactionAsync(prompt);
            return result?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI categorization failed for transaction: {Description}", transaction.Description);
            return null;
        }
    }
}