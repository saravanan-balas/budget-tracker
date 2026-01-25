using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using BudgetTracker.Common.Data;
using BudgetTracker.Common.Services;
using BudgetTracker.API.Services;
using BudgetTracker.Common.Services.Parsing;
using BudgetTracker.Common.Services.AI;
using BudgetTracker.Common.Services.OCR;
using BudgetTracker.Common.Services.Templates;
using BudgetTracker.Common.Services.Merchants;
using BudgetTracker.Common.Services.Categories;
using BudgetTracker.Common.Services.Transactions;
using BudgetTracker.API.Middleware;
using BudgetTracker.Observability.Models;
using BudgetTracker.Observability.Interfaces;
using BudgetTracker.Observability.Services;
using BudgetTracker.Observability.Extensions;
using FluentValidation;
using FluentValidation.AspNetCore;

// Enable Serilog self-logging to see internal errors
Serilog.Debugging.SelfLog.Enable(Console.Error);

// Build configuration first to read observability options
var tempBuilder = WebApplication.CreateBuilder(args);
var observabilityOptions = tempBuilder.Configuration.GetSection("Observability").Get<ObservabilityOptions>() ?? new ObservabilityOptions();

// Build connection string for Serilog configuration
var host = Environment.GetEnvironmentVariable("DB_HOST");
var user = Environment.GetEnvironmentVariable("DB_USER");
var password = Environment.GetEnvironmentVariable("DB_PASSWORD");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");

string connectionString;
// Only use environment variables if ALL required variables are set
if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(dbName))
{
    // Use Prefer for local Docker containers, Require for remote databases
    var sslMode = (host == "postgres" || host == "localhost" || host?.StartsWith("127.0.0.1") == true) 
        ? "Prefer" 
        : "Require";
    connectionString = $"Host={host};Database={dbName};Username={user};Password={password};SSL Mode={sslMode};";
}
else
{
    connectionString = tempBuilder.Configuration.GetConnectionString("DefaultConnection") ?? "localhost connection not found";
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/budget-tracker-.txt", rollingInterval: RollingInterval.Day)
    .ConfigurePostgresSink(connectionString, observabilityOptions)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();
    
    // Configure ObservabilityOptions
    builder.Services.Configure<ObservabilityOptions>(builder.Configuration.GetSection("Observability"));
    
    // Register ObservabilityService
    builder.Services.AddScoped<IObservabilityService>(sp =>
    {
        var context = sp.GetRequiredService<BudgetTrackerDbContext>();
        return new ObservabilityService(context);
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme."
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] {}
            }
        });
    });

    // Debug: Print relevant environment variables
    Console.WriteLine("[DEBUG] Relevant Environment Variables:");
    var relevantVars = new[] { "DB_HOST", "DB_USER", "DB_PASSWORD", "DB_NAME", "OPENAI_API_KEY", "JWT_KEY", "AZURE_STORAGE_CONNECTION_STRING", "ConnectionStrings__DefaultConnection" };
    foreach (var varName in relevantVars)
    {
        var value = Environment.GetEnvironmentVariable(varName);
        var maskedValue = varName.Contains("PASSWORD") || varName.Contains("KEY") || varName.Contains("CONNECTION") 
            ? (string.IsNullOrEmpty(value) ? "NOT SET" : "***MASKED***") 
            : value ?? "NOT SET";
        Console.WriteLine($"  {varName} = {maskedValue}");
    }
    
    // Reuse connection string from above (already built for Serilog)
    Console.WriteLine($"[DEBUG] Connection String: {connectionString}");
    
    builder.Services.AddDbContext<BudgetTrackerDbContext>(options =>
        {
            options.UseNpgsql(connectionString, 
                b => {
                    b.MigrationsAssembly("BudgetTracker.API");
                    b.UseVector();
                });
            
            // Reduce SQL logging verbosity - only log warnings and errors
            options.LogTo(Console.WriteLine, LogLevel.Warning);
        });

    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddFluentValidationClientsideAdapters();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Configure storage service - use local storage for development
    builder.Services.AddScoped<IBlobStorageService, LocalFileStorageService>();
    
    // Existing services
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ITransactionService, TransactionService>();
    builder.Services.AddScoped<ISimplifiedImportService, SimplifiedImportService>();
    builder.Services.AddScoped<ISynchronousImportService, SynchronousImportService>();
    builder.Services.AddScoped<IChatService, ChatService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    
    // Monthly Insights (Ask AI)
    builder.Services.AddHttpClient<IOpenAiInsightsClient, OpenAiInsightsClient>();
    builder.Services.AddScoped<IMonthlyInsightsService, MonthlyInsightsService>();

    // Universal Bank Import Services
    builder.Services.AddScoped<IFormatDetectionService, FormatDetectionService>();
    builder.Services.AddScoped<IUniversalBankParser, UniversalBankParser>();
    builder.Services.AddHttpClient<IAIBankAnalyzer, AIBankAnalyzer>();
    builder.Services.AddScoped<IOCRService, OCRService>();
    builder.Services.AddScoped<IBankTemplateService, BankTemplateService>();

    // Memory cache for embedding optimization
    builder.Services.AddMemoryCache();

    // Optimized Services
    builder.Services.AddScoped<IMerchantService, OptimizedMerchantService>();
    builder.Services.AddScoped<ICategoryAssignmentService, OptimizedCategoryAssignmentService>();
    builder.Services.AddScoped<IBatchTransactionService, OptimizedBatchTransactionService>();

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll",
            builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();

    app.UseMiddleware<ErrorHandlingMiddleware>();

    // CORS must be before HTTPS redirection to handle preflight requests
    app.UseCors("AllowAll");

    // Disable HTTPS redirection in development to avoid issues with HTTP requests
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerDbContext>();
        if (context.Database.GetPendingMigrations().Any())
        {
            context.Database.Migrate();
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}