using Microsoft.EntityFrameworkCore;
using Serilog;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Services;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.Services.AI;
using BudgetTracker.Common.Services.OCR;
using BudgetTracker.Common.Services.Templates;
using BudgetTracker.Worker.Workers;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/worker-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog();

    builder.Services.AddDbContext<BudgetTrackerDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddScoped<IBlobStorageService, LocalFileStorageService>();

    // Universal Bank Import Services
    builder.Services.AddScoped<IFormatDetectionService, FormatDetectionService>();
    builder.Services.AddScoped<IUniversalBankParser, UniversalBankParser>();
    builder.Services.AddScoped<IAIBankAnalyzer, AIBankAnalyzer>();
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