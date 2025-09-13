using BudgetTracker.Common.DTOs;

namespace BudgetTracker.API.Services;

public interface ISmartImportService
{
    Task<ImportResult> ProcessSmartImportAsync(Guid userId, FileImportDto importDto);
    Task<FileAnalysisResult> AnalyzeImportFileAsync(byte[] fileData, string fileName);
    Task<ImportPreviewDto> GeneratePreviewAsync(byte[] fileData, string fileName);
    Task<decimal> EstimateProcessingCostAsync(byte[] fileData, string fileName);
}