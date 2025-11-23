using System;
using System.Collections.Generic;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;
using BudgetTracker.Observability.Models;

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
            return loggerConfiguration;
        }

        var columnWriters = new Dictionary<string, ColumnWriterBase>
        {
            { "id", new IdColumnWriter() },
            { "timestamp", new TimestampColumnWriter() },
            { "level", new LevelColumnWriter() },
            { "message", new MessageColumnWriter() },
            { "exception", new ExceptionColumnWriter() },
            { "source", new SourceColumnWriter() },
            { "user_id", new UserIdColumnWriter() },
            { "properties", new PropertiesColumnWriter() }
        };

        // Apply sampling filter if needed
        if (options.SamplingRate < 1.0)
        {
            loggerConfiguration = loggerConfiguration.Filter.ByIncludingOnly(logEvent =>
            {
                // Always log errors regardless of sampling rate
                if (logEvent.Level >= LogEventLevel.Error)
                {
                    return true;
                }

                // Apply sampling for other levels
                return Random.Shared.NextDouble() < options.SamplingRate;
            });
        }

        loggerConfiguration.WriteTo.PostgreSQL(
            connectionString,
            "ApplicationLogs",
            columnWriters,
            needAutoCreateTable: true,
            restrictedToMinimumLevel: LogEventLevel.Information,
            batchSizeLimit: 50,
            period: TimeSpan.FromSeconds(5),
            useCopy: true);

        return loggerConfiguration;
    }
}

// Custom column writers for PostgreSQL sink
public class IdColumnWriter : ColumnWriterBase
{
    public IdColumnWriter() : base(NpgsqlTypes.NpgsqlDbType.Uuid) { }
    public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null)
        => Guid.NewGuid();
}

public class TimestampColumnWriter : ColumnWriterBase
{
    public TimestampColumnWriter() : base(NpgsqlTypes.NpgsqlDbType.Timestamp) { }
    public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null)
        => logEvent.Timestamp.DateTime;
}

public class LevelColumnWriter : ColumnWriterBase
{
    public LevelColumnWriter() : base(NpgsqlTypes.NpgsqlDbType.Varchar, 50) { }
    public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null)
        => logEvent.Level.ToString();
}

public class MessageColumnWriter : ColumnWriterBase
{
    public MessageColumnWriter() : base(NpgsqlTypes.NpgsqlDbType.Text) { }
    public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null)
        => logEvent.RenderMessage(formatProvider);
}

public class ExceptionColumnWriter : ColumnWriterBase
{
    public ExceptionColumnWriter() : base(NpgsqlTypes.NpgsqlDbType.Text) { }
    public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null)
        => logEvent.Exception?.ToString();
}

public class SourceColumnWriter : ColumnWriterBase
{
    public SourceColumnWriter() : base(NpgsqlTypes.NpgsqlDbType.Varchar, 255) { }
    public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null)
    {
        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
        {
            return sourceContext.ToString().Trim('"');
        }
        return null;
    }
}

public class UserIdColumnWriter : ColumnWriterBase
{
    public UserIdColumnWriter() : base(NpgsqlTypes.NpgsqlDbType.Uuid) { }
    public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null)
    {
        if (logEvent.Properties.TryGetValue("UserId", out var userId) && 
            Guid.TryParse(userId.ToString().Trim('"'), out var guid))
        {
            return guid;
        }
        return null;
    }
}

public class PropertiesColumnWriter : ColumnWriterBase
{
    public PropertiesColumnWriter() : base(NpgsqlTypes.NpgsqlDbType.Jsonb) { }
    public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null)
    {
        var properties = new Dictionary<string, object>();
        foreach (var prop in logEvent.Properties)
        {
            if (prop.Key != "SourceContext" && prop.Key != "UserId")
            {
                properties[prop.Key] = prop.Value.ToString().Trim('"');
            }
        }
        return System.Text.Json.JsonSerializer.Serialize(properties);
    }
}

