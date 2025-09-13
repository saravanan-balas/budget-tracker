using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using BudgetTracker.Common.DTOs;

namespace BudgetTracker.Common.Services.Parsing;

public interface IFormatDetectionService
{
    Task<string> DetectFormatAsync(byte[] fileData, string fileName);
    Task<FileAnalysisResult> AnalyzeFileAsync(byte[] fileData, string fileName);
    Task<bool> IsValidFileAsync(byte[] fileData, string fileName);
}