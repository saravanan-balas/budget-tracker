using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using BudgetTracker.Observability.Models;

namespace BudgetTracker.Observability.Sinks;

/// <summary>
/// Custom Serilog sink that writes logs to PostgreSQL using raw SQL with quoted identifiers.
/// This ensures PascalCase column names are respected (consistent with other tables).
/// </summary>
public class EfCoreSink : IBatchedLogEventSink
{
    private readonly string _connectionString;
    private readonly ObservabilityOptions _options;

    public EfCoreSink(string connectionString, ObservabilityOptions options)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task EmitBatchAsync(IEnumerable<LogEvent> events)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var logEntries = events
            .Where(e => ShouldLog(e))
            .Select(ConvertToApplicationLog)
            .ToList();

        if (logEntries.Count == 0)
        {
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Use batch INSERT for better performance
            // Build parameterized query with multiple value sets
            var valuePlaceholders = new List<string>();
            var parameters = new List<NpgsqlParameter>();
            
            for (int i = 0; i < logEntries.Count; i++)
            {
                var log = logEntries[i];
                var suffix = i.ToString();
                
                valuePlaceholders.Add($"(@Id{suffix}, @Timestamp{suffix}, @Level{suffix}, @Message{suffix}, @Exception{suffix}, @Source{suffix}, @UserId{suffix}, @Properties{suffix})");
                
                parameters.Add(new NpgsqlParameter($"Id{suffix}", log.Id));
                parameters.Add(new NpgsqlParameter($"Timestamp{suffix}", log.Timestamp));
                parameters.Add(new NpgsqlParameter($"Level{suffix}", log.Level));
                parameters.Add(new NpgsqlParameter($"Message{suffix}", log.Message));
                parameters.Add(new NpgsqlParameter($"Exception{suffix}", (object?)log.Exception ?? DBNull.Value));
                parameters.Add(new NpgsqlParameter($"Source{suffix}", (object?)log.Source ?? DBNull.Value));
                parameters.Add(new NpgsqlParameter($"UserId{suffix}", (object?)log.UserId ?? DBNull.Value));
                
                // Properties column is jsonb, so we need to set the parameter type explicitly
                var propertiesParam = new NpgsqlParameter($"Properties{suffix}", NpgsqlDbType.Jsonb);
                propertiesParam.Value = log.Properties ?? (object)DBNull.Value;
                parameters.Add(propertiesParam);
            }
            
            // Use quoted identifiers to preserve PascalCase column names
            // This matches the database schema (consistent with other tables)
            var sql = $@"
                INSERT INTO ""ApplicationLogs"" (""Id"", ""Timestamp"", ""Level"", ""Message"", ""Exception"", ""Source"", ""UserId"", ""Properties"")
                VALUES {string.Join(", ", valuePlaceholders)}";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddRange(parameters.ToArray());
            
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            // Log to console to avoid infinite recursion (can't use ILogger here)
            // This is safe because Console.WriteLine won't trigger Serilog
            Console.Error.WriteLine($"[EfCoreSink] Error writing logs: {ex.Message}");
            if (ex.StackTrace != null)
            {
                Console.Error.WriteLine($"[EfCoreSink] Stack trace: {ex.StackTrace}");
            }
        }
    }

    private bool ShouldLog(LogEvent logEvent)
    {
        // Apply sampling if configured
        if (_options.SamplingRate < 1.0)
        {
            // Always log errors
            if (logEvent.Level >= LogEventLevel.Error)
            {
                return true;
            }

            // Apply sampling for other levels
            return Random.Shared.NextDouble() < _options.SamplingRate;
        }

        return true;
    }

    private ApplicationLog ConvertToApplicationLog(LogEvent logEvent)
    {
        var log = new ApplicationLog
        {
            Id = Guid.NewGuid(),
            Timestamp = logEvent.Timestamp.DateTime,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            Exception = logEvent.Exception?.ToString()
        };

        // Extract Source from properties
        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
        {
            log.Source = sourceContext.ToString().Trim('"');
        }

        // Extract UserId from properties
        if (logEvent.Properties.TryGetValue("UserId", out var userId) &&
            Guid.TryParse(userId.ToString().Trim('"'), out var guid))
        {
            log.UserId = guid;
        }

        // Extract other properties as JSON
        var properties = new Dictionary<string, object>();
        foreach (var prop in logEvent.Properties)
        {
            if (prop.Key != "SourceContext" && prop.Key != "UserId")
            {
                properties[prop.Key] = prop.Value.ToString().Trim('"');
            }
        }

        if (properties.Count > 0)
        {
            log.Properties = System.Text.Json.JsonSerializer.Serialize(properties);
        }

        return log;
    }

    public Task OnEmptyBatchAsync()
    {
        // Called when there are no events to process
        // Nothing to do in this case
        return Task.CompletedTask;
    }

    public void Emit(LogEvent logEvent)
    {
        // This method is required by ILogEventSink but we use batching
        // The PeriodicBatching wrapper will call EmitBatchAsync
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
