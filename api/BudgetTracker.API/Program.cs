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
using BudgetTracker.Common.Services.Messaging;
using BudgetTracker.API.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/budget-tracker-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

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

    builder.Services.AddDbContext<BudgetTrackerDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), 
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
    builder.Services.AddScoped<IChatService, ChatService>();
    builder.Services.AddScoped<IEmailService, EmailService>();

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

    // Redis Message Queue Service
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrEmpty(redisConnectionString))
    {
    builder.Services.AddSingleton<IMessageQueueService>(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<SimpleRedisMessageQueueService>>();
        return new SimpleRedisMessageQueueService(redisConnectionString, logger);
    });
    }
    else
    {
        Log.Warning("Redis connection string not found in configuration - message queue service will not be available");
    }

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

    app.UseHttpsRedirection();

    app.UseCors("AllowAll");

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