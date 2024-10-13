using System.Collections.Frozen;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Bulker;

internal sealed class Accumulator<TInput, TOutput> : IAccumulator<TInput, TOutput> where TInput : notnull
{
    private readonly IAccumulatorHandler<TInput, TOutput> _handler;
    private readonly AccumulatorOptions _options;
    private readonly Subject<InputWrapper<TInput, TOutput>> _subject = new();

    public Accumulator(IAccumulatorHandler<TInput, TOutput> handler, AccumulatorOptions accumulatorOptions)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _options = accumulatorOptions ?? throw new ArgumentNullException(nameof(accumulatorOptions));
        Initialize();
    }

    public async Task<TOutput> ExecuteAsync(TInput item, CancellationToken token)
    {
        var wrapper = new InputWrapper<TInput, TOutput>
        {
            Item = item,
            CancellationToken = token
        };
        _subject.OnNext(wrapper);
        return await wrapper.TaskCompletionSource.Task.WaitAsync(token);
    }

    private void Initialize()
    {
        _subject.Buffer(_options.Timeout, _options.MaxBatchSize)
            .Where(x => x.Any())
            .Subscribe(async wrappers => await ProcessItems(wrappers));
    }

    private async Task ProcessItems(IList<InputWrapper<TInput, TOutput>> wrappers)
    {
        try
        {
            await ProcessBulk(wrappers);
        }
        catch (Exception ex)
        {
            if (_options.TryProcessBySingleOnError)
            {
                await ProcessBySingleItem(wrappers);
                return;
            }

            foreach (var wrapper in wrappers)
                wrapper.TaskCompletionSource.SetException(ex);
        }
    }

    private async Task ProcessBulk(IList<InputWrapper<TInput, TOutput>> wrappers)
    {
        var wrappersByInput = wrappers.ToFrozenDictionary(x => x.Item);
        var outputs = await _handler.HandleAsync(wrappers);

        if (outputs.Count != wrappers.Count)
            throw new InvalidOperationException(
                $"Wrong handler behavior. The number of outputs should be equal to the number of inputs. Expected: {wrappers.Count}, actual: {outputs.Count}");

        foreach (var input in wrappers)
        {
            var result = outputs[input.Item];
            wrappersByInput[input.Item].TaskCompletionSource.SetResult(result);
        }
    }

    private async Task ProcessBySingleItem(IList<InputWrapper<TInput, TOutput>> wrappers)
    {
        await Parallel.ForEachAsync(wrappers, new ParallelOptions(), async (wrapper, _) =>
        {
            try
            {
                var result = await _handler.HandleAsync([wrapper]);
                wrapper.TaskCompletionSource.SetResult(result[wrapper.Item]);
            }
            catch (Exception e)
            {
                wrapper.TaskCompletionSource.SetException(e);
            }
        });
    }
}