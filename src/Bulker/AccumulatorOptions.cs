namespace Bulker;

public sealed class AccumulatorOptions
{
    /// <summary>
    ///     Maximum batch size
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    ///     The maximum waiting time for a batch to be filled, after which the batch will be processed,
    ///     even if the maximum number of elements has not been collected
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    ///     Whether to try to process each element in the batch one by one if an error occurred while processing the batch.
    ///     This can be useful if the batch contains some invalid data that cannot be processed.
    /// </summary>
    public bool TryProcessBySingleOnError { get; set; } = true;
}