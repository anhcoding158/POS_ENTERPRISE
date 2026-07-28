using Microsoft.EntityFrameworkCore;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSaleConcurrencyTests
{
    [Fact]
    public async Task Concurrent_same_hold_payload_creates_one_document()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var request = database.HoldRequest(Guid.NewGuid());
        var barrier = new Barrier(2);
        async Task<POS.Application.Common.Result<POS.Application.DTOs.HeldSales.HeldSaleDto>> RunAsync()
        {
            await using var context = database.Context();
            barrier.SignalAndWait();
            return await database.HeldSaleService(context).CreateHeldSaleAsync(request);
        }

        var results = await Task.WhenAll(Task.Run(RunAsync), Task.Run(RunAsync));
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(results[0].Value.Id, results[1].Value.Id);
        Assert.Contains(results, result => result.Value.IsIdempotentReplay);
        await using var verify = database.Context();
        Assert.Equal(1, await verify.HeldSales.CountAsync());
        Assert.Empty(await verify.Orders.ToArrayAsync());
        Assert.Equal(20, await verify.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.Empty(await verify.InventoryMovements.ToArrayAsync());
        Assert.Empty(await verify.OrderReceiptSnapshots.ToArrayAsync());
        Assert.Empty(await verify.CheckoutRequestJournals.ToArrayAsync());
    }

    [Fact]
    public async Task Concurrent_same_id_different_payload_has_one_winner_and_one_conflict()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var requestId = Guid.NewGuid();
        var barrier = new Barrier(2);
        async Task<POS.Application.Common.Result<POS.Application.DTOs.HeldSales.HeldSaleDto>> RunAsync(int quantity)
        {
            await using var context = database.Context();
            barrier.SignalAndWait();
            return await database.HeldSaleService(context)
                .CreateHeldSaleAsync(database.HoldRequest(requestId, quantity));
        }

        var results = await Task.WhenAll(
            Task.Run(() => RunAsync(1)),
            Task.Run(() => RunAsync(3)));
        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result =>
            result.IsFailure && result.Error.Code == "HELD_SALE.IDEMPOTENCY_CONFLICT");
        await using var verify = database.Context();
        Assert.Equal(1, await verify.HeldSales.CountAsync());
    }

    [Fact]
    public async Task Concurrent_checkout_same_held_sale_creates_business_data_once()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        var request = database.CheckoutRequest(Guid.NewGuid(), heldSaleId);
        var barrier = new Barrier(2);
        async Task<POS.Application.Common.Result<POS.Application.DTOs.Checkout.CheckoutResultDto>> RunAsync()
        {
            await using var context = database.Context();
            barrier.SignalAndWait();
            return await database.CheckoutService(context).CheckoutAsync(request);
        }

        var results = await Task.WhenAll(Task.Run(RunAsync), Task.Run(RunAsync));
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(results[0].Value.OrderId, results[1].Value.OrderId);
        Assert.Contains(results, result => result.Value.IsIdempotentReplay);
        await database.AssertBusinessStateAsync(
            1, HeldSaleStatus.Completed, 1, 18, 1, 1,
            CheckoutRequestStatus.Completed);
        await using var verify = database.Context();
        await using var command = verify.Database.GetDbConnection().CreateCommand();
        await verify.Database.OpenConnectionAsync();
        command.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", (string?)await command.ExecuteScalarAsync());
    }
}
