namespace Bulker;

/// <summary>
///     Интерфейс обработчика для аккумулятора.
/// </summary>
/// <typeparam name="TInput">Тип входных данных.</typeparam>
/// <typeparam name="TOutput">Тип выходных данных.</typeparam>
public interface IAccumulatorHandler<TInput, TOutput>
{
    Task<IReadOnlyDictionary<TInput, TOutput>> HandleAsync(IList<InputWrapper<TInput, TOutput>> items);
}