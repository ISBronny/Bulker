using Microsoft.Extensions.DependencyInjection;

namespace Bulker.Tests;

public class UnitTest1
{
    [Fact]
    public async Task Test1()
    {
        var services = new ServiceCollection()
            .AddAccumulator<IPostgresBulkInsertAccumulator, Entity, Entity, PostgresBulkInsertHandler>(100,
                TimeSpan.FromMicroseconds(1));
        
        var sp = services.BuildServiceProvider();

        var accumulator = sp.GetRequiredService<IPostgresBulkInsertAccumulator>();

        var item = await accumulator.ExecuteAsync(new Entity());
        
        Assert.NotEqual(0, item.Id);
    }
}

public interface IPostgresBulkInsertAccumulator : IAccumulator<Entity, Entity>;

internal sealed class PostgresBulkInsertHandler : IAccumulatorHandler<Entity, Entity>
{
    public Task<IList<Entity>> HandleAsync(IList<Entity> items)
    {
        var insertedItems = items.Select(i =>
        {
            i.Id = Random.Shared.Next();
            return i;
        }).ToList();
        return Task.FromResult<IList<Entity>>(insertedItems);
    }
}

public class Entity
{
    public int Id { get; set; }
    public string Value { get; set; }
}