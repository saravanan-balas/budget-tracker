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
    private readonly IImportService _importService;
    private readonly ISmartImportService _smartImportService;
    private readonly IBankTemplateService _templateService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        IImportService importService,
        ISmartImportService smartImportService,
        IBankTemplateService templateService,
        ILogger<ImportController> logger)
    {
        _importService = importService;
        _smartImportService = smartImportService;
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

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var fileData = stream.ToArray();

            var preview = await _importService.PreviewImportAsync(file.FileName, fileData);
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

            var importId = await _importService.StartImportAsync(userId, importDto);
            
            return Ok(new { importId, message = "Import started successfully" });
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
            var status = await _importService.GetImportStatusAsync(userId, importId);
            
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
            var history = await _importService.GetImportHistoryAsync(userId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching import history");
            return StatusCode(500, new { error = "An error occurred while fetching import history" });
        }
    }

    // New Universal Bank Import Endpoints

    [HttpPost("smart")]
    public async Task<IActionResult> SmartImport([FromForm] IFormFile file, [FromForm] Guid accountId)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "File is required" });
            }

            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            var importDto = new FileImportDto
            {
                AccountId = accountId,
                FileName = file.FileName,
                FileType = Path.GetExtension(file.FileName),
                FileData = stream.ToArray()
            };

            var result = await _smartImportService.ProcessSmartImportAsync(userId, importDto);
            
            if (result.IsAsync)
            {
                return Accepted(new 
                { 
                    jobId = result.JobId,
                    importId = result.ImportId,
                    message = result.Message,
                    estimatedSeconds = result.EstimatedSeconds
                });
            }
            else
            {
                return result.IsSuccessful ? Ok(result) : BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in smart import");
            return StatusCode(500, new { error = "An error occurred during smart import" });
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

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var fileData = stream.ToArray();

            var analysis = await _smartImportService.AnalyzeImportFileAsync(fileData, file.FileName);
            var costEstimate = await _smartImportService.EstimateProcessingCostAsync(fileData, file.FileName);

            return Ok(new
            {
                fileFormat = analysis.FileFormat,
                fileSize = analysis.FileSize,
                canProcessSynchronously = analysis.CanProcessSynchronously,
                asyncReason = analysis.AsyncReason,
                estimatedSeconds = analysis.EstimatedSeconds,
                hasKnownTemplate = analysis.HasKnownTemplate,
                estimatedCost = costEstimate,
                estimatedRowCount = analysis.EstimatedRowCount
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
        try
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest(new { error = "Image file is required" });
            }

            var allowedTypes = new[] { "image/png", "image/jpeg", "image/jpg" };
            if (!allowedTypes.Contains(image.ContentType.ToLowerInvariant()))
            {
                return BadRequest(new { error = "Only PNG and JPEG images are supported" });
            }

            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());

            using var stream = new MemoryStream();
            await image.CopyToAsync(stream);

            var importDto = new FileImportDto
            {
                AccountId = accountId,
                FileName = image.FileName,
                FileType = Path.GetExtension(image.FileName),
                FileData = stream.ToArray()
            };

            var result = await _smartImportService.ProcessSmartImportAsync(userId, importDto);
            
            // Images are always processed asynchronously due to OCR requirements
            return Accepted(new 
            { 
                jobId = result.JobId,
                importId = result.ImportId,
                message = result.Message,
                estimatedSeconds = result.EstimatedSeconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return StatusCode(500, new { error = "An error occurred while uploading the image" });
        }
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
            // Create dummy file data for cost estimation
            var dummyData = new byte[fileSizeBytes];
            var fileName = $"dummy.{fileType}";
            
            var cost = await _smartImportService.EstimateProcessingCostAsync(dummyData, fileName);
            
            return Ok(new
            {
                estimatedCost = cost,
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
}