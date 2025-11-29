using System;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using BudgetTracker.Observability.Models;
using BudgetTracker.Observability.Sinks;

namespace BudgetTracker.Observability.Extensions;

public static class SerilogExtensions
{
    public static LoggerConfiguration ConfigurePostgresSink(
        this LoggerConfiguration loggerConfiguration,
        string connectionString,
        ObservabilityOptions options)
    {
        if (!options.Enabled)
        {
            Console.WriteLine("[Serilog] PostgreSQL sink is DISABLED (Observability.Enabled = false)");
            return loggerConfiguration;
        }
        
        // Parse minimum log level from configuration
        var minimumLevel = ParseLogLevel(options.MinimumLevel);
        Console.WriteLine($"[Serilog] Configuring EF Core sink - Enabled: {options.Enabled}, SamplingRate: {options.SamplingRate}, MinimumLevel: {options.MinimumLevel} ({minimumLevel})");
        Console.WriteLine($"[Serilog] Connection string: {(string.IsNullOrEmpty(connectionString) ? "NULL" : connectionString.Substring(0, Math.Min(50, connectionString.Length)) + "...")}");
        Console.WriteLine("[Serilog] Using EF Core sink to ensure PascalCase column names match database schema (consistent with other tables)");

        // Apply sampling filter if needed
        if (options.SamplingRate < 1.0)
        {
            loggerConfiguration = loggerConfiguration.Filter.ByIncludingOnly(logEvent =>
            {
                // Always log errors and fatal regardless of sampling rate
                if (logEvent.Level >= LogEventLevel.Error)
                {
                    return true;
                }

                // Apply sampling for other levels
                return Random.Shared.NextDouble() < options.SamplingRate;
            });
            Console.WriteLine($"[Serilog] Sampling enabled: {options.SamplingRate * 100}% of non-error logs will be written");
        }

        try
        {
            // Use custom sink with raw SQL and quoted identifiers
            // This ensures PascalCase column names are respected (consistent with other tables)
            var efCoreSink = new EfCoreSink(connectionString, options);
            loggerConfiguration.WriteTo.Sink(
                new PeriodicBatchingSink(efCoreSink, new PeriodicBatchingSinkOptions
                {
                    BatchSizeLimit = 10,
                    Period = TimeSpan.FromSeconds(2)
                }),
                restrictedToMinimumLevel: minimumLevel);
            
            Console.WriteLine($"[Serilog] Minimum log level for database: {options.MinimumLevel} (logs below this level will not be written to database)");
            
            Console.WriteLine("[Serilog] Custom PostgreSQL sink configured for table 'ApplicationLogs' (PascalCase to match schema)");
            Console.WriteLine("[Serilog] Using quoted identifiers ensures consistent column naming with other tables");
            Console.WriteLine("[Serilog] Batch settings: Size=10, Period=2s");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Serilog] ERROR configuring EF Core sink: {ex.Message}");
            Console.WriteLine($"[Serilog] Stack trace: {ex.StackTrace}");
            // Don't throw - allow application to continue with other sinks
        }

        return loggerConfiguration;
    }

    private static LogEventLevel ParseLogLevel(string level)
    {
        return level?.ToUpperInvariant() switch
        {
            "VERBOSE" or "DEBUG" => LogEventLevel.Verbose,
            "INFORMATION" or "INFO" => LogEventLevel.Information,
            "WARNING" or "WARN" => LogEventLevel.Warning,
            "ERROR" => LogEventLevel.Error,
            "FATAL" or "CRITICAL" => LogEventLevel.Fatal,
            _ => LogEventLevel.Warning // Default to Warning if invalid
        };
    }
}

