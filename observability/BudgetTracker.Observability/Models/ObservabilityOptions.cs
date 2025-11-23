namespace BudgetTracker.Observability.Models;

public class ObservabilityOptions
{
    public bool Enabled { get; set; } = true;
    public double SamplingRate { get; set; } = 1.0;
}

