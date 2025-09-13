using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text;
using BudgetTracker.Common.DTOs;

namespace BudgetTracker.Common.Services.Parsing;

public class UniversalBankParser : IUniversalBankParser
{
    private readonly IFormatDetectionService _formatDetection;
    private readonly ILogger<UniversalBankParser> _logger;

    public UniversalBankParser(
        IFormatDetectionService formatDetection,
        ILogger<UniversalBankParser> logger)
    {
        _formatDetection = formatDetection;
        _logger = logger;
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

            // Simple CSV parsing - this will be enhanced with AI later
            var headers = lines[0].Split(',').Select(h => h.Trim('"')).ToArray();
            var transactions = new List<ParsedTransaction>();

            // Try to identify column indices
            var dateIndex = FindColumnIndex(headers, new[] { "date", "transaction date", "posting date" });
            var amountIndex = FindColumnIndex(headers, new[] { "amount", "transaction amount", "debit", "credit" });
            var descriptionIndex = FindColumnIndex(headers, new[] { "description", "memo", "details", "merchant" });
            var balanceIndex = FindColumnIndex(headers, new[] { "balance", "running balance" });

            for (int i = 1; i < lines.Length; i++)
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

                    if (transaction.Date != default && transaction.Amount != 0)
                    {
                        transactions.Add(transaction);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error parsing line {LineNumber}: {Error}", i + 1, ex.Message);
                }
            }

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
        await Task.CompletedTask;
        
        // Placeholder for PDF parsing - will be implemented with iTextSharp
        throw new NotImplementedException("PDF parsing will be implemented with OCR support");
    }

    private async Task<TransactionParsingResult> ParseImageAsync(byte[] fileData)
    {
        await Task.CompletedTask;
        
        // Placeholder for image parsing - will be implemented with OCR
        throw new NotImplementedException("Image parsing will be implemented with OCR support");
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

            var headers = lines[0].Split(',').Select(h => h.Trim('"')).ToList();
            preview.Headers = headers;

            // Generate sample rows (up to 5)
            for (int i = 1; i < Math.Min(lines.Length, 6); i++)
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
}