namespace Bulker;

/// <summary>
///     A wrapper around the passed object that stores element-related data, such as a Cancellation Token
/// </summary>
/// <typeparam name="TInput"></typeparam>
/// <typeparam name="TOutput"></typeparam>
public class InputWrapper<TInput, TOutput>
{
    public required TInput Item { get; init; }
    public required CancellationToken CancellationToken { get; init; }

    internal TaskCompletionSource<TOutput> TaskCompletionSource { get; } = new();
}