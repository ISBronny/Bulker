using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Bulker;

/// <summary>
///     Интерфейс обработчика для аккумулятора.
/// </summary>
/// <typeparam name="TInput">Тип входных данных.</typeparam>
/// <typeparam name="TOutput">Тип выходных данных.</typeparam>
public interface IAccumulatorHandler<TInput, TOutput>
{
    Task<IList<TOutput>> HandleAsync(IList<TInput> items);
}

public interface IAccumulator<TInput, TOutput>
{
    Task<TOutput> ExecuteAsync(TInput item);
}

public class AccumulatorDynamicBase<TInput, TOutput> : IAccumulator<TInput, TOutput>
{
    private readonly IAccumulator<TInput, TOutput> _accumulator;

    public AccumulatorDynamicBase(IAccumulator<TInput, TOutput> accumulator)
    {
        _accumulator = accumulator;
    }

    public Task<TOutput> ExecuteAsync(TInput item)
    {
        return _accumulator.ExecuteAsync(item);
    }
}

public class Accumulator<TInput, TOutput> : IAccumulator<TInput, TOutput>
{
    private class InputWrapper
    {
        public TInput Item { get; set; }
        public TaskCompletionSource<TOutput> TaskCompletionSource { get; } = new();
    }

    private readonly ISubject<InputWrapper> _subject = new Subject<InputWrapper>();
    private readonly IAccumulatorHandler<TInput, TOutput> _handler;
    private readonly int _bufferSize;
    private readonly TimeSpan _timeSpan;

    public Accumulator(IAccumulatorHandler<TInput, TOutput> handler, int bufferSize, TimeSpan timeSpan)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _bufferSize = bufferSize;
        _timeSpan = timeSpan;
        Initialize();
    }

    private void Initialize()
    {
        _subject.Buffer(_timeSpan, _bufferSize)
            .Where(items => items.Any())
            .Subscribe(async wrappers =>
            {
                try
                {
                    var inputs = wrappers.Select(w => w.Item).ToList();
                    var outputs = await _handler.HandleAsync(inputs);

                    if (outputs.Count != wrappers.Count)
                        throw new InvalidOperationException(
                            "Количество результатов не соответствует количеству входных элементов.");

                    for (var i = 0; i < wrappers.Count; i++) wrappers[i].TaskCompletionSource.SetResult(outputs[i]);
                }
                catch (Exception ex)
                {
                    foreach (var wrapper in wrappers) wrapper.TaskCompletionSource.SetException(ex);
                }
            });
    }

    public Task<TOutput> ExecuteAsync(TInput item)
    {
        var wrapper = new InputWrapper { Item = item };
        _subject.OnNext(wrapper);
        return wrapper.TaskCompletionSource.Task;
    }
}

/// <summary>
///     Расширения для регистрации аккумулятора в контейнере служб.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAccumulator<TAccumulatorInterface, TInput, TOutput, THandler>(
        this IServiceCollection services,
        int bufferSize,
        TimeSpan timeSpan)
        where TAccumulatorInterface : class, IAccumulator<TInput, TOutput>
        where THandler : class, IAccumulatorHandler<TInput, TOutput>
    {
        var assemblyName =
            $"{Assembly.GetCallingAssembly().GetName().Name}.DynamicallyEmtittedTypes-{Guid.NewGuid():N}";

        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);

        var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);

        // Define a public class that inherits from MyClassBase and implements IMyClass and IMyClass2
        var typeBuilder = moduleBuilder.DefineType(
            $"Accumulator_{typeof(TAccumulatorInterface).Name}",
            TypeAttributes.Class);

        typeBuilder.SetParent(typeof(AccumulatorDynamicBase<TInput, TOutput>));
        typeBuilder.AddInterfaceImplementation(typeof(TAccumulatorInterface));
        typeBuilder.AddInterfaceImplementation(typeof(IAccumulator<TInput, TOutput>));


        CreatePass(typeBuilder, typeof(AccumulatorDynamicBase<TInput, TOutput>));

        // Create the type
        var accumulatorType = typeBuilder.CreateType();

        // Регистрируем обработчик
        services.AddSingleton<THandler>();

        // Регистрируем аккумулятор как реализацию маркерного интерфейса
        services.AddSingleton(typeof(TAccumulatorInterface), provider =>
        {
            var handler = provider.GetRequiredService<THandler>();
            var accumulator = new Accumulator<TInput, TOutput>(handler,
                bufferSize, timeSpan);
            return Activator.CreateInstance(accumulatorType, accumulator)!;
        });

        return services;
    }

    private static void CreatePass(TypeBuilder builder, Type baseType)
    {
        foreach (var baseConstructor in baseType.GetConstructors())
        {
            var parameters = baseConstructor.GetParameters();
            if (parameters.Length > 0 && parameters.Last().IsDefined(typeof(ParamArrayAttribute), false))
                //throw new InvalidOperationException("Variadic constructors are not supported");
                continue;
            var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
            var requiredCustomModifiers = parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray();
            var optionalCustomModifiers = parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray();
            var constructor = builder.DefineConstructor(
                MethodAttributes.Public, baseConstructor.CallingConvention,
                parameterTypes, requiredCustomModifiers,
                optionalCustomModifiers);
            for (var i = 0; i < parameters.Length; ++i)
            {
                var parameter = parameters[i];
                var parameterBuilder = constructor.DefineParameter(i + 1, parameter.Attributes, parameter.Name);
                if (((int)parameter.Attributes & (int)ParameterAttributes.HasDefault) != 0)
                    parameterBuilder.SetConstant(parameter.RawDefaultValue);

                foreach (var attribute in parameter.GetCustomAttributesData())
                    parameterBuilder.SetCustomAttribute(BuildCustomAttribute(attribute));
            }

            foreach (var attribute in baseConstructor.GetCustomAttributesData())
                constructor.SetCustomAttribute(BuildCustomAttribute(attribute));
            var emitter = constructor.GetILGenerator();
            emitter.Emit(OpCodes.Nop);
            // Load `this` and call base constructor with arguments    emitter.Emit(OpCodes.Ldarg_0);

            for (var i = 1; i <= parameters.Length; ++i) emitter.Emit(OpCodes.Ldarg, i);
            emitter.Emit(OpCodes.Call, baseConstructor);
            emitter.Emit(OpCodes.Ret);
        }
    }

    private static CustomAttributeBuilder BuildCustomAttribute(CustomAttributeData attribute)
    {
        var attributeArgs = attribute.ConstructorArguments.Select(a => UnwrapCustomAttributeArguments(a.Value))
            .ToArray();
        var namedPropertyInfos = attribute.NamedArguments.Select(a => a.MemberInfo).OfType<PropertyInfo>()
            .ToArray();
        var namedPropertyValues = attribute.NamedArguments.Where(a => a.MemberInfo is PropertyInfo)
            .Select(a => a.TypedValue.Value).ToArray();
        var namedFieldInfos = attribute.NamedArguments.Select(a => a.MemberInfo).OfType<FieldInfo>().ToArray();
        var namedFieldValues = attribute.NamedArguments.Where(a => a.MemberInfo is FieldInfo)
            .Select(a => a.TypedValue.Value).ToArray();
        return new CustomAttributeBuilder(
            attribute.Constructor, attributeArgs,
            namedPropertyInfos, namedPropertyValues,
            namedFieldInfos, namedFieldValues);
    }

    private static object? UnwrapCustomAttributeArguments(object? argument)
    {
        if (argument is IEnumerable<CustomAttributeTypedArgument> coll)
            return coll.Select(item => item.Value).ToArray();

        return argument;
    }
}