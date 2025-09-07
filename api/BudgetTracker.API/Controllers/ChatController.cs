using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BudgetTracker.Common.DTOs;
using BudgetTracker.API.Services;

namespace BudgetTracker.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequestDto request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var response = await _chatService.ProcessChatAsync(userId, request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return Ok(new ChatResponseDto
            {
                Type = ChatResponseType.Error,
                Message = "I'm sorry, I encountered an error processing your request. Please try again."
            });
        }
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions()
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var suggestions = await _chatService.GetSuggestionsAsync(userId);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching suggestions");
            return StatusCode(500, new { error = "An error occurred while fetching suggestions" });
        }
    }
}