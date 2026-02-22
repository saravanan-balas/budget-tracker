using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.Services.Categories;

namespace BudgetTracker.Categorization.Tests;

public class CategorizationTestFixture : IAsyncLifetime
{
    public BudgetTrackerDbContext DbContext { get; private set; } = null!;
    public OptimizedCategoryAssignmentService CategoryService { get; private set; } = null!;
    public Guid TestUserId { get; private set; }
    public TestCaseData TestData { get; private set; } = null!;

    private static readonly string[] StandardCategories =
    [
        "Food & Dining", "Groceries", "Transportation", "Shopping", "Entertainment",
        "Bills & Utilities", "Healthcare", "Education", "Travel", "Insurance",
        "Personal Care", "Income", "Transfer", "Charity", "Donations",
        "Uncategorized", "Pet Care", "Legal Services", "Child Care"
    ];

    public async Task InitializeAsync()
    {
        TestUserId = Guid.NewGuid();

        // In-memory database (use subclass that ignores pgvector types)
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase($"CategorizeTests_{Guid.NewGuid()}")
            .Options;
        DbContext = new TestBudgetTrackerDbContext(options);

        // Seed standard categories
        foreach (var name in StandardCategories)
        {
            DbContext.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                UserId = TestUserId,
                Name = name,
                Type = name == "Income" ? CategoryType.Income : CategoryType.Expense,
                IsSystem = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await DbContext.SaveChangesAsync();

        // Create service with no AI (to test local-only categorization)
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<OptimizedCategoryAssignmentService>.Instance;

        // Service provider with no AI analyzer (forces local categorization only)
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache>(memoryCache);
        var serviceProvider = services.BuildServiceProvider();

        CategoryService = new OptimizedCategoryAssignmentService(
            DbContext, memoryCache, logger, serviceProvider);

        // Load test fixtures
        TestData = LoadTestCases();
    }

    public Task DisposeAsync()
    {
        DbContext?.Dispose();
        return Task.CompletedTask;
    }

    private static TestCaseData LoadTestCases()
    {
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var filePath = Path.Combine(fixturesDir, "categorization-test-cases.json");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Test fixture not found: {filePath}");

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<TestCaseData>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new TestCaseData();
    }

    public Guid? GetCategoryId(string categoryName)
    {
        return DbContext.Categories
            .FirstOrDefault(c => c.UserId == TestUserId && c.Name == categoryName)?.Id;
    }

    public string? GetCategoryName(Guid? categoryId)
    {
        if (!categoryId.HasValue) return null;
        return DbContext.Categories.FirstOrDefault(c => c.Id == categoryId.Value)?.Name;
    }
}

public class TestCaseData
{
    [JsonPropertyName("testCases")]
    public List<TestCase> TestCases { get; set; } = new();

    [JsonPropertyName("falsePositiveTests")]
    public List<FalsePositiveTest> FalsePositiveTests { get; set; } = new();
}

public class TestCase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("merchant")]
    public string Merchant { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("expectedCategory")]
    public string ExpectedCategory { get; set; } = "";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

public class FalsePositiveTest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("merchant")]
    public string Merchant { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("mustNotBeCategory")]
    public string MustNotBeCategory { get; set; } = "";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Test-only DbContext subclass that ignores pgvector Vector properties
/// which are not supported by the InMemory provider.
/// </summary>
internal class TestBudgetTrackerDbContext : BudgetTrackerDbContext
{
    public TestBudgetTrackerDbContext(DbContextOptions<BudgetTrackerDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore Vector properties that InMemory provider doesn't support
        modelBuilder.Entity<Merchant>(entity =>
        {
            entity.Ignore(e => e.Embedding);
        });
    }
}
