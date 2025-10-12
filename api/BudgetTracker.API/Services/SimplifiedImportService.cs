using BudgetTracker.Common.Data;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.DTOs.Messaging;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services;
using BudgetTracker.Common.Services.Messaging;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.API.Services;

public interface ISimplifiedImportService
{
    Task<ImportResult> UploadFileAsync(Guid userId, FileImportDto importDto);
    Task<ImportStatusDto?> GetImportStatusAsync(Guid userId, Guid importId);
    Task<IEnumerable<ImportStatusDto>> GetImportHistoryAsync(Guid userId);
    Task<ImportPreviewDto> GeneratePreviewAsync(byte[] fileData, string fileName);
}

public class SimplifiedImportService : ISimplifiedImportService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<SimplifiedImportService> _logger;
    private readonly IMessageQueueService? _messageQueue;

    public SimplifiedImportService(
        BudgetTrackerDbContext context,
        IBlobStorageService blobStorageService,
        ILogger<SimplifiedImportService> logger,
        IMessageQueueService? messageQueue = null)
    {
        _context = context;
        _blobStorageService = blobStorageService;
        _logger = logger;
        _messageQueue = messageQueue;
    }

    public async Task<ImportResult> UploadFileAsync(Guid userId, FileImportDto importDto)
    {
        _logger.LogInformation("[IMPORT-START] Uploading file {FileName} for user {UserId}, FileSize: {FileSize} bytes, FileType: {FileType}", 
            importDto.FileName, userId, importDto.FileData.Length, importDto.FileType);

        try
        {
            // Create import record
            _logger.LogDebug("[IMPORT-STEP-1] Creating import record in database");
            var importFile = await CreateImportRecordAsync(userId, importDto);
            _logger.LogInformation("[IMPORT-STEP-1-COMPLETE] Import record created with ID: {ImportId}", importFile.Id);

            // Upload file to blob storage
            _logger.LogDebug("[IMPORT-STEP-2] Starting blob storage upload");
            var blobPath = $"{userId}/{importFile.Id}{importDto.FileType}";
            _logger.LogDebug("[IMPORT-STEP-2] Blob path: {BlobPath}", blobPath);
            
            var blobUrl = await _blobStorageService.UploadFileAsync(
                "imports",
                blobPath,
                new MemoryStream(importDto.FileData),
                "application/octet-stream"
            );
            _logger.LogInformation("[IMPORT-STEP-2-COMPLETE] File uploaded to blob storage: {BlobUrl}", blobUrl);

            _logger.LogDebug("[IMPORT-STEP-3] Updating import record and publishing message to Redis");
            importFile.BlobUrl = blobUrl;
            importFile.Status = ImportStatus.Processing;
            importFile.ProcessingStartedAt = DateTime.UtcNow;
            
            _logger.LogDebug("[IMPORT-STEP-3] Saving changes to database");
            await _context.SaveChangesAsync();

            _logger.LogDebug("[IMPORT-STEP-4] Publishing message to Redis message queue");
            if (_messageQueue != null)
            {
                var message = new ImportProcessingMessage
                {
                    ImportId = importFile.Id,
                    UserId = userId,
                    FileName = importDto.FileName,
                    FileType = importDto.FileType,
                    BlobUrl = blobUrl,
                    DetectedBankName = importFile.DetectedBankName,
                    DetectedCountry = importFile.DetectedCountry,
                    DetectedFormat = importFile.DetectedFormat,
                    Priority = 10 // High priority for imports
                };

                await _messageQueue.PublishMessageAsync("import-processing", message);
                _logger.LogInformation("[IMPORT-STEP-4-COMPLETE] Message published to Redis queue");
            }
            else
            {
                _logger.LogWarning("[IMPORT-STEP-4] Redis message queue not available, falling back to polling");
            }

            _logger.LogInformation("[IMPORT-COMPLETE] File uploaded successfully. Import {ImportId} queued for worker processing. Status: {Status}", 
                importFile.Id, importFile.Status);

            var estimatedTime = EstimateProcessingTime(importDto.FileData.Length, importDto.FileType);
            _logger.LogInformation("[IMPORT-RESULT] Returning success result. EstimatedProcessingTime: {EstimatedSeconds} seconds", estimatedTime);
            
            return new ImportResult
            {
                ImportId = importFile.Id,
                JobId = importFile.Id, // Use ImportId as JobId for tracking
                IsAsync = true,
                IsSuccessful = true,
                Message = "File uploaded successfully and queued for processing",
                EstimatedSeconds = estimatedTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IMPORT-ERROR] Error uploading file {FileName}. Error: {ErrorMessage}", 
                importDto.FileName, ex.Message);
            return new ImportResult
            {
                IsAsync = false,
                IsSuccessful = false,
                Message = $"Upload failed: {ex.Message}"
            };
        }
    }

    public async Task<ImportStatusDto?> GetImportStatusAsync(Guid userId, Guid importId)
    {
        _logger.LogDebug("[STATUS-CHECK] Fetching import status for ImportId: {ImportId}, UserId: {UserId}", importId, userId);
        
        var importFile = await _context.ImportedFiles
            .FirstOrDefaultAsync(f => f.Id == importId && f.UserId == userId);

        if (importFile == null)
        {
            _logger.LogWarning("[STATUS-CHECK] Import not found for ImportId: {ImportId}", importId);
            return null;
        }
        
        _logger.LogInformation("[STATUS-CHECK] Found import. Status: {Status}, ProcessedRows: {ProcessedRows}/{TotalRows}", 
            importFile.Status, importFile.ProcessedRows, importFile.TotalRows);

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
        if (importFile.Status == ImportStatus.Processing)
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
                ErrorDetails = f.ErrorDetails,
                DetectedBankName = f.DetectedBankName,
                DetectedFormat = f.DetectedFormat,
                AICost = f.AICost
            })
            .ToListAsync();

        return imports;
    }

    public async Task<ImportPreviewDto> GeneratePreviewAsync(byte[] fileData, string fileName)
    {
        // For now, return a simple preview - this could be enhanced later
        return new ImportPreviewDto
        {
            Headers = new List<string> { "Preview not available" },
            SampleRows = new List<Dictionary<string, string>>
            {
                new() { ["Message"] = "File will be processed by worker" }
            }
        };
    }

    private async Task<ImportedFile> CreateImportRecordAsync(Guid userId, FileImportDto importDto)
    {
        var importId = Guid.NewGuid();
        _logger.LogDebug("[CREATE-RECORD] Creating new import record with ID: {ImportId}", importId);
        
        var importFile = new ImportedFile
        {
            Id = importId,
            UserId = userId,
            FileName = importDto.FileName,
            FileType = importDto.FileType,
            FileSize = importDto.FileData.Length,
            Status = ImportStatus.Pending,
            BankTemplate = importDto.BankTemplate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _logger.LogDebug("[CREATE-RECORD] Adding import record to context. FileName: {FileName}, FileSize: {FileSize}, BankTemplate: {BankTemplate}", 
            importFile.FileName, importFile.FileSize, importFile.BankTemplate);
        
        _context.ImportedFiles.Add(importFile);
        await _context.SaveChangesAsync();

        _logger.LogDebug("[CREATE-RECORD] Import record saved successfully");
        return importFile;
    }

    private int EstimateProcessingTime(long fileSize, string fileType)
    {
        // Estimate processing time based on file size and type
        var baseTime = fileType.ToLowerInvariant() switch
        {
            ".csv" => 30, // 30 seconds for CSV
            ".pdf" => 120, // 2 minutes for PDF
            ".png" or ".jpg" or ".jpeg" => 90, // 1.5 minutes for images
            _ => 60 // 1 minute default
        };

        // Add time based on file size (1 second per 10KB)
        var sizeTime = (int)(fileSize / 10240);
        
        return Math.Min(baseTime + sizeTime, 300); // Cap at 5 minutes
    }
}
