using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.DTOs;
using BudgetTracker.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly BudgetTrackerDbContext _context;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(BudgetTrackerDbContext context, ILogger<AccountsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAccounts()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var accounts = await _context.Accounts
            .Where(a => a.UserId == userId.Value && a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new AccountDto
            {
                Id = a.Id,
                Name = a.Name,
                Type = a.Type.ToString(),
                Balance = a.Balance,
                Currency = a.Currency,
                Institution = a.Institution,
                AccountNumber = a.AccountNumber,
                IsActive = a.IsActive
            })
            .ToListAsync();

        return Ok(accounts);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var account = await _context.Accounts
            .Where(a => a.Id == id && a.UserId == userId.Value)
            .Select(a => new AccountDto
            {
                Id = a.Id,
                Name = a.Name,
                Type = a.Type.ToString(),
                Balance = a.Balance,
                Currency = a.Currency,
                Institution = a.Institution,
                AccountNumber = a.AccountNumber,
                IsActive = a.IsActive
            })
            .FirstOrDefaultAsync();

        if (account == null) return NotFound();

        return Ok(account);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (!Enum.TryParse<AccountType>(dto.Type, out var accountType))
        {
            return BadRequest(new { error = "Invalid account type" });
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = dto.Name,
            Type = accountType,
            Balance = dto.InitialBalance ?? 0,
            Currency = dto.Currency ?? "USD",
            Institution = dto.Institution,
            AccountNumber = dto.AccountNumber,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, new AccountDto
        {
            Id = account.Id,
            Name = account.Name,
            Type = account.Type.ToString(),
            Balance = account.Balance,
            Currency = account.Currency,
            Institution = account.Institution,
            AccountNumber = account.AccountNumber,
            IsActive = account.IsActive
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateAccountDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value);

        if (account == null) return NotFound();

        if (!string.IsNullOrEmpty(dto.Name))
            account.Name = dto.Name;
        
        if (!string.IsNullOrEmpty(dto.Institution))
            account.Institution = dto.Institution;
        
        if (!string.IsNullOrEmpty(dto.AccountNumber))
            account.AccountNumber = dto.AccountNumber;
        
        if (dto.Balance.HasValue)
            account.Balance = dto.Balance.Value;

        account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new AccountDto
        {
            Id = account.Id,
            Name = account.Name,
            Type = account.Type.ToString(),
            Balance = account.Balance,
            Currency = account.Currency,
            Institution = account.Institution,
            AccountNumber = account.AccountNumber,
            IsActive = account.IsActive
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value);

        if (account == null) return NotFound();

        // Check if there are transactions associated with this account
        var hasTransactions = await _context.Transactions
            .AnyAsync(t => t.AccountId == id);

        if (hasTransactions)
        {
            // Soft delete - just mark as inactive
            account.IsActive = false;
            account.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Hard delete if no transactions
            _context.Accounts.Remove(account);
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }
}

