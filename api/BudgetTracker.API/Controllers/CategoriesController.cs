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
public class CategoriesController : ControllerBase
{
    private readonly BudgetTrackerDbContext _context;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(BudgetTrackerDbContext context, ILogger<CategoriesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var categories = await _context.Categories
            .Where(c => c.UserId == userId.Value && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Type = c.Type.ToString(),
                Icon = c.Icon,
                Color = c.Color,
                ParentCategoryId = c.ParentCategoryId,
                BudgetAmount = c.BudgetAmount,
                IsSystem = c.IsSystem
            })
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategory(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var category = await _context.Categories
            .Where(c => c.Id == id && c.UserId == userId.Value)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Type = c.Type.ToString(),
                Icon = c.Icon,
                Color = c.Color,
                ParentCategoryId = c.ParentCategoryId,
                BudgetAmount = c.BudgetAmount,
                IsSystem = c.IsSystem
            })
            .FirstOrDefaultAsync();

        if (category == null) return NotFound();

        return Ok(category);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (!Enum.TryParse<CategoryType>(dto.Type, out var categoryType))
        {
            return BadRequest(new { error = "Invalid category type" });
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = dto.Name,
            Type = categoryType,
            Icon = dto.Icon,
            Color = dto.Color,
            ParentCategoryId = dto.ParentCategoryId,
            BudgetAmount = dto.BudgetAmount,
            IsSystem = false,
            IsActive = true,
            DisplayOrder = dto.DisplayOrder ?? 999,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Type = category.Type.ToString(),
            Icon = category.Icon,
            Color = category.Color,
            ParentCategoryId = category.ParentCategoryId,
            BudgetAmount = category.BudgetAmount,
            IsSystem = category.IsSystem
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CreateCategoryDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (!Enum.TryParse<CategoryType>(dto.Type, out var categoryType))
        {
            return BadRequest(new { error = "Invalid category type" });
        }

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);

        if (category == null) return NotFound();

        // Update category properties
        category.Name = dto.Name;
        category.Type = categoryType;
        category.Icon = dto.Icon;
        category.Color = dto.Color;
        category.ParentCategoryId = dto.ParentCategoryId;
        category.BudgetAmount = dto.BudgetAmount;
        category.DisplayOrder = dto.DisplayOrder ?? category.DisplayOrder;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Type = category.Type.ToString(),
            Icon = category.Icon,
            Color = category.Color,
            ParentCategoryId = category.ParentCategoryId,
            BudgetAmount = category.BudgetAmount,
            IsSystem = category.IsSystem
        });
    }

    [HttpPost("seed-defaults")]
    public async Task<IActionResult> SeedDefaultCategories()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // Check if user already has categories
        var hasCategories = await _context.Categories.AnyAsync(c => c.UserId == userId.Value);
        if (hasCategories)
        {
            return BadRequest(new { error = "User already has categories" });
        }

        await SeedDefaultCategoriesForUser(userId.Value);
        return Ok(new { message = $"Created {GetDefaultCategories().Count} default categories" });
    }

    [HttpPost("seed-defaults/{userId}")]
    public async Task<IActionResult> SeedDefaultCategoriesForUserEndpoint(Guid userId)
    {
        await SeedDefaultCategoriesForUser(userId);
        return Ok(new { message = $"Created {GetDefaultCategories().Count} default categories" });
    }

    private async Task SeedDefaultCategoriesForUser(Guid userId)
    {
        var defaultCategories = GetDefaultCategories().Select(c => new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = c.Name,
            Type = c.Type,
            Icon = c.Icon,
            Color = c.Color,
            DisplayOrder = c.DisplayOrder,
            IsSystem = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        _context.Categories.AddRange(defaultCategories);
        await _context.SaveChangesAsync();
    }

    private List<(string Name, CategoryType Type, string Icon, string Color, int DisplayOrder)> GetDefaultCategories()
    {
        return new List<(string Name, CategoryType Type, string Icon, string Color, int DisplayOrder)>
        {
            // Income categories
            ("Salary", CategoryType.Income, "💰", "#10b981", 1),
            ("Freelance", CategoryType.Income, "💼", "#10b981", 2),
            ("Investments", CategoryType.Income, "📈", "#10b981", 3),
            ("Business", CategoryType.Income, "🏢", "#10b981", 4),
            ("Other Income", CategoryType.Income, "💵", "#10b981", 5),
            
            // Essential Expenses
            ("Rent/Mortgage", CategoryType.Expense, "🏠", "#f43f5e", 10),
            ("Groceries", CategoryType.Expense, "🛒", "#f97316", 11),
            ("Utilities", CategoryType.Expense, "⚡", "#3b82f6", 12),
            ("Transportation", CategoryType.Expense, "🚗", "#eab308", 13),
            ("Insurance", CategoryType.Expense, "🛡️", "#6366f1", 14),
            
            // Food & Dining
            ("Restaurants", CategoryType.Expense, "🍔", "#ef4444", 20),
            ("Coffee", CategoryType.Expense, "☕", "#8b4513", 21),
            ("Fast Food", CategoryType.Expense, "🍟", "#ff6b35", 22),
            
            // Shopping & Entertainment
            ("Shopping", CategoryType.Expense, "🛍️", "#a855f7", 30),
            ("Entertainment", CategoryType.Expense, "🎬", "#8b5cf6", 31),
            ("Subscriptions", CategoryType.Expense, "📱", "#06b6d4", 32),
            
            // Health & Personal
            ("Healthcare", CategoryType.Expense, "🏥", "#06b6d4", 40),
            ("Fitness", CategoryType.Expense, "💪", "#14b8a6", 41),
            ("Personal Care", CategoryType.Expense, "💅", "#fb923c", 42),
            
            // Education & Travel
            ("Education", CategoryType.Expense, "📚", "#14b8a6", 50),
            ("Travel", CategoryType.Expense, "✈️", "#ec4899", 51),
            
            // Other
            ("Gas", CategoryType.Expense, "⛽", "#f59e0b", 60),
            ("ATM/Cash", CategoryType.Expense, "💵", "#6b7280", 61),
            ("Other", CategoryType.Expense, "📝", "#9ca3af", 62),
            
            // Transfer category
            ("Transfer", CategoryType.Transfer, "↔️", "#6b7280", 70)
        };
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId.Value);

        if (category == null) return NotFound();

        // Check if category is being used by any transactions
        var hasTransactions = await _context.Transactions
            .AnyAsync(t => t.CategoryId == id);

        if (hasTransactions)
        {
            // Soft delete - just mark as inactive
            category.IsActive = false;
            category.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Hard delete if no transactions use it
            _context.Categories.Remove(category);
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Category deleted successfully" });
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

