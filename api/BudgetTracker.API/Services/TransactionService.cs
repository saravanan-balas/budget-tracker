using Microsoft.EntityFrameworkCore;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Models;
using AutoMapper;

namespace BudgetTracker.API.Services;

public class TransactionService : ITransactionService
{
    private readonly BudgetTrackerDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        BudgetTrackerDbContext context,
        IMapper mapper,
        ILogger<TransactionService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IEnumerable<TransactionDto>> GetTransactionsAsync(Guid userId, TransactionFilterDto filter)
    {
        var query = _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => t.UserId == userId);

        if (filter.AccountId.HasValue)
            query = query.Where(t => t.AccountId == filter.AccountId.Value);

        if (filter.CategoryId.HasValue)
            query = query.Where(t => t.CategoryId == filter.CategoryId.Value);

        if (filter.StartDate.HasValue)
        {
            var startDateUtc = filter.StartDate.Value.Kind == DateTimeKind.Utc 
                ? filter.StartDate.Value 
                : DateTime.SpecifyKind(filter.StartDate.Value, DateTimeKind.Utc);
            query = query.Where(t => t.TransactionDate >= startDateUtc);
        }

        if (filter.EndDate.HasValue)
        {
            var endDateUtc = filter.EndDate.Value.Kind == DateTimeKind.Utc 
                ? filter.EndDate.Value 
                : DateTime.SpecifyKind(filter.EndDate.Value.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
            query = query.Where(t => t.TransactionDate <= endDateUtc);
        }

        if (filter.MinAmount.HasValue)
            query = query.Where(t => Math.Abs(t.Amount) >= filter.MinAmount.Value);

        if (filter.MaxAmount.HasValue)
            query = query.Where(t => Math.Abs(t.Amount) <= filter.MaxAmount.Value);

        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(t => 
                t.OriginalMerchant.ToLower().Contains(searchTerm) ||
                (t.Description != null && t.Description.ToLower().Contains(searchTerm)) ||
                (t.Notes != null && t.Notes.ToLower().Contains(searchTerm))
            );
        }

        if (filter.IsRecurring.HasValue)
            query = query.Where(t => t.IsRecurring == filter.IsRecurring.Value);

        if (filter.IsPending.HasValue)
            query = query.Where(t => t.IsPending == filter.IsPending.Value);

        var skip = (filter.Page - 1) * filter.PageSize;
        
        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip(skip)
            .Take(filter.PageSize)
            .ToListAsync();

        return _mapper.Map<IEnumerable<TransactionDto>>(transactions);
    }

    public async Task<TransactionDto?> GetTransactionByIdAsync(Guid userId, Guid transactionId)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId);

        return transaction != null ? _mapper.Map<TransactionDto>(transaction) : null;
    }

    public async Task<TransactionDto> CreateTransactionAsync(Guid userId, CreateTransactionDto createDto)
    {
        // Ensure the account exists and belongs to the user
        var accountId = createDto.AccountId;
        var accountExists = await _context.Accounts
            .AnyAsync(a => a.Id == accountId && a.UserId == userId);
            
        if (!accountExists)
        {
            // Create or find a default account for the user
            var defaultAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Type == AccountType.Cash);
                
            if (defaultAccount == null)
            {
                // Create a default Cash account
                defaultAccount = new Account
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = "Default Account",
                    Type = AccountType.Cash,
                    Currency = "USD",
                    Balance = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                _context.Accounts.Add(defaultAccount);
                await _context.SaveChangesAsync();
            }
            
            accountId = defaultAccount.Id;
        }

        var transactionDateUtc = createDto.TransactionDate.Kind == DateTimeKind.Utc
            ? createDto.TransactionDate
            : DateTime.SpecifyKind(createDto.TransactionDate, DateTimeKind.Utc);

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = accountId,
            TransactionDate = transactionDateUtc,
            PostedDate = transactionDateUtc,
            Amount = createDto.Amount,
            Type = createDto.Amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            OriginalMerchant = createDto.Merchant,
            CategoryId = createDto.CategoryId,
            Description = createDto.Description,
            Notes = createDto.Notes,
            Tags = createDto.Tags,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return await GetTransactionByIdAsync(userId, transaction.Id) ?? throw new InvalidOperationException();
    }

    public async Task<TransactionDto?> UpdateTransactionAsync(Guid userId, Guid transactionId, UpdateTransactionDto updateDto)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId);

        if (transaction == null)
            return null;

        if (updateDto.CategoryId.HasValue)
            transaction.CategoryId = updateDto.CategoryId;

        if (!string.IsNullOrEmpty(updateDto.Notes))
            transaction.Notes = updateDto.Notes;

        if (!string.IsNullOrEmpty(updateDto.Tags))
            transaction.Tags = updateDto.Tags;

        if (!string.IsNullOrEmpty(updateDto.Merchant))
            transaction.OriginalMerchant = updateDto.Merchant;

        transaction.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetTransactionByIdAsync(userId, transactionId);
    }

    public async Task<bool> DeleteTransactionAsync(Guid userId, Guid transactionId)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId);

        if (transaction == null)
            return false;

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<TransactionDto>?> SplitTransactionAsync(Guid userId, Guid transactionId, SplitTransactionDto splitDto)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId);

        if (transaction == null)
            return null;

        var totalSplitAmount = splitDto.Splits.Sum(s => s.Amount);
        if (Math.Abs(totalSplitAmount - transaction.Amount) > 0.01m)
        {
            throw new ArgumentException("Split amounts must equal the original transaction amount");
        }

        transaction.IsSplit = true;
        var splitTransactions = new List<Transaction>();

        foreach (var split in splitDto.Splits)
        {
            var splitTransaction = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccountId = transaction.AccountId,
                TransactionDate = transaction.TransactionDate,
                PostedDate = transaction.PostedDate,
                Amount = split.Amount,
                Type = split.Amount < 0 ? TransactionType.Debit : TransactionType.Credit,
                OriginalMerchant = transaction.OriginalMerchant,
                CategoryId = split.CategoryId ?? transaction.CategoryId,
                Description = split.Description ?? transaction.Description,
                ParentTransactionId = transaction.Id,
                IsSplit = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            splitTransactions.Add(splitTransaction);
        }

        _context.Transactions.AddRange(splitTransactions);
        await _context.SaveChangesAsync();

        return _mapper.Map<IEnumerable<TransactionDto>>(splitTransactions);
    }
}