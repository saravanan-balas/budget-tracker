using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;
using Bogus;
using BCrypt.Net;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((context, config) =>
{
    config.AddJsonFile("appsettings.json", optional: false);
    config.AddEnvironmentVariables();
});

builder.ConfigureServices((context, services) =>
{
    var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
    
    services.AddDbContext<BudgetTrackerDbContext>(options =>
        options.UseNpgsql(connectionString, o => o.UseVector()));
    
    services.AddScoped<TestDataGenerator>();
});

var host = builder.Build();

using var scope = host.Services.CreateScope();
var generator = scope.ServiceProvider.GetRequiredService<TestDataGenerator>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Starting test data generation...");
    await generator.GenerateTestDataAsync();
    logger.LogInformation("Test data generation completed successfully!");
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred while generating test data");
    return 1;
}

return 0;

public class TestDataGenerator
{
    private readonly BudgetTrackerDbContext _context;
    private readonly ILogger<TestDataGenerator> _logger;
    private readonly Random _random = new();

    public TestDataGenerator(BudgetTrackerDbContext context, ILogger<TestDataGenerator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task GenerateTestDataAsync()
    {
        // Check if test user already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        if (existingUser != null)
        {
            _logger.LogInformation("Test user already exists. Deleting existing data...");
            await CleanupExistingDataAsync(existingUser.Id);
        }

        // Create test user
        var user = await CreateTestUserAsync();
        
        // Create sample accounts
        var accounts = await CreateSampleAccountsAsync(user.Id);
        
        // Create sample categories
        var categories = await CreateSampleCategoriesAsync(user.Id);
        
        // Generate transactions for the past year
        await GenerateTransactionsAsync(user.Id, accounts, categories);
        
        _logger.LogInformation($"Generated test data for user: {user.Email}");
        _logger.LogInformation($"Created {accounts.Count} accounts");
        _logger.LogInformation($"Created {categories.Count} categories");
        
        var transactionCount = await _context.Transactions.CountAsync(t => t.UserId == user.Id);
        _logger.LogInformation($"Generated {transactionCount} transactions");
    }

    private async Task CleanupExistingDataAsync(Guid userId)
    {
        var transactions = await _context.Transactions.Where(t => t.UserId == userId).ToListAsync();
        var accounts = await _context.Accounts.Where(a => a.UserId == userId).ToListAsync();
        var categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync();
        var user = await _context.Users.FirstAsync(u => u.Id == userId);

        _context.Transactions.RemoveRange(transactions);
        _context.Accounts.RemoveRange(accounts);
        _context.Categories.RemoveRange(categories);
        _context.Users.Remove(user);
        
        await _context.SaveChangesAsync();
    }

    private async Task<User> CreateTestUserAsync()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = HashPassword("Test123**"),
            FirstName = "Test",
            LastName = "User",
            SubscriptionTier = SubscriptionTier.Free,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Created test user: {user.Email}");
        return user;
    }

    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private async Task<List<Account>> CreateSampleAccountsAsync(Guid userId)
    {
        var accounts = new List<Account>
        {
            new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Primary Checking",
                Type = AccountType.Checking,
                Currency = "USD",
                Institution = "Chase Bank",
                Balance = 5000.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Savings Account",
                Type = AccountType.Savings,
                Currency = "USD",
                Institution = "Chase Bank",
                Balance = 15000.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Rewards Credit Card",
                Type = AccountType.CreditCard,
                Currency = "USD",
                Institution = "American Express",
                Balance = -1250.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Cash Wallet",
                Type = AccountType.Cash,
                Currency = "USD",
                Balance = 300.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _context.Accounts.AddRange(accounts);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Created {accounts.Count} sample accounts");
        return accounts;
    }

    private async Task<List<Category>> CreateSampleCategoriesAsync(Guid userId)
    {
        var categories = new List<Category>();

        // Income categories
        var incomeCategories = new[]
        {
            new { Name = "Salary", Icon = "💰", Color = "#10B981" },
            new { Name = "Freelance", Icon = "💻", Color = "#059669" },
            new { Name = "Investment Returns", Icon = "📈", Color = "#047857" },
            new { Name = "Side Business", Icon = "🏪", Color = "#065F46" },
            new { Name = "Other Income", Icon = "💵", Color = "#064E3B" }
        };

        foreach (var cat in incomeCategories)
        {
            categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = cat.Name,
                Type = CategoryType.Income,
                Icon = cat.Icon,
                Color = cat.Color,
                IsSystem = false,
                IsActive = true,
                DisplayOrder = categories.Count + 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Expense categories
        var expenseCategories = new[]
        {
            new { Name = "Groceries", Icon = "🛒", Color = "#EF4444" },
            new { Name = "Dining Out", Icon = "🍽️", Color = "#DC2626" },
            new { Name = "Transportation", Icon = "🚗", Color = "#B91C1C" },
            new { Name = "Utilities", Icon = "⚡", Color = "#991B1B" },
            new { Name = "Rent/Mortgage", Icon = "🏠", Color = "#7F1D1D" },
            new { Name = "Healthcare", Icon = "🏥", Color = "#F59E0B" },
            new { Name = "Entertainment", Icon = "🎬", Color = "#D97706" },
            new { Name = "Shopping", Icon = "🛍️", Color = "#B45309" },
            new { Name = "Subscriptions", Icon = "📱", Color = "#92400E" },
            new { Name = "Insurance", Icon = "🛡️", Color = "#78350F" },
            new { Name = "Education", Icon = "📚", Color = "#8B5CF6" },
            new { Name = "Travel", Icon = "✈️", Color = "#7C3AED" },
            new { Name = "Personal Care", Icon = "💇", Color = "#6D28D9" },
            new { Name = "Gifts & Donations", Icon = "🎁", Color = "#5B21B6" },
            new { Name = "Other Expenses", Icon = "📝", Color = "#4C1D95" }
        };

        foreach (var cat in expenseCategories)
        {
            categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = cat.Name,
                Type = CategoryType.Expense,
                Icon = cat.Icon,
                Color = cat.Color,
                IsSystem = false,
                IsActive = true,
                DisplayOrder = categories.Count + 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        _context.Categories.AddRange(categories);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Created {categories.Count} sample categories");
        return categories;
    }

    private async Task GenerateTransactionsAsync(Guid userId, List<Account> accounts, List<Category> categories)
    {
        var transactions = new List<Transaction>();
        var startDate = DateTime.UtcNow.AddYears(-1);
        var endDate = DateTime.UtcNow;

        var incomeCategories = categories.Where(c => c.Type == CategoryType.Income).ToList();
        var expenseCategories = categories.Where(c => c.Type == CategoryType.Expense).ToList();
        
        var checkingAccount = accounts.First(a => a.Type == AccountType.Checking);
        var creditAccount = accounts.First(a => a.Type == AccountType.CreditCard);
        var cashAccount = accounts.First(a => a.Type == AccountType.Cash);

        var currentDate = startDate;

        while (currentDate <= endDate)
        {
            // Monthly salary (first working day of month)
            if (currentDate.Day <= 3 && currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
            {
                transactions.Add(CreateTransaction(userId, checkingAccount.Id, 
                    GetRandomAmount(4500, 5500), TransactionType.Credit,
                    "Salary Deposit", "ACME Corporation", 
                    incomeCategories.First(c => c.Name == "Salary").Id, currentDate));
            }

            // Weekly groceries (Saturdays)
            if (currentDate.DayOfWeek == DayOfWeek.Saturday)
            {
                var groceryStores = new[] { "Whole Foods", "Safeway", "Target", "Costco", "Trader Joe's" };
                transactions.Add(CreateTransaction(userId, 
                    _random.NextDouble() > 0.3 ? checkingAccount.Id : creditAccount.Id,
                    -GetRandomAmount(80, 200), TransactionType.Debit,
                    "Grocery Shopping", groceryStores[_random.Next(groceryStores.Length)],
                    expenseCategories.First(c => c.Name == "Groceries").Id, currentDate));
            }

            // Random daily expenses
            if (_random.NextDouble() > 0.4) // 60% chance of daily expense
            {
                var expenseType = _random.Next(10);
                switch (expenseType)
                {
                    case 0: // Dining
                        var restaurants = new[] { "McDonald's", "Starbucks", "Chipotle", "Olive Garden", "Local Cafe" };
                        transactions.Add(CreateTransaction(userId, creditAccount.Id,
                            -GetRandomAmount(12, 85), TransactionType.Debit,
                            "Restaurant", restaurants[_random.Next(restaurants.Length)],
                            expenseCategories.First(c => c.Name == "Dining Out").Id, currentDate));
                        break;
                        
                    case 1: // Transportation
                        var transportMerchants = new[] { "Shell", "Chevron", "Uber", "Lyft", "Metro Transit" };
                        transactions.Add(CreateTransaction(userId, checkingAccount.Id,
                            -GetRandomAmount(15, 120), TransactionType.Debit,
                            "Transportation", transportMerchants[_random.Next(transportMerchants.Length)],
                            expenseCategories.First(c => c.Name == "Transportation").Id, currentDate));
                        break;
                        
                    case 2: // Entertainment
                        var entertainmentMerchants = new[] { "Netflix", "Spotify", "Cinema", "Amazon Prime", "Game Store" };
                        transactions.Add(CreateTransaction(userId, creditAccount.Id,
                            -GetRandomAmount(10, 150), TransactionType.Debit,
                            "Entertainment", entertainmentMerchants[_random.Next(entertainmentMerchants.Length)],
                            expenseCategories.First(c => c.Name == "Entertainment").Id, currentDate));
                        break;
                        
                    case 3: // Shopping
                        var shoppingMerchants = new[] { "Amazon", "Target", "Best Buy", "Walmart", "Local Store" };
                        transactions.Add(CreateTransaction(userId, creditAccount.Id,
                            -GetRandomAmount(25, 300), TransactionType.Debit,
                            "Shopping", shoppingMerchants[_random.Next(shoppingMerchants.Length)],
                            expenseCategories.First(c => c.Name == "Shopping").Id, currentDate));
                        break;
                }
            }

            // Monthly recurring expenses
            if (currentDate.Day == 1) // First of month
            {
                // Rent
                transactions.Add(CreateTransaction(userId, checkingAccount.Id,
                    -2200, TransactionType.Debit,
                    "Rent Payment", "Landlord", 
                    expenseCategories.First(c => c.Name == "Rent/Mortgage").Id, currentDate));
            }

            if (currentDate.Day == 5) // 5th of month
            {
                // Utilities
                transactions.Add(CreateTransaction(userId, checkingAccount.Id,
                    -GetRandomAmount(120, 180), TransactionType.Debit,
                    "Electric Bill", "PG&E",
                    expenseCategories.First(c => c.Name == "Utilities").Id, currentDate));
            }

            if (currentDate.Day == 15) // 15th of month
            {
                // Insurance
                transactions.Add(CreateTransaction(userId, checkingAccount.Id,
                    -GetRandomAmount(200, 350), TransactionType.Debit,
                    "Auto Insurance", "State Farm",
                    expenseCategories.First(c => c.Name == "Insurance").Id, currentDate));
            }

            // Occasional freelance income (random)
            if (_random.NextDouble() > 0.85 && currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
            {
                transactions.Add(CreateTransaction(userId, checkingAccount.Id,
                    GetRandomAmount(300, 1200), TransactionType.Credit,
                    "Freelance Payment", "Client Corp",
                    incomeCategories.First(c => c.Name == "Freelance").Id, currentDate));
            }

            currentDate = currentDate.AddDays(1);
        }

        _context.Transactions.AddRange(transactions);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Generated {transactions.Count} transactions over one year");
    }

    private Transaction CreateTransaction(Guid userId, Guid accountId, decimal amount, 
        TransactionType type, string description, string merchant, Guid categoryId, DateTime date)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = accountId,
            TransactionDate = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            PostedDate = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            Amount = amount,
            Type = type,
            OriginalMerchant = merchant,
            CategoryId = categoryId,
            Description = description,
            IsPending = false,
            IsRecurring = false,
            IsTransfer = false,
            IsSplit = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private decimal GetRandomAmount(decimal min, decimal max)
    {
        return (decimal)(_random.NextDouble() * (double)(max - min) + (double)min);
    }
}
