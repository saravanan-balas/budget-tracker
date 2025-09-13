using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;
using BudgetTracker.Common.DTOs;

namespace BudgetTracker.Common.Services.Parsing;

public class FormatDetectionService : IFormatDetectionService
{
    private readonly ILogger<FormatDetectionService> _logger;

    // File signatures (magic bytes) for format detection
    private static readonly Dictionary<string, byte[][]> FileSignatures = new()
    {
        ["PDF"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } }, // %PDF
        ["PNG"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        ["JPEG"] = new[] 
        {
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, // JFIF
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, // EXIF
            new byte[] { 0xFF, 0xD8, 0xFF, 0xDB }  // JPEG raw
        }
    };

    public FormatDetectionService(ILogger<FormatDetectionService> logger)
    {
        _logger = logger;
    }

    public async Task<string> DetectFormatAsync(byte[] fileData, string fileName)
    {
        await Task.CompletedTask;
        
        if (fileData.Length == 0)
            return "UNKNOWN";

        // Check file signatures first (more reliable than extensions)
        foreach (var (format, signatures) in FileSignatures)
        {
            if (signatures.Any(signature => fileData.Take(signature.Length).SequenceEqual(signature)))
            {
                return format;
            }
        }

        // Fallback to extension-based detection
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => "CSV",
            ".pdf" => "PDF",
            ".png" => "PNG",
            ".jpg" or ".jpeg" => "JPEG",
            ".txt" => IsLikelyCsv(fileData) ? "CSV" : "TEXT",
            _ => "UNKNOWN"
        };
    }

    public async Task<FileAnalysisResult> AnalyzeFileAsync(byte[] fileData, string fileName)
    {
        var format = await DetectFormatAsync(fileData, fileName);
        var fileSize = fileData.Length;
        
        var analysis = new FileAnalysisResult
        {
            FileFormat = format,
            FileSize = fileSize
        };

        // Determine processing approach based on file characteristics
        analysis.CanProcessSynchronously = format switch
        {
            "CSV" when fileSize < 100_000 => true, // Small CSV files
            "PDF" => false, // PDFs always async due to OCR potential
            "PNG" or "JPEG" => false, // Images always async due to OCR
            _ => false
        };

        if (!analysis.CanProcessSynchronously)
        {
            analysis.AsyncReason = format switch
            {
                "PDF" => "PDF processing requires text extraction and potential OCR",
                "PNG" or "JPEG" => "Image processing requires OCR",
                "CSV" when fileSize >= 100_000 => "Large CSV file requires background processing",
                _ => "Unknown format requires AI analysis"
            };

            analysis.EstimatedSeconds = format switch
            {
                "PDF" => EstimatePdfProcessingTime(fileSize),
                "PNG" or "JPEG" => EstimateImageProcessingTime(fileSize),
                "CSV" => EstimateCsvProcessingTime(fileSize),
                _ => 30
            };
        }

        // Estimate row count for CSV files
        if (format == "CSV")
        {
            analysis.EstimatedRowCount = EstimateCsvRowCount(fileData);
        }

        return analysis;
    }

    public async Task<bool> IsValidFileAsync(byte[] fileData, string fileName)
    {
        var format = await DetectFormatAsync(fileData, fileName);
        
        return format switch
        {
            "CSV" => IsValidCsv(fileData),
            "PDF" => IsValidPdf(fileData),
            "PNG" or "JPEG" => IsValidImage(fileData, format),
            _ => false
        };
    }

    private bool IsLikelyCsv(byte[] fileData)
    {
        if (fileData.Length == 0) return false;
        
        try
        {
            var sample = Encoding.UTF8.GetString(fileData.Take(1000).ToArray());
            var lines = sample.Split('\n').Take(5).ToArray();
            
            if (lines.Length < 2) return false;
            
            // Check if lines have consistent comma count
            var firstLineCommas = lines[0].Count(c => c == ',');
            return firstLineCommas > 0 && 
                   lines.Skip(1).All(line => Math.Abs(line.Count(c => c == ',') - firstLineCommas) <= 1);
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidCsv(byte[] fileData)
    {
        try
        {
            var sample = Encoding.UTF8.GetString(fileData.Take(1000).ToArray());
            return sample.Contains(',') || sample.Contains(';') || sample.Contains('\t');
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidPdf(byte[] fileData)
    {
        return fileData.Length > 4 && 
               fileData.Take(4).SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 });
    }

    private bool IsValidImage(byte[] fileData, string format)
    {
        return FileSignatures.ContainsKey(format) && 
               FileSignatures[format].Any(signature => 
                   fileData.Take(signature.Length).SequenceEqual(signature));
    }

    private int EstimateCsvRowCount(byte[] fileData)
    {
        try
        {
            var text = Encoding.UTF8.GetString(fileData);
            return text.Count(c => c == '\n') + 1;
        }
        catch
        {
            return (int)(fileData.Length / 100); // Rough estimate
        }
    }

    private int EstimatePdfProcessingTime(long fileSize)
    {
        // Estimate based on file size: ~2 seconds per MB
        var mbSize = fileSize / (1024.0 * 1024.0);
        return Math.Max(5, (int)(mbSize * 2));
    }

    private int EstimateImageProcessingTime(long fileSize)
    {
        // OCR processing time estimation
        var mbSize = fileSize / (1024.0 * 1024.0);
        return Math.Max(10, (int)(mbSize * 5));
    }

    private int EstimateCsvProcessingTime(long fileSize)
    {
        // Large CSV processing time
        var mbSize = fileSize / (1024.0 * 1024.0);
        return Math.Max(3, (int)(mbSize * 1));
    }
}