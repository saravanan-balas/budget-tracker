using BudgetTracker.Common.DTOs;

namespace BudgetTracker.API.Services;

public interface IChatService
{
    Task<ChatResponseDto> ProcessChatAsync(Guid userId, ChatRequestDto request);
    Task<IEnumerable<string>> GetSuggestionsAsync(Guid userId);
}