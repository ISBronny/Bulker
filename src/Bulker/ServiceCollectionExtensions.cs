﻿using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bulker;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAccumulator<TAccumulatorInterface, TInput, TOutput, THandler>(
        this IServiceCollection services,
        AccumulatorOptions options)
        where TAccumulatorInterface : IAccumulator<TInput, TOutput>
        where THandler : class, IAccumulatorHandler<TInput, TOutput>
        where TInput : notnull
    {
        services.TryAddSingleton<THandler>();

        services.AddSingleton(typeof(TAccumulatorInterface), provider =>
        {
            var handler = provider.GetRequiredService<THandler>();
            var accumulator = new Accumulator<TInput, TOutput>(handler, options);

            return GetProxy<TAccumulatorInterface, TInput, TOutput>(accumulator);
        });

        return services;
    }

    private static TAccumulatorInterface GetProxy<TAccumulatorInterface, TInput, TOutput>(
        IAccumulator<TInput, TOutput> accumulator)
        where TAccumulatorInterface : IAccumulator<TInput, TOutput>
    {
        var proxy = DispatchProxy.Create<TAccumulatorInterface, AccumulatorProxy<TInput, TOutput>>();
        ((AccumulatorProxy<TInput, TOutput>)(object)proxy).SetAccumulator(accumulator);
        return proxy;
    }
}