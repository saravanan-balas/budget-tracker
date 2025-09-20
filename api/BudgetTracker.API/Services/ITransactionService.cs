using BudgetTracker.Common.DTOs;

namespace BudgetTracker.API.Services;

public interface ITransactionService
{
    Task<IEnumerable<TransactionDto>> GetTransactionsAsync(Guid userId, TransactionFilterDto filter);
    Task<TransactionDto?> GetTransactionByIdAsync(Guid userId, Guid transactionId);
    Task<TransactionDto> CreateTransactionAsync(Guid userId, CreateTransactionDto createDto);
    Task<TransactionDto?> UpdateTransactionAsync(Guid userId, Guid transactionId, UpdateTransactionDto updateDto);
    Task<bool> DeleteTransactionAsync(Guid userId, Guid transactionId);
    Task<IEnumerable<TransactionDto>?> SplitTransactionAsync(Guid userId, Guid transactionId, SplitTransactionDto splitDto);
    Task<IEnumerable<TransactionDto>> GetTransactionsByImportIdAsync(Guid userId, Guid importId);
}