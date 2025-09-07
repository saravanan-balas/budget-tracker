using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BudgetTracker.Common.DTOs;
using BudgetTracker.API.Services;

namespace BudgetTracker.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(IImportService importService, ILogger<ImportController> logger)
    {
        _importService = importService;
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
}