using Microsoft.EntityFrameworkCore;
using Serilog;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Services;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.Services.AI;
using BudgetTracker.Common.Services.OCR;
using BudgetTracker.Common.Services.Templates;
using BudgetTracker.Common.Services.Merchants;
using BudgetTracker.Common.Services.Categories;
using BudgetTracker.Common.Services.Transactions;
using BudgetTracker.Worker.Workers;
using BudgetTracker.Worker;
using BudgetTracker.Observability.Models;
using BudgetTracker.Observability.Extensions;

// Verify required environment variables are available
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: OPENAI_API_KEY environment variable not found!");
    Environment.Exit(1);
}
else
{
    Console.WriteLine($"OPENAI_API_KEY loaded successfully (length: {apiKey.Length})");
}

// Build configuration first to read observability options
var tempBuilder = Host.CreateApplicationBuilder(args);
var observabilityOptions = tempBuilder.Configuration.GetSection("Observability").Get<ObservabilityOptions>() ?? new ObservabilityOptions();
var connectionString = tempBuilder.Configuration.GetConnectionString("DefaultConnection") ?? "localhost connection not found";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/worker-.txt", rollingInterval: RollingInterval.Day)
    .ConfigurePostgresSink(connectionString, observabilityOptions)
    .CreateLogger();

try
{
    // Check if we're running in test mode
    if (args.Length > 0 && args[0] == "test-pdf")
    {
        // Test mode - just read and print PDF
        var testPdfPath = Path.Combine(Directory.GetCurrentDirectory(), "../../test-data/boa_credit_card_stmt.pdf");
        
        if (!File.Exists(testPdfPath))
        {
            Console.WriteLine($"Test PDF file not found at: {testPdfPath}");
            return;
        }

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog());
        services.AddScoped<TestPdfReader>();
        
        var serviceProvider = services.BuildServiceProvider();
        var testReader = serviceProvider.GetRequiredService<TestPdfReader>();
        
        await testReader.TestReadPdf(testPdfPath);
        
        Console.WriteLine("\nTest completed. Press any key to exit...");
        Console.ReadKey();
        return;
    }

    // Normal worker mode
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog();

    builder.Services.AddDbContext<BudgetTrackerDbContext>(options =>
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), 
            b => b.UseVector());
        
        // Suppress EF Core command logging to reduce noise
        options.LogTo(Console.WriteLine, LogLevel.Warning);
    });

    builder.Services.AddScoped<IBlobStorageService, LocalFileStorageService>();

    // Configure HttpClient for AI services
    builder.Services.AddHttpClient<IAIBankAnalyzer, AIBankAnalyzer>();

    // Universal Bank Import Services
    builder.Services.AddScoped<IFormatDetectionService, FormatDetectionService>();
    builder.Services.AddScoped<IUniversalBankParser, UniversalBankParser>();
    builder.Services.AddScoped<IOCRService, OCRService>();
    builder.Services.AddScoped<IBankTemplateService, BankTemplateService>();

    // Memory cache for embedding optimization
    builder.Services.AddMemoryCache();

    // Optimized Services
    builder.Services.AddScoped<IMerchantService, OptimizedMerchantService>();
    builder.Services.AddScoped<ICategoryAssignmentService, OptimizedCategoryAssignmentService>();
    builder.Services.AddScoped<IBatchTransactionService, OptimizedBatchTransactionService>();

    // Worker services (polling-based processing)
    builder.Services.AddHostedService<ImportProcessorWorker>();
    builder.Services.AddHostedService<RecurringTransactionWorker>();
    
    var host = builder.Build();

    using (var scope = host.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerDbContext>();
        if (context.Database.GetPendingMigrations().Any())
        {
            context.Database.Migrate();
        }
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker service startup failed");
}
finally
{
    Log.CloseAndFlush();
}