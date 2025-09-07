using BudgetTracker.Common.DTOs;

namespace BudgetTracker.API.Services;

public interface IImportService
{
    Task<ImportPreviewDto> PreviewImportAsync(string fileName, byte[] fileData);
    Task<Guid> StartImportAsync(Guid userId, FileImportDto importDto);
    Task<ImportStatusDto?> GetImportStatusAsync(Guid userId, Guid importId);
    Task<IEnumerable<ImportStatusDto>> GetImportHistoryAsync(Guid userId);
}