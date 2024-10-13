namespace Bulker;

public sealed class AccumulatorOptions
{
    public int MaxBatchSize { get; set; } = 100;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(50);
    public bool TryProcessBySingleOnError { get; set; } = true;
}