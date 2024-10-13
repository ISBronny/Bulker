using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bulker.Tests;

public class AccumulatorTests : IDisposable
{
    public void Dispose()
    {
        TestHandler.LastInputTestEntities.Clear();
        TestHandler.InvalidItem = null;
        TestHandler.Counter = 0;
    }

    [Fact]
    public async Task ExecuteAsync_WhenSingeItem_ShouldExecuteWithSingleItem()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddAccumulator<ITestAccumulator, InputTestEntity, OutputTestEntity, TestHandler>(new AccumulatorOptions
            {
                Timeout = TimeSpan.FromMilliseconds(10),
                MaxBatchSize = 100
            });
        var sp = services.BuildServiceProvider();
        var accumulator = sp.GetRequiredService<ITestAccumulator>();

        var input = new InputTestEntity
        {
            Id = 123
        };
        // Act
        var item = await accumulator.ExecuteAsync(input, default);

        // Assert
        Assert.Equal("123", item.Value);
        TestHandler.LastInputTestEntities.Single().Item.Should().Be(input);
    }

    [Fact]
    public async Task ExecuteAsync_WhenItemsCountLessThenBatch_ShouldExecuteUsingSingleBatch()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddAccumulator<ITestAccumulator, InputTestEntity, OutputTestEntity, TestHandler>(new AccumulatorOptions
            {
                Timeout = TimeSpan.FromMilliseconds(50),
                MaxBatchSize = 10
            });
        var sp = services.BuildServiceProvider();
        var accumulator = sp.GetRequiredService<ITestAccumulator>();

        var input = Enumerable.Range(0, 10).Select(x => new InputTestEntity { Id = x }).ToArray();
        // Act

        var output = await Task.WhenAll(input.Select(x => accumulator.ExecuteAsync(x, default)));

        // Assert
        output.Should().HaveCount(10);
        output.Select(x => x.Value).Should().BeEquivalentTo(input.Select(x => x.Id.ToString()));
        TestHandler.LastInputTestEntities.Should().HaveCount(10);
    }

    [Fact]
    public async Task ExecuteAsync_WhenItemsCountGreaterThenBatch_ShouldExecuteUsingSingleBatch()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddAccumulator<ITestAccumulator, InputTestEntity, OutputTestEntity, TestHandler>(new AccumulatorOptions
            {
                Timeout = TimeSpan.FromMilliseconds(50),
                MaxBatchSize = 5
            });
        var sp = services.BuildServiceProvider();
        var accumulator = sp.GetRequiredService<ITestAccumulator>();

        var input = Enumerable.Range(0, 10).Select(x => new InputTestEntity { Id = x }).ToArray();
        // Act

        var output = await Task.WhenAll(input.Select(x => accumulator.ExecuteAsync(x, default)));

        // Assert
        output.Should().HaveCount(10);
        output.Select(x => x.Value).Should().BeEquivalentTo(input.Select(x => x.Id.ToString()));
        TestHandler.LastInputTestEntities.Should().HaveCountLessOrEqualTo(5);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidItemInBatchAndSingleProcessingEnabled_ShouldExecuteSeparately()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddAccumulator<ITestAccumulator, InputTestEntity, OutputTestEntity, TestHandler>(new AccumulatorOptions
            {
                Timeout = TimeSpan.FromMilliseconds(50),
                MaxBatchSize = 2,
                TryProcessBySingleOnError = true
            });
        var sp = services.BuildServiceProvider();
        var accumulator = sp.GetRequiredService<ITestAccumulator>();

        var validInput = new InputTestEntity { Id = 1 };
        var invalidInput = new InputTestEntity { Id = 2 };
        TestHandler.InvalidItem = invalidInput;

        // Act

        var validTask = accumulator.ExecuteAsync(validInput, default);
        var invalidTask = accumulator.ExecuteAsync(invalidInput, default);


        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await invalidTask);
        await validTask;

        TestHandler.LastInputTestEntities.Should().HaveCount(1);
        TestHandler.Counter.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidItemInBatchAndSingleProcessingDisabled_ShouldThorw()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddAccumulator<ITestAccumulator, InputTestEntity, OutputTestEntity, TestHandler>(new AccumulatorOptions
            {
                Timeout = TimeSpan.FromMilliseconds(50),
                MaxBatchSize = 2,
                TryProcessBySingleOnError = false
            });
        var sp = services.BuildServiceProvider();
        var accumulator = sp.GetRequiredService<ITestAccumulator>();

        var validInput = new InputTestEntity { Id = 1 };
        var invalidInput = new InputTestEntity { Id = 2 };
        TestHandler.InvalidItem = invalidInput;

        // Act

        var validTask = accumulator.ExecuteAsync(validInput, default);
        var invalidTask = accumulator.ExecuteAsync(invalidInput, default);


        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Task.WhenAll(validTask, invalidTask));

        TestHandler.LastInputTestEntities.Should().HaveCount(2);
        TestHandler.Counter.Should().Be(1);
    }
}

public interface ITestAccumulator : IAccumulator<InputTestEntity, OutputTestEntity>;

internal sealed class TestHandler : IAccumulatorHandler<InputTestEntity, OutputTestEntity>
{
    public static IList<InputWrapper<InputTestEntity, OutputTestEntity>> LastInputTestEntities { get; private set; } =
        new List<InputWrapper<InputTestEntity, OutputTestEntity>>();

    public static InputTestEntity? InvalidItem { get; set; }

    public static int Counter { get; set; }

    public Task<IReadOnlyDictionary<InputTestEntity, OutputTestEntity>> HandleAsync(
        IList<InputWrapper<InputTestEntity, OutputTestEntity>> items)
    {
        Counter++;
        LastInputTestEntities = items.ToList();

        if (items.Select(x => x.Item).Contains(InvalidItem))
            throw new InvalidOperationException("Invalid item");

        var output = items
            .ToDictionary(x => x.Item, x => new OutputTestEntity
            {
                Value = x.Item.Id.ToString()
            });

        return Task.FromResult<IReadOnlyDictionary<InputTestEntity, OutputTestEntity>>(output);
    }
}

public sealed class InputTestEntity
{
    public int Id { get; set; }
}

public sealed class OutputTestEntity
{
    public string Value { get; set; }
}