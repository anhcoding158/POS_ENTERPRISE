using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.HeldSales;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSaleApplicationIntegrationTests
{
    [Fact]
    public async Task Create_held_sale_persists_snapshot_without_business_mutation()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var result = await database.HeldSaleService(context)
            .CreateHeldSaleAsync(database.HoldRequest(Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsIdempotentReplay);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal("CF-01", line.ProductCodeSnapshot);
        Assert.Equal("893000000001", line.BarcodeSnapshot);
        Assert.Equal("Cà phê sữa", line.ProductNameSnapshot);
        Assert.Equal(35_000, line.UnitPriceSnapshot);
        await database.AssertBusinessStateAsync(
            1, HeldSaleStatus.Active, 0, 20, 0, 0);
        Assert.Empty(await context.CheckoutRequestJournals.ToArrayAsync());
    }

    [Fact]
    public async Task Same_id_same_payload_returns_replay_without_second_document()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var request = database.HoldRequest(Guid.NewGuid());
        await using var firstContext = database.Context();
        var first = await database.HeldSaleService(firstContext).CreateHeldSaleAsync(request);
        await using var secondContext = database.Context();
        var second = await database.HeldSaleService(secondContext).CreateHeldSaleAsync(request);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(second.Value.IsIdempotentReplay);
        Assert.Equal(first.Value.Id, second.Value.Id);
        await using var verify = database.Context();
        Assert.Equal(1, await verify.HeldSales.CountAsync());
    }

    [Fact]
    public async Task Same_id_different_payload_returns_conflict_without_mutation()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var requestId = Guid.NewGuid();
        await using var firstContext = database.Context();
        Assert.True((await database.HeldSaleService(firstContext)
            .CreateHeldSaleAsync(database.HoldRequest(requestId, quantity: 1))).IsSuccess);
        await using var secondContext = database.Context();
        var conflict = await database.HeldSaleService(secondContext)
            .CreateHeldSaleAsync(database.HoldRequest(requestId, quantity: 3));

        Assert.True(conflict.IsFailure);
        Assert.Equal("HELD_SALE.IDEMPOTENCY_CONFLICT", conflict.Error.Code);
        await database.AssertBusinessStateAsync(
            1, HeldSaleStatus.Active, 0, 20, 0, 0);
    }

    [Fact]
    public async Task Active_list_is_bounded_sorted_no_tracking_and_excludes_terminal_states()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var ids = new List<int>();
        for (var index = 0; index < 3; index++)
            ids.Add(await database.CreateHeldSaleAsync());
        await using (var terminal = database.Context())
        {
            var service = database.HeldSaleService(terminal);
            Assert.True((await service.CancelHeldSaleAsync(ids[0])).IsSuccess);
        }
        await using var listContext = database.Context();
        var result = await database.HeldSaleService(listContext)
            .GetActiveHeldSalesAsync(limit: 1);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.DoesNotContain(result.Value, value => value.Id == ids[0]);
        Assert.Empty(listContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Resume_reports_price_stock_and_unavailable_without_mutation()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        await using (var mutate = database.Context())
        {
            var product = await mutate.Products.SingleAsync();
            product.ChangePrices(product.CostPrice, 40_000, HeldSaleTestDatabase.Now.AddMinutes(1));
            product.DecreaseStock(19, HeldSaleTestDatabase.Now.AddMinutes(1));
            await mutate.SaveChangesAsync();
        }
        await using var resumeContext = database.Context();
        var result = await database.HeldSaleService(resumeContext)
            .GetHeldSaleForResumeAsync(heldSaleId);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(HeldSaleResumeLineStatus.InsufficientStock, line.Status);
        Assert.Equal(35_000, line.UnitPriceSnapshot);
        Assert.Equal(40_000, line.CurrentUnitPrice);
        Assert.Equal(1, line.CurrentStock);
        Assert.Empty(resumeContext.ChangeTracker.Entries());
        await using var verify = database.Context();
        Assert.Equal(HeldSaleStatus.Active,
            await verify.HeldSales.Select(x => x.Status).SingleAsync());
        Assert.Empty(await verify.Orders.ToArrayAsync());
        Assert.Empty(await verify.CheckoutRequestJournals.ToArrayAsync());
    }

    [Fact]
    public async Task Resume_archived_product_reports_unavailable()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        await using (var mutate = database.Context())
        {
            var product = await mutate.Products.SingleAsync();
            product.Archive(database.UserId, HeldSaleTestDatabase.Now.AddMinutes(1));
            await mutate.SaveChangesAsync();
        }
        await using var context = database.Context();
        var result = await database.HeldSaleService(context)
            .GetHeldSaleForResumeAsync(heldSaleId);
        Assert.Equal(HeldSaleResumeLineStatus.Unavailable,
            Assert.Single(result.Value.Lines).Status);
    }

    [Fact]
    public async Task Cancel_is_idempotent_and_mutates_no_business_data()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        await using (var first = database.Context())
            Assert.True((await database.HeldSaleService(first)
                .CancelHeldSaleAsync(heldSaleId)).IsSuccess);
        await using (var replay = database.Context())
            Assert.True((await database.HeldSaleService(replay)
                .CancelHeldSaleAsync(heldSaleId)).IsSuccess);
        await database.AssertBusinessStateAsync(
            1, HeldSaleStatus.Cancelled, 0, 20, 0, 0);
    }
}
