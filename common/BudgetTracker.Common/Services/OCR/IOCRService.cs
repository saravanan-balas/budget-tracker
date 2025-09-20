using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
namespace BudgetTracker.Common.Services.OCR;

public interface IOCRService
{
    Task<string> ExtractTextAsync(byte[] imageData, string fileName);
    Task<OCRResult> ExtractTextWithConfidenceAsync(byte[] imageData, string fileName);
    Task<bool> IsImageQualityAcceptableAsync(byte[] imageData);
    Task<byte[]> PreprocessImageAsync(byte[] imageData);
}

public class OCRResult
{
    public string ExtractedText { get; set; } = string.Empty;
    public double OverallConfidence { get; set; }
    public List<TextRegion> Regions { get; set; } = new();
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TextRegion
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public BoundingRectangle BoundingBox { get; set; } = new();
}

public class BoundingRectangle
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}