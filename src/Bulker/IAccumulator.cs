namespace Bulker;

public interface IAccumulator<in TInput, TOutput>
{
    /// <summary>
    ///     Performs an operation on elements according to the registered handler
    /// </summary>
    /// <param name="item"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<TOutput> ExecuteAsync(TInput item, CancellationToken token);
}