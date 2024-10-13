namespace Bulker;

public interface IAccumulator<in TInput, TOutput>
{
    Task<TOutput> ExecuteAsync(TInput item, CancellationToken token);
}