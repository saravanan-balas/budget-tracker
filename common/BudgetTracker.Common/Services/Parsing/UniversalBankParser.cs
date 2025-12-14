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

        _logger.LogInformation("[PARSER-START] ParseFileAsync called for {FileName}, FileSize: {FileSize} bytes", 
            fileName, fileData.Length);

        try
        {
            _logger.LogDebug("[PARSER-STEP-1] Detecting file format");
            var format = await _formatDetection.DetectFormatAsync(fileData, fileName);
            _logger.LogInformation("[PARSER-STEP-1-COMPLETE] File {FileName} detected as format: {Format}", fileName, format);

            _logger.LogDebug("[PARSER-STEP-2] Starting format-specific parsing for {Format}", format);
            
            result = format switch
            {
                "CSV" => await ParseCsvAsync(fileData),
                "PDF" => await ParsePdfAsync(fileData),
                "PNG" or "JPEG" => await ParseImageAsync(fileData),
                _ => throw new NotSupportedException($"File format {format} is not supported")
            };
            
            _logger.LogDebug("[PARSER-STEP-2-COMPLETE] Format-specific parsing completed");

            result.IsSuccessful = true;
            _logger.LogInformation("[PARSER-SUCCESS] Successfully parsed {Count} transactions from {FileName}. Processing time: {ProcessingTime}ms", 
                result.Transactions.Count, fileName, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PARSER-ERROR] Error parsing file {FileName}. Error: {ErrorMessage}", 
                fileName, ex.Message);
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            _logger.LogInformation("[PARSER-COMPLETE] Total parsing time: {TotalTime}ms for {FileName}", 
                stopwatch.ElapsedMilliseconds, fileName);
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

        _logger.LogDebug("[CSV-START] Starting CSV parsing, FileSize: {FileSize} bytes", fileData.Length);

        try
        {
            var csvContent = Encoding.UTF8.GetString(fileData);
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            _logger.LogDebug("[CSV-STEP-1] CSV split into {LineCount} lines", lines.Length);

            if (lines.Length < 2)
            {
                throw new InvalidDataException("CSV file must contain at least header and one data row");
            }

            // Find the actual header line (look for line with "Date" column)
            _logger.LogDebug("[CSV-STEP-2] Searching for header line");
            int headerLineIndex = -1;
            string[]? headers = null;
            
            for (int i = 0; i < lines.Length; i++)
            {
                var potentialHeaders = lines[i].Split(',').Select(h => h.Trim('"')).ToArray();
                _logger.LogDebug("[CSV-STEP-2] Line {LineIndex} headers: {Headers}", i, string.Join(", ", potentialHeaders));
                
                if (potentialHeaders.Any(h => h.ToLowerInvariant().Contains("date")) && 
                    potentialHeaders.Any(h => h.ToLowerInvariant().Contains("amount") || h.ToLowerInvariant().Contains("description")))
                {
                    headerLineIndex = i;
                    headers = potentialHeaders;
                    _logger.LogInformation("[CSV-STEP-2-COMPLETE] Found header at line {LineIndex}", i);
                    break;
                }
            }

            if (headerLineIndex == -1 || headers == null)
            {
                throw new InvalidDataException("Could not find valid header line in CSV");
            }

            _logger.LogInformation("[CSV-HEADERS] Header line at index {Index}: {Headers}", headerLineIndex, string.Join(", ", headers));

            var transactions = new List<ParsedTransaction>();

            // Try to identify column indices
            _logger.LogDebug("[CSV-STEP-3] Identifying column indices");
            var dateIndex = FindColumnIndex(headers, new[] { 
                "date", "transaction date", "posting date", "posted date", "trans date",
                "post date", "trans. date", "transaction dt", "value date", "booking date",
                "settlement date", "processed date", "effective date", "activity date",
                "payment date", "purchase date"
            });
            var amountIndex = FindColumnIndex(headers, new[] { 
                "amount", "transaction amount", "trans amount", "payment", "charge", 
                "trans. amount", "transaction amt", "money", "sum", "value", "net amount",
                "transaction value", "payment amount"
            });
            
            // Check for separate debit/credit columns (common in many banks)
            var debitIndex = FindColumnIndex(headers, new[] { 
                "debit", "withdrawal", "debit amount", "withdrawals", "paid out", 
                "money out", "debits", "debit amt", "charges", "expense", "outflow"
            });
            var creditIndex = FindColumnIndex(headers, new[] { 
                "credit", "deposit", "credit amount", "deposits", "paid in", 
                "money in", "credits", "credit amt", "income", "inflow", "lodgement"
            });
            var descriptionIndex = FindColumnIndex(headers, new[] { 
                "payee", "description", "memo", "details", "merchant", "narrative", 
                "transaction description", "trans description", "merchant name", "vendor",
                "particulars", "transaction details", "payment details",
                "trans. description", "transaction desc", "name", "to/from", "beneficiary",
                "counterparty", "recipient", "statement description", "transaction narrative"
            });
            var balanceIndex = FindColumnIndex(headers, new[] { 
                "balance", "running balance", "running bal", "available balance", 
                "current balance", "closing balance", "ending balance", "new balance",
                "balance after", "post balance", "ledger balance", "available bal",
                "account balance", "total balance", "final balance"
            });
            
            // Try to find reference/check number column (must check after description to avoid conflicts)
            var referenceIndex = FindColumnIndex(headers, new[] {
                "reference number", "ref number", "ref no", "reference no",
                "check number", "cheque number", "check no", "cheque no", "check #",
                "transaction id", "trans id", "transaction ref", "trans ref", "confirmation number",
                "confirmation code", "auth code", "authorization code", "trace number",
                "reference" // Check "reference" last to avoid matching columns like "Payment Reference"
            });
            
            // Try to find transaction type column (indicates debit/credit)
            var typeIndex = FindColumnIndex(headers, new[] {
                "type", "transaction type", "trans type", "txn type", "transaction kind",
                "debit/credit", "dr/cr", "d/c", "direction", "payment type", "category",
                "transaction category", "trans. type"
            });
            
            _logger.LogInformation("[CSV-COLUMNS] DateIndex: {DateIndex}, AmountIndex: {AmountIndex}, DebitIndex: {DebitIndex}, CreditIndex: {CreditIndex}, TypeIndex: {TypeIndex}, DescriptionIndex: {DescIndex}, BalanceIndex: {BalIndex}, ReferenceIndex: {RefIndex}",
                dateIndex, amountIndex, debitIndex, creditIndex, typeIndex, descriptionIndex, balanceIndex, referenceIndex);

            _logger.LogDebug("[CSV-STEP-4] Parsing {Count} data rows", lines.Length - headerLineIndex - 1);
            
            for (int i = headerLineIndex + 1; i < lines.Length; i++)
            {
                try
                {
                    _logger.LogDebug("[CSV-ROW-{LineNumber}] Processing line: {Line}", i, lines[i]);
                    var fields = ParseCsvLine(lines[i]);
                    if (fields.Length < Math.Max(dateIndex + 1, amountIndex + 1)) continue;

                    var transaction = new ParsedTransaction();

                    // Parse date - try various formats
                    if (dateIndex >= 0 && dateIndex < fields.Length)
                    {
                        transaction.Date = ParseFlexibleDate(fields[dateIndex]);
                    }

                    // Parse amount - handle both single amount column and separate debit/credit columns
                    if (amountIndex >= 0 && amountIndex < fields.Length)
                    {
                        var amountStr = fields[amountIndex].Replace("$", "").Replace(",", "").Replace("(", "-").Replace(")", "");
                        if (decimal.TryParse(amountStr, out var amount))
                        {
                            transaction.Amount = amount;
                            
                            // Check if there's a type column to determine sign
                            // Only override sign if amount is zero or if type indicates a mismatch
                            if (typeIndex >= 0 && typeIndex < fields.Length)
                            {
                                var typeStr = fields[typeIndex].ToLowerInvariant();
                                
                                // Check if amount already has correct sign based on type
                                bool isDebitType = typeStr.Contains("debit") || typeStr.Contains("withdrawal") || 
                                                  typeStr.Contains("expense") || typeStr.Contains("charge") ||
                                                  typeStr.Contains("payment") || typeStr.Contains("dr") ||
                                                  typeStr == "d";
                                bool isCreditType = typeStr.Contains("credit") || typeStr.Contains("deposit") ||
                                                   typeStr.Contains("income") || typeStr.Contains("cr") ||
                                                   typeStr.Contains("refund") || typeStr == "c";
                                
                                if (isDebitType && amount > 0)
                                {
                                    // Type says debit but amount is positive - make it negative
                                    transaction.Amount = -amount;
                                }
                                else if (isCreditType && amount < 0)
                                {
                                    // Type says credit but amount is negative - make it positive
                                    transaction.Amount = -amount;
                                }
                                // If signs already match (negative debit or positive credit), keep as-is
                            }
                        }
                    }
                    else if (debitIndex >= 0 || creditIndex >= 0)
                    {
                        // Handle separate debit/credit columns
                        decimal debitAmount = 0, creditAmount = 0;
                        
                        if (debitIndex >= 0 && debitIndex < fields.Length && !string.IsNullOrWhiteSpace(fields[debitIndex]))
                        {
                            var debitStr = fields[debitIndex].Replace("$", "").Replace(",", "").Replace("(", "").Replace(")", "");
                            decimal.TryParse(debitStr, out debitAmount);
                        }
                        
                        if (creditIndex >= 0 && creditIndex < fields.Length && !string.IsNullOrWhiteSpace(fields[creditIndex]))
                        {
                            var creditStr = fields[creditIndex].Replace("$", "").Replace(",", "").Replace("(", "").Replace(")", "");
                            decimal.TryParse(creditStr, out creditAmount);
                        }
                        
                        // Debits are negative (money out), credits are positive (money in)
                        if (debitAmount != 0)
                            transaction.Amount = -Math.Abs(debitAmount);
                        else if (creditAmount != 0)
                            transaction.Amount = Math.Abs(creditAmount);
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

                    // Parse reference number
                    if (referenceIndex >= 0 && referenceIndex < fields.Length)
                    {
                        transaction.Reference = fields[referenceIndex].Trim('"');
                    }

                    // Filter out summary lines and invalid transactions
                    if (transaction.Date != default && transaction.Amount != 0 && 
                        !string.IsNullOrWhiteSpace(transaction.Description) &&
                        !transaction.Description.ToLowerInvariant().Contains("beginning balance") &&
                        !transaction.Description.ToLowerInvariant().Contains("ending balance"))
                    {
                        transactions.Add(transaction);
                        _logger.LogDebug("[CSV-TRANSACTION-{Index}] Added: Date={Date}, Description={Description}, Amount={Amount}, Balance={Balance}", 
                            transactions.Count, transaction.Date, transaction.Description, transaction.Amount, transaction.Balance);
                    }
                    else
                    {
                        _logger.LogDebug("[CSV-SKIP-{LineNumber}] Skipped invalid/summary line", i);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[CSV-ROW-ERROR] Error parsing line {LineNumber}: {Error}. Line content: {Line}", 
                        i + 1, ex.Message, lines[i]);
                }
            }

            _logger.LogInformation("[CSV-STEP-5] Parsed {Count} valid transactions", transactions.Count);
            
            // Skip AI categorization here - let OptimizedBatchTransactionService handle it with caching and rule-based logic
            // await ApplyAICategorization(transactions);
            
            result.Transactions = transactions;
            _logger.LogInformation("[CSV-COMPLETE] CSV parsing completed with {Count} transactions", result.Transactions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CSV-ERROR] Failed to parse CSV");
            throw new InvalidDataException($"Failed to parse CSV: {ex.Message}");
        }

        return result;
    }
    
    private DateTime ParseFlexibleDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return DateTime.Now;
        
        // Clean up the date string
        dateStr = dateStr.Trim().Trim('"');
        
        // Try standard parsing first
        if (DateTime.TryParse(dateStr, out var date))
            return date;
        
        // Try various date formats commonly used by banks
        string[] formats = new[]
        {
            "MM/dd/yyyy", "M/d/yyyy", "MM/dd/yy", "M/d/yy",
            "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy",
            "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "MM-dd-yyyy",
            "MMM dd, yyyy", "MMMM dd, yyyy", "dd MMM yyyy", "dd MMMM yyyy",
            "MM-dd-yy", "dd-MM-yy", "yyyyMMdd", "ddMMyyyy", "MMddyyyy",
            "dd.MM.yyyy", "MM.dd.yyyy", "dd.MM.yy", "MM.dd.yy",
            "dd-MMM-yyyy", "dd-MMM-yy", "MMM dd yyyy", "MMMM dd yyyy",
            "yyyy.MM.dd", "yy/MM/dd", "dd/MMM/yyyy", "MMM/dd/yyyy"
        };
        
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateStr, format, 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, out date))
            {
                return date;
            }
        }
        
        // If all parsing fails, return current date
        _logger.LogWarning("Unable to parse date: {DateStr}, using current date", dateStr);
        return DateTime.Now;
    }

    private async Task<TransactionParsingResult> ParsePdfAsync(byte[] fileData)
    {
        var result = new TransactionParsingResult();
        
        _logger.LogInformation("[PDF-START] Starting PDF parsing, FileSize: {FileSize} bytes", fileData.Length);
        
        try
        {
            _logger.LogDebug("[PDF-STEP-1] Extracting text from PDF");
            
            var extractedText = ExtractTextFromPdf(fileData);
            
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                _logger.LogError("[PDF-ERROR] Unable to extract text from PDF");
                throw new InvalidOperationException("Unable to extract text from PDF");
            }
            
            _logger.LogInformation("[PDF-STEP-1-COMPLETE] PDF text extracted: {Length} characters", extractedText.Length);
            
            // Get AI analyzer for transaction parsing
            _logger.LogDebug("[PDF-STEP-2] Getting AI analyzer for transaction parsing");
            var aiAnalyzer = _serviceProvider.GetService<AI.IAIBankAnalyzer>();
            if (aiAnalyzer != null)
            {
                _logger.LogDebug("[PDF-STEP-2] AI analyzer available, using AI parsing");
                // Use AI to parse transactions from PDF text
                var bankInfo = new BankDetectionResult
                {
                    BankName = "Unknown",
                    Country = "US",
                    FileFormat = "PDF"
                };
                
                _logger.LogDebug("[PDF-AI] Calling AI analyzer to parse transactions");
                var aiResult = await aiAnalyzer.ParseTransactionsWithAIAsync(
                    System.Text.Encoding.UTF8.GetBytes(extractedText),
                    "pdf_text.txt", 
                    bankInfo);
                
                _logger.LogDebug("[PDF-AI] AI parsing result: IsSuccessful={IsSuccessful}, TransactionCount={Count}", 
                    aiResult.IsSuccessful, aiResult.Transactions?.Count ?? 0);
                
                if (aiResult.IsSuccessful)
                {
                    result.Transactions = aiResult.Transactions;
                    result.AICost = aiResult.AICost;
                    
                    _logger.LogInformation("[PDF-AI-SUCCESS] AI parsed {Count} transactions from PDF text. AI Cost: ${Cost}", 
                        result.Transactions.Count, result.AICost);
                    
                    // Log each parsed transaction for debugging
                    int txnIndex = 0;
                    foreach (var txn in result.Transactions)
                    {
                        _logger.LogDebug("[PDF-TXN-{Index}] Date={Date}, Description={Desc}, Amount={Amount}, Category={Category}",
                            ++txnIndex, txn.Date, txn.Description, txn.Amount, txn.Category ?? "N/A");
                    }
                }
                else
                {
                    _logger.LogWarning("[PDF-AI-FALLBACK] AI parsing failed: {ErrorMessage}, falling back to basic pattern matching", 
                        aiResult.ErrorMessage ?? "Unknown error");
                    result.Transactions = ParseTransactionsFromText(extractedText);
                }
            }
            else
            {
                // Fallback to basic pattern matching if AI is not available
                _logger.LogWarning("[PDF-NO-AI] AI analyzer not available, using basic pattern matching");
                result.Transactions = ParseTransactionsFromText(extractedText);
            }
            
            result.IsSuccessful = true;
            _logger.LogInformation("[PDF-SUCCESS] PDF parsing completed with {Count} transactions", result.Transactions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PDF-ERROR] Error parsing PDF: {ErrorMessage}", ex.Message);
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
            List<string>? headers = null;
            
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
        // First pass: try exact match (case-insensitive)
        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i].ToLowerInvariant().Trim();
            if (possibleNames.Any(name => header.Equals(name.ToLowerInvariant())))
            {
                return i;
            }
        }
        
        // Second pass: try contains match for compound names
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

        _logger.LogDebug("[AI-CATEGORIZATION-START] Starting categorization for {Count} transactions", transactions.Count);

        try
        {
            // Get AI analyzer from service provider
            var aiAnalyzer = _serviceProvider.GetService<AI.IAIBankAnalyzer>();
            if (aiAnalyzer == null)
            {
                _logger.LogWarning("[AI-CATEGORIZATION] AI analyzer not available for transaction categorization");
                return;
            }

            _logger.LogInformation("[AI-CATEGORIZATION] Applying AI categorization to {Count} transactions", transactions.Count);

            int categorizedCount = 0;
            for (int i = 0; i < transactions.Count; i++)
            {
                var transaction = transactions[i];
                try
                {
                    _logger.LogDebug("[AI-CAT-{Index}] Categorizing: {Description}", i + 1, transaction.Description);
                    
                    // Use AI to categorize individual transaction
                    var category = await CategorizeTransactionWithAI(aiAnalyzer, transaction);
                    if (!string.IsNullOrEmpty(category))
                    {
                        transaction.Category = category;
                        categorizedCount++;
                        _logger.LogDebug("[AI-CAT-{Index}-SUCCESS] '{Description}' categorized as '{Category}'", 
                            i + 1, transaction.Description, category);
                    }
                    else
                    {
                        _logger.LogDebug("[AI-CAT-{Index}-EMPTY] No category returned for '{Description}'", 
                            i + 1, transaction.Description);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AI-CAT-{Index}-ERROR] Failed to categorize transaction: {Description}", 
                        i + 1, transaction.Description);
                }
            }
            
            _logger.LogInformation("[AI-CATEGORIZATION-COMPLETE] Categorized {CategorizedCount}/{TotalCount} transactions", 
                categorizedCount, transactions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI-CATEGORIZATION-ERROR] Error during AI categorization");
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