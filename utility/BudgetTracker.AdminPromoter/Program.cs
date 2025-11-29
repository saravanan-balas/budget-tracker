using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Models;

if (args.Length == 0)
{
    Console.WriteLine("Usage: BudgetTracker.AdminPromoter <email>");
    Console.WriteLine("Example: BudgetTracker.AdminPromoter user@example.com");
    Environment.Exit(1);
}

var email = args[0];

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((context, config) =>
{
    config.AddJsonFile("appsettings.json", optional: false);
    config.AddEnvironmentVariables();
});

builder.ConfigureServices((context, services) =>
{
    var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
    
    if (string.IsNullOrEmpty(connectionString))
    {
        Console.WriteLine("ERROR: DefaultConnection not found in appsettings.json or environment variables");
        Environment.Exit(1);
    }
    
    services.AddDbContext<BudgetTrackerDbContext>(options =>
        options.UseNpgsql(connectionString, o => o.UseVector()));
});

var host = builder.Build();

using var scope = host.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerDbContext>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation($"Looking for user with email: {email}");
    
    var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
    
    if (user == null)
    {
        logger.LogError($"User with email '{email}' not found in database");
        Console.WriteLine($"ERROR: User with email '{email}' not found.");
        Console.WriteLine("Available users:");
        var allUsers = await context.Users.Select(u => u.Email).ToListAsync();
        foreach (var userEmail in allUsers)
        {
            Console.WriteLine($"  - {userEmail}");
        }
        Environment.Exit(1);
    }
    
    if (user.IsAdmin)
    {
        logger.LogInformation($"User '{email}' is already an admin");
        Console.WriteLine($"User '{email}' is already an admin.");
        Environment.Exit(0);
    }
    
    user.IsAdmin = true;
    user.UpdatedAt = DateTime.UtcNow;
    
    await context.SaveChangesAsync();
    
    logger.LogInformation($"Successfully promoted user '{email}' to admin");
    Console.WriteLine($"✓ Successfully promoted user '{email}' to admin.");
    Console.WriteLine();
    Console.WriteLine("IMPORTANT: The user must logout and login again to get a new JWT token");
    Console.WriteLine("with the updated IsAdmin claim. The logs tab will appear after re-authentication.");
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred while promoting user to admin");
    Console.WriteLine($"ERROR: {ex.Message}");
    Environment.Exit(1);
}

