using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BudgetTracker.Common.DTOs;
using BudgetTracker.API.Services;

namespace BudgetTracker.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(ITransactionService transactionService, ILogger<TransactionsController> logger)
    {
        _transactionService = transactionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions([FromQuery] TransactionFilterDto filter)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var transactions = await _transactionService.GetTransactionsAsync(userId, filter);
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transactions");
            return StatusCode(500, new { error = "An error occurred while fetching transactions" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTransaction(Guid id)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var transaction = await _transactionService.GetTransactionByIdAsync(userId, id);
            
            if (transaction == null)
            {
                return NotFound(new { error = "Transaction not found" });
            }
            
            return Ok(transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transaction");
            return StatusCode(500, new { error = "An error occurred while fetching the transaction" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionDto createDto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var transaction = await _transactionService.CreateTransactionAsync(userId, createDto);
            return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transaction");
            return StatusCode(500, new { error = "An error occurred while creating the transaction" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTransaction(Guid id, [FromBody] UpdateTransactionDto updateDto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var transaction = await _transactionService.UpdateTransactionAsync(userId, id, updateDto);
            
            if (transaction == null)
            {
                return NotFound(new { error = "Transaction not found" });
            }
            
            return Ok(transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating transaction");
            return StatusCode(500, new { error = "An error occurred while updating the transaction" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var result = await _transactionService.DeleteTransactionAsync(userId, id);
            
            if (!result)
            {
                return NotFound(new { error = "Transaction not found" });
            }
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting transaction");
            return StatusCode(500, new { error = "An error occurred while deleting the transaction" });
        }
    }

    [HttpPost("{id}/split")]
    public async Task<IActionResult> SplitTransaction(Guid id, [FromBody] SplitTransactionDto splitDto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var transactions = await _transactionService.SplitTransactionAsync(userId, id, splitDto);
            
            if (transactions == null)
            {
                return NotFound(new { error = "Transaction not found" });
            }
            
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting transaction");
            return StatusCode(500, new { error = "An error occurred while splitting the transaction" });
        }
    }

    [HttpGet("import/{importId}")]
    public async Task<IActionResult> GetTransactionsByImportId(Guid importId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("UserId")?.Value ?? throw new InvalidOperationException());
            var transactions = await _transactionService.GetTransactionsByImportIdAsync(userId, importId);
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transactions for import {ImportId}", importId);
            return StatusCode(500, new { error = "An error occurred while fetching transactions" });
        }
    }
}