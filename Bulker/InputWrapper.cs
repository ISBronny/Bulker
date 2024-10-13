namespace Bulker;

public class InputWrapper<TInput, TOutput>
{
    public required TInput Item { get; init; }
    public required CancellationToken CancellationToken { get; init; }

    internal TaskCompletionSource<TOutput> TaskCompletionSource { get; } = new();
}