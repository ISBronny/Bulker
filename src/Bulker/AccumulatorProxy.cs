using System.Reflection;

namespace Bulker;

public class AccumulatorProxy<TInput, TOutput> : DispatchProxy
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private IAccumulator<TInput, TOutput> _accumulator;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public void SetAccumulator(IAccumulator<TInput, TOutput> accumulator)
    {
        _accumulator = accumulator;
    }

    protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        // Try to find the corresponding method on the accumulator
        var method = _accumulator.GetType().GetMethod(targetMethod.Name,
            targetMethod.GetParameters().Select(p => p.ParameterType).ToArray());

        if (method != null) return method.Invoke(_accumulator, args);

        throw new NotImplementedException($"Method {targetMethod.Name} is not implemented in the accumulator.");
    }
}