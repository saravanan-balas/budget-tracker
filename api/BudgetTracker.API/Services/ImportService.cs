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
    private readonly ISmartImportService _smartImportService;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        BudgetTrackerDbContext context,
        IBlobStorageService blobStorageService,
        ISmartImportService smartImportService,
        ILogger<ImportService> logger)
    {
        _context = context;
        _blobStorageService = blobStorageService;
        _smartImportService = smartImportService;
        _logger = logger;
    }

    public async Task<ImportPreviewDto> PreviewImportAsync(string fileName, byte[] fileData)
    {
        // Delegate to the smart import service for enhanced preview generation
        return await _smartImportService.GeneratePreviewAsync(fileData, fileName);
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

        var status = new ImportStatusDto
        {
            ImportId = importFile.Id,
            Status = importFile.Status.ToString(),
            TotalRows = importFile.TotalRows,
            ProcessedRows = importFile.ProcessedRows,
            ImportedTransactions = importFile.ImportedTransactions,
            DuplicateTransactions = importFile.DuplicateTransactions,
            FailedRows = importFile.FailedRows,
            ErrorDetails = importFile.ErrorDetails,
            DetectedBankName = importFile.DetectedBankName,
            DetectedFormat = importFile.DetectedFormat,
            AICost = importFile.AICost,
            IsProcessedSynchronously = importFile.IsProcessedSynchronously
        };

        // Calculate estimated time remaining for processing imports
        if (importFile.Status == ImportStatus.Processing && !importFile.IsProcessedSynchronously)
        {
            var elapsed = DateTime.UtcNow - (importFile.ProcessingStartedAt ?? importFile.CreatedAt);
            var estimatedTotal = TimeSpan.FromMinutes(2); // Default 2 minutes for async processing
            var remaining = estimatedTotal - elapsed;
            status.EstimatedSecondsRemaining = Math.Max(0, (int)remaining.TotalSeconds);
        }

        return status;
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