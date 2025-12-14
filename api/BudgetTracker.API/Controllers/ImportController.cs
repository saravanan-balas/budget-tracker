using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BudgetTracker.Common.DTOs;
using BudgetTracker.API.Services;
using BudgetTracker.Common.Services.Templates;

namespace BudgetTracker.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly ISimplifiedImportService _simplifiedImportService;
    private readonly ISynchronousImportService _synchronousImportService;
    private readonly IBankTemplateService _templateService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        ISimplifiedImportService simplifiedImportService,
        ISynchronousImportService synchronousImportService,
        IBankTemplateService templateService,
        ILogger<ImportController> logger)
    {
        _simplifiedImportService = simplifiedImportService;
        _synchronousImportService = synchronousImportService;
        _templateService = templateService;
        _logger = logger;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewImport([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "File is required" });
            }

            if (!IsCsvFile(file.FileName))
            {
                return BadRequest(new { error = "Only CSV files are currently supported. Please upload a CSV export." });
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var fileData = stream.ToArray();

            var preview = await _simplifiedImportService.GeneratePreviewAsync(fileData, file.FileName);
            return Ok(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing import");
            return StatusCode(500, new { error = "An error occurred while previewing the import" });
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> ImportFile([FromForm] IFormFile file, [FromForm] Guid accountId, [FromForm] string? bankTemplate)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "File is required" });
            }

            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());

            if (!IsCsvFile(file.FileName))
            {
                return BadRequest(new { error = "Only CSV files are currently supported. Please upload a CSV export." });
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            var importDto = new FileImportDto
            {
                AccountId = accountId,
                FileName = file.FileName,
                FileType = Path.GetExtension(file.FileName),
                FileData = stream.ToArray(),
                BankTemplate = bankTemplate
            };

            _logger.LogInformation("Processing CSV file synchronously: {FileName}", file.FileName);
            var result = await _synchronousImportService.ProcessCsvAsync(userId, importDto);
            return result.IsSuccessful ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing file");
            return StatusCode(500, new { error = "An error occurred while importing the file" });
        }
    }

    [HttpGet("status/{importId}")]
    public async Task<IActionResult> GetImportStatus(Guid importId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var status = await _simplifiedImportService.GetImportStatusAsync(userId, importId);
            
            if (status == null)
            {
                return NotFound(new { error = "Import not found" });
            }
            
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching import status");
            return StatusCode(500, new { error = "An error occurred while fetching import status" });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetImportHistory()
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var history = await _simplifiedImportService.GetImportHistoryAsync(userId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching import history");
            return StatusCode(500, new { error = "An error occurred while fetching import history" });
        }
    }

    // Smart Import Endpoint - Used by frontend
    [HttpPost("smart")]
    public async Task<IActionResult> SmartImport([FromForm] IFormFile file, [FromForm] Guid accountId)
    {
        return await UploadUnified(file, accountId);
    }

    // Unified Import Endpoint - CSV processed synchronously, others return error
    [HttpPost("upload-unified")]
    public async Task<IActionResult> UploadUnified([FromForm] IFormFile file, [FromForm] Guid accountId)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "File is required" });
            }

            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());

            if (!IsCsvFile(file.FileName))
            {
                return BadRequest(new { error = "Only CSV files are currently supported. Please upload a CSV export." });
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            var importDto = new FileImportDto
            {
                AccountId = accountId,
                FileName = file.FileName,
                FileType = Path.GetExtension(file.FileName),
                FileData = stream.ToArray()
            };

            _logger.LogInformation("Processing CSV file synchronously: {FileName}", file.FileName);
            var result = await _synchronousImportService.ProcessCsvAsync(userId, importDto);
            return result.IsSuccessful ? Ok(result) : BadRequest(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in unified upload");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in unified upload");
            return StatusCode(500, new { error = "An error occurred during file upload" });
        }
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeFile([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "File is required" });
            }

            if (!IsCsvFile(file.FileName))
            {
                return BadRequest(new { error = "Only CSV files are currently supported. Please upload a CSV export." });
            }

            // Read file content to count actual rows
            byte[] fileData;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileData = ms.ToArray();
            }

            // Count actual rows (excluding header)
            var estimatedRowCount = EstimateCsvRowCount(fileData);

            return Ok(new
            {
                fileFormat = Path.GetExtension(file.FileName).ToLowerInvariant(),
                fileSize = file.Length,
                canProcessSynchronously = true,
                asyncReason = string.Empty,
                estimatedSeconds = EstimateProcessingTime(file.Length, Path.GetExtension(file.FileName)),
                hasKnownTemplate = false,
                estimatedCost = EstimateCostByFileType(Path.GetExtension(file.FileName), (int)file.Length),
                estimatedRowCount = estimatedRowCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing file");
            return StatusCode(500, new { error = "An error occurred while analyzing the file" });
        }
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile image, [FromForm] Guid accountId)
    {
        // Image processing is not supported
        return BadRequest(new { error = "Only CSV files are currently supported. Please upload a CSV export." });
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetKnownBanks()
    {
        try
        {
            var templates = await _templateService.GetKnownBanksAsync();
            
            var result = templates.Select(t => new
            {
                id = t.Id,
                bankName = t.BankName,
                country = t.Country,
                fileFormat = t.FileFormat,
                confidenceScore = t.ConfidenceScore,
                successCount = t.SuccessCount,
                lastUsed = t.LastUsed
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching known banks");
            return StatusCode(500, new { error = "An error occurred while fetching known banks" });
        }
    }

    [HttpGet("cost-estimate")]
    public async Task<IActionResult> EstimateCost([FromQuery] int fileSizeBytes, [FromQuery] string fileType)
    {
        try
        {
            // Simple cost estimation - all processing is now done in worker
            var estimatedCost = EstimateCostByFileType(fileType, fileSizeBytes);
            
            return Ok(new
            {
                estimatedCost = estimatedCost,
                currency = "USD",
                fileSize = fileSizeBytes,
                fileType = fileType
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error estimating cost, returning default");
            return Ok(new
            {
                estimatedCost = 0.05m,
                currency = "USD",
                fileSize = fileSizeBytes,
                fileType = fileType
            });
        }
    }

    private static bool IsCsvFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private int EstimateProcessingTime(long fileSize, string fileType)
    {
        // Estimate processing time based on file size and type
        var baseTime = fileType.ToLowerInvariant() switch
        {
            ".csv" or ".txt" => 30,
            _ => 30
        };

        // Add time based on file size (1 second per 10KB)
        var sizeTime = (int)(fileSize / 10240);
        
        return Math.Min(baseTime + sizeTime, 300); // Cap at 5 minutes
    }

    private int EstimateRowCount(long fileSize)
    {
        // Rough estimate: 1KB per transaction row (fallback method)
        return Math.Max(1, (int)(fileSize / 1024));
    }

    private int EstimateCsvRowCount(byte[] fileData)
    {
        try
        {
            var text = System.Text.Encoding.UTF8.GetString(fileData);
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            // Subtract 1 for header row, ensure minimum of 0
            return Math.Max(0, lines.Length - 1);
        }
        catch
        {
            // Fallback to size-based estimate
            return Math.Max(1, (int)(fileData.Length / 1024));
        }
    }

    private decimal EstimateCostByFileType(string fileType, int fileSizeBytes)
    {
        // Simple cost estimation based on file type and size
        var baseCost = fileType.ToLowerInvariant() switch
        {
            ".csv" or ".txt" => 0.001m,
            _ => 0.001m
        };

        // Add cost based on file size
        var sizeCost = fileSizeBytes / 1024 * 0.0001m; // $0.0001 per KB
        
        return Math.Min(baseCost + sizeCost, 0.1m); // Cap at $0.10
    }
}