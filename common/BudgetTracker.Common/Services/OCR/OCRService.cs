using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace BudgetTracker.Common.Services.OCR;

public class OCRService : IOCRService
{
    private readonly ILogger<OCRService> _logger;
    private readonly IConfiguration _configuration;

    public OCRService(
        ILogger<OCRService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<string> ExtractTextAsync(byte[] imageData, string fileName)
    {
        var result = await ExtractTextWithConfidenceAsync(imageData, fileName);
        return result.ExtractedText;
    }

    public async Task<OCRResult> ExtractTextWithConfidenceAsync(byte[] imageData, string fileName)
    {
        _logger.LogInformation("Starting OCR extraction for image: {FileName}", fileName);

        try
        {
            // Validate image data
            if (imageData == null || imageData.Length == 0)
            {
                throw new ArgumentException("Invalid image data");
            }

            // Check image quality
            var isAcceptable = await IsImageQualityAcceptableAsync(imageData);
            if (!isAcceptable)
            {
                _logger.LogWarning("Image quality may be too low for accurate OCR: {FileName}", fileName);
            }

            // Preprocess image for better OCR results
            var processedImage = await PreprocessImageAsync(imageData);

            // Perform OCR extraction
            // For now, this is a placeholder - real implementation would use Tesseract.NET
            var result = await PerformMockOCRAsync(processedImage, fileName);

            _logger.LogInformation("OCR completed for {FileName}: {TextLength} characters extracted with {Confidence}% confidence",
                fileName, result.ExtractedText.Length, result.OverallConfidence * 100);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing OCR on image: {FileName}", fileName);
            return new OCRResult
            {
                IsSuccessful = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> IsImageQualityAcceptableAsync(byte[] imageData)
    {
        await Task.CompletedTask;

        try
        {
            // Basic image validation
            if (imageData.Length < 1024) // Too small
                return false;

            if (imageData.Length > 10 * 1024 * 1024) // Too large (>10MB)
                return false;

            // Check for valid image headers
            if (IsJpeg(imageData) || IsPng(imageData))
                return true;

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking image quality");
            return false;
        }
    }

    public async Task<byte[]> PreprocessImageAsync(byte[] imageData)
    {
        await Task.CompletedTask;

        try
        {
            // Image preprocessing would be implemented here using ImageSharp or similar
            // - Deskewing
            // - Noise reduction
            // - Contrast enhancement
            // - Conversion to grayscale
            
            _logger.LogInformation("Image preprocessing completed");
            
            // For now, return original data
            // Real implementation would use SixLabors.ImageSharp
            return imageData;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error preprocessing image, using original");
            return imageData;
        }
    }

    private async Task<OCRResult> PerformMockOCRAsync(byte[] imageData, string fileName)
    {
        await Task.CompletedTask;

        // This is a mock implementation
        // Real implementation would use Tesseract.NET like this:
        /*
        using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
        {
            using (var img = Pix.LoadFromMemory(imageData))
            {
                using (var page = engine.Process(img))
                {
                    var text = page.GetText();
                    var confidence = page.GetMeanConfidence();
                    // Extract regions, bounding boxes, etc.
                }
            }
        }
        */

        // Mock result for development
        var mockText = GenerateMockBankStatementText();
        
        return new OCRResult
        {
            ExtractedText = mockText,
            OverallConfidence = 0.85,
            IsSuccessful = true,
            Regions = new List<TextRegion>
            {
                new TextRegion
                {
                    Text = "BANK STATEMENT",
                    Confidence = 0.95,
                    BoundingBox = new Rectangle { X = 100, Y = 50, Width = 200, Height = 30 }
                },
                new TextRegion
                {
                    Text = mockText,
                    Confidence = 0.80,
                    BoundingBox = new Rectangle { X = 50, Y = 100, Width = 500, Height = 400 }
                }
            }
        };
    }

    private string GenerateMockBankStatementText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Latest Card Transactions");
        sb.AppendLine("");
        sb.AppendLine("Date       Description                     Amount");
        sb.AppendLine("Sep 12     Uber                           $5.00");
        sb.AppendLine("           Pending - Apple Pay            4 hours ago");
        sb.AppendLine("Sep 12     Uber                          $52.25");
        sb.AppendLine("           Pending - Apple Pay            5 hours ago");
        sb.AppendLine("Sep 11     Netflix                        $7.99");
        sb.AppendLine("           Pending - Card Number Used     7 hours ago");
        sb.AppendLine("Sep 11     Uber Eats                     $23.94");
        sb.AppendLine("           Pending - Apple Pay            Yesterday");
        sb.AppendLine("Sep 10     Cinemark                      $14.85");
        sb.AppendLine("           Milpitas, CA                   Yesterday");
        sb.AppendLine("Sep 10     Fi                           $108.04");
        sb.AppendLine("           Card Number Used               Yesterday");
        sb.AppendLine("Sep 09     Giant's Liquor & Food          $5.04");
        sb.AppendLine("           Mountain View, CA              Thursday");
        
        return sb.ToString();
    }

    private bool IsJpeg(byte[] data)
    {
        return data.Length >= 4 && 
               data[0] == 0xFF && data[1] == 0xD8 && 
               data[2] == 0xFF;
    }

    private bool IsPng(byte[] data)
    {
        return data.Length >= 8 && 
               data[0] == 0x89 && data[1] == 0x50 && 
               data[2] == 0x4E && data[3] == 0x47 &&
               data[4] == 0x0D && data[5] == 0x0A && 
               data[6] == 0x1A && data[7] == 0x0A;
    }
}