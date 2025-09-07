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

        var defaultCategories = new List<Category>
        {
            // Income categories
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Salary", Type = CategoryType.Income, Icon = "💰", Color = "#10b981", DisplayOrder = 1 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Freelance", Type = CategoryType.Income, Icon = "💼", Color = "#10b981", DisplayOrder = 2 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Investments", Type = CategoryType.Income, Icon = "📈", Color = "#10b981", DisplayOrder = 3 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Other Income", Type = CategoryType.Income, Icon = "💵", Color = "#10b981", DisplayOrder = 4 },
            
            // Expense categories
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Food & Dining", Type = CategoryType.Expense, Icon = "🍔", Color = "#ef4444", DisplayOrder = 10 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Groceries", Type = CategoryType.Expense, Icon = "🛒", Color = "#f97316", DisplayOrder = 11 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Transportation", Type = CategoryType.Expense, Icon = "🚗", Color = "#eab308", DisplayOrder = 12 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Shopping", Type = CategoryType.Expense, Icon = "🛍️", Color = "#a855f7", DisplayOrder = 13 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Entertainment", Type = CategoryType.Expense, Icon = "🎬", Color = "#8b5cf6", DisplayOrder = 14 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Bills & Utilities", Type = CategoryType.Expense, Icon = "📱", Color = "#3b82f6", DisplayOrder = 15 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Healthcare", Type = CategoryType.Expense, Icon = "🏥", Color = "#06b6d4", DisplayOrder = 16 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Education", Type = CategoryType.Expense, Icon = "📚", Color = "#14b8a6", DisplayOrder = 17 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Travel", Type = CategoryType.Expense, Icon = "✈️", Color = "#ec4899", DisplayOrder = 18 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Insurance", Type = CategoryType.Expense, Icon = "🛡️", Color = "#6366f1", DisplayOrder = 19 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Rent/Mortgage", Type = CategoryType.Expense, Icon = "🏠", Color = "#f43f5e", DisplayOrder = 20 },
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Personal Care", Type = CategoryType.Expense, Icon = "💅", Color = "#fb923c", DisplayOrder = 21 },
            
            // Transfer category
            new Category { Id = Guid.NewGuid(), UserId = userId.Value, Name = "Transfer", Type = CategoryType.Transfer, Icon = "↔️", Color = "#6b7280", DisplayOrder = 30 }
        };

        foreach (var category in defaultCategories)
        {
            category.IsSystem = false;
            category.IsActive = true;
            category.CreatedAt = DateTime.UtcNow;
            category.UpdatedAt = DateTime.UtcNow;
        }

        _context.Categories.AddRange(defaultCategories);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Created {defaultCategories.Count} default categories" });
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

