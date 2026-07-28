using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSalePersistenceTests
{
    [Fact]
    public void HeldSalePersistence_model_has_required_unique_indexes_and_concurrency()
    {
        using var context = new PosDbContext(
            new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite("Data Source=:memory:").Options);
        var entity = context.Model.FindEntityType(typeof(HeldSale));
        Assert.NotNull(entity);
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(HeldSale.ClientRequestId));
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(HeldSale.DisplayCode));
        Assert.True(entity.FindProperty("ConcurrencyToken")!.IsConcurrencyToken);
    }

    [Fact]
    public void HeldSalePersistence_completed_order_link_is_unique()
    {
        using var context = new PosDbContext(
            new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite("Data Source=:memory:").Options);
        var entity = context.Model.FindEntityType(typeof(HeldSale))!;
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(HeldSale.CompletedOrderId));
    }
}
