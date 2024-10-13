namespace Bulker;

/// <summary>
///     Handler interface for the accumulator.
/// </summary>
/// <typeparam name="TInput">Input data type.</typeparam>
/// <typeparam name="TOutput">Output data type.</typeparam>
public interface IAccumulatorHandler<TInput, TOutput>
{
    /// <summary>
    ///     Users handler. Describe here everything you want to do with the batch.
    /// </summary>
    /// <param name="items"></param>
    /// <returns>A dictionary where the key is the input element and the value is the result of its processing.</returns>
    Task<IReadOnlyDictionary<TInput, TOutput>> HandleAsync(IList<InputWrapper<TInput, TOutput>> items);
}