namespace BudgetTracker.Observability.Models;

public class ObservabilityOptions
{
    /// <summary>
    /// Enable or disable writing logs to PostgreSQL database
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Sampling rate for logs (0.0 to 1.0). 
    /// 1.0 = log all events, 0.5 = log 50% of events, etc.
    /// Errors are always logged regardless of sampling rate.
    /// </summary>
    public double SamplingRate { get; set; } = 1.0;
    
    /// <summary>
    /// Minimum log level to write to database.
    /// Options: "Information", "Warning", "Error", "Fatal"
    /// Default: "Warning" (excludes Information level logs)
    /// </summary>
    public string MinimumLevel { get; set; } = "Warning";
}

