using BudgetTracker.Common.Data;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.API.Services;

public class ImportService : IImportService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        BudgetTrackerDbContext context,
        IBlobStorageService blobStorageService,
        ILogger<ImportService> logger)
    {
        _context = context;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<ImportPreviewDto> PreviewImportAsync(string fileName, byte[] fileData)
    {
        await Task.CompletedTask;
        
        var preview = new ImportPreviewDto
        {
            Headers = new List<string> { "Date", "Description", "Amount", "Balance" },
            SampleRows = new List<Dictionary<string, string>>
            {
                new() { ["Date"] = "2024-01-15", ["Description"] = "Sample Transaction", ["Amount"] = "-45.99", ["Balance"] = "1234.56" }
            },
            SuggestedMapping = new ColumnMappingDto
            {
                DateColumn = 0,
                DescriptionColumn = 1,
                AmountColumn = 2,
                DateFormat = "yyyy-MM-dd"
            }
        };

        return preview;
    }

    public async Task<Guid> StartImportAsync(Guid userId, FileImportDto importDto)
    {
        var importFile = new ImportedFile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = importDto.FileName,
            FileType = importDto.FileType,
            FileSize = importDto.FileData.Length,
            Status = ImportStatus.Pending,
            BankTemplate = importDto.BankTemplate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ImportedFiles.Add(importFile);
        await _context.SaveChangesAsync();

        var blobUrl = await _blobStorageService.UploadFileAsync(
            "imports",
            $"{userId}/{importFile.Id}{importDto.FileType}",
            new MemoryStream(importDto.FileData),
            "application/octet-stream"
        );

        importFile.BlobUrl = blobUrl;
        importFile.Status = ImportStatus.Processing;
        importFile.ProcessingStartedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        _logger.LogInformation("Import job queued for file {FileId}", importFile.Id);

        return importFile.Id;
    }

    public async Task<ImportStatusDto?> GetImportStatusAsync(Guid userId, Guid importId)
    {
        var importFile = await _context.ImportedFiles
            .FirstOrDefaultAsync(f => f.Id == importId && f.UserId == userId);

        if (importFile == null)
            return null;

        return new ImportStatusDto
        {
            ImportId = importFile.Id,
            Status = importFile.Status.ToString(),
            TotalRows = importFile.TotalRows,
            ProcessedRows = importFile.ProcessedRows,
            ImportedTransactions = importFile.ImportedTransactions,
            DuplicateTransactions = importFile.DuplicateTransactions,
            FailedRows = importFile.FailedRows,
            ErrorDetails = importFile.ErrorDetails
        };
    }

    public async Task<IEnumerable<ImportStatusDto>> GetImportHistoryAsync(Guid userId)
    {
        var imports = await _context.ImportedFiles
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(20)
            .Select(f => new ImportStatusDto
            {
                ImportId = f.Id,
                Status = f.Status.ToString(),
                TotalRows = f.TotalRows,
                ProcessedRows = f.ProcessedRows,
                ImportedTransactions = f.ImportedTransactions,
                DuplicateTransactions = f.DuplicateTransactions,
                FailedRows = f.FailedRows,
                ErrorDetails = f.ErrorDetails
            })
            .ToListAsync();

        return imports;
    }
}