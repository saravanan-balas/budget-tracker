using Microsoft.EntityFrameworkCore;
using Serilog;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Services;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.Services.AI;
using BudgetTracker.Common.Services.OCR;
using BudgetTracker.Common.Services.Templates;
using BudgetTracker.Worker.Workers;
using BudgetTracker.Worker;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/worker-.txt", rollingInterval: RollingInterval.Day)
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
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddScoped<IBlobStorageService, LocalFileStorageService>();

    // Configure HttpClient for AI services
    builder.Services.AddHttpClient<IAIBankAnalyzer, AIBankAnalyzer>();

    // Universal Bank Import Services
    builder.Services.AddScoped<IFormatDetectionService, FormatDetectionService>();
    builder.Services.AddScoped<IUniversalBankParser, UniversalBankParser>();
    builder.Services.AddScoped<IOCRService, OCRService>();
    builder.Services.AddScoped<IBankTemplateService, BankTemplateService>();

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