using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentConcurrencyTests
{
    [Fact]
    public async Task Concurrent_same_create_creates_one_intent()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var requestId = Guid.NewGuid();
        using var barrier = new Barrier(2);

        async Task<(bool Success, int? Id, bool Replay)> ActAsync()
        {
            await using var context = database.Context();
            var service = database.PaymentIntentService(context);
            barrier.SignalAndWait();
            var result = await service.CreateAsync(Request(database, requestId));
            return result.IsSuccess
                ? (true, result.Value.Id, result.Value.IsReplay)
                : (false, null, false);
        }

        var first = Task.Run(ActAsync);
        var second = Task.Run(ActAsync);
        var results = await Task.WhenAll(first, second);

        Assert.All(results, value => Assert.True(value.Success));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Contains(results, value => value.Replay);
        await using var verify = database.Context();
        Assert.Equal(1, await verify.PaymentIntents.CountAsync());
    }

    [Fact]
    public async Task Concurrent_same_id_different_quote_has_one_winner_and_one_conflict()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var requestId = Guid.NewGuid();
        using var barrier = new Barrier(2);

        async Task<bool> ActAsync(int quantity)
        {
            await using var context = database.Context();
            barrier.SignalAndWait();
            return (await database.PaymentIntentService(context)
                .CreateAsync(Request(database, requestId, quantity))).IsSuccess;
        }

        var results = await Task.WhenAll(
            Task.Run(() => ActAsync(1)),
            Task.Run(() => ActAsync(2)));

        Assert.Single(results, value => value);
        await using var verify = database.Context();
        Assert.Equal(1, await verify.PaymentIntents.CountAsync());
    }

    [Fact]
    public async Task Concurrent_checkout_same_intent_creates_business_data_once()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        int intentId;
        var checkoutRequestId = Guid.NewGuid();

        await using (var setup = database.Context())
        {
            var payments = database.PaymentIntentService(setup);
            var created = await payments.CreateAsync(Request(database, Guid.NewGuid()));
            Assert.True(created.IsSuccess);
            intentId = created.Value.Id;
            Assert.True((await payments.MarkPresentedAsync(intentId)).IsSuccess);
            Assert.True((await payments.ConfirmReceivedAsync(intentId)).IsSuccess);

            var checkout = database.CheckoutService(setup);
            Assert.True((await checkout.PrepareCheckoutAsync(
                CheckoutRequest(database, intentId, checkoutRequestId))).IsSuccess);
        }

        using var barrier = new Barrier(2);
        async Task<POS.Application.Common.Result<CheckoutResultDto>> ActAsync()
        {
            await using var context = database.Context();
            barrier.SignalAndWait();
            return await database.CheckoutService(context).CheckoutAsync(
                CheckoutRequest(database, intentId, checkoutRequestId));
        }

        var results = await Task.WhenAll(
            Task.Run(ActAsync),
            Task.Run(ActAsync));

        Assert.All(results, result => Assert.True(
            result.IsSuccess,
            result.IsFailure ? result.AppError.Message : null));
        Assert.Equal(results[0].Value.OrderId, results[1].Value.OrderId);
        Assert.Contains(results, result => result.Value.IsIdempotentReplay);

        await using var verify = database.Context();
        var orderId = await verify.Orders.Select(value => value.Id).SingleAsync();
        var intent = await verify.PaymentIntents.SingleAsync();
        Assert.Equal(PaymentIntentStatus.Completed, intent.Status);
        Assert.Equal(orderId, intent.CompletedOrderId);
        Assert.Equal(1, await verify.Orders.CountAsync());
        Assert.Equal(1, await verify.OrderItems.CountAsync());
        Assert.Equal(18, await verify.Products.Select(value => value.StockQuantity).SingleAsync());
        Assert.Equal(1, await verify.InventoryMovements.CountAsync());
        Assert.Equal(1, await verify.OrderReceiptSnapshots.CountAsync());
        Assert.Equal(0, await verify.OrderDiscountSnapshots.CountAsync());
        Assert.Equal(
            CheckoutRequestStatus.Completed,
            await verify.CheckoutRequestJournals.Select(value => value.Status).SingleAsync());

        await verify.Database.OpenConnectionAsync();
        await using var command = verify.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        Assert.Null(await command.ExecuteScalarAsync());
        command.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", (string?)await command.ExecuteScalarAsync());
    }

    private static CreatePaymentIntentRequest Request(
        HeldSaleTestDatabase database, Guid requestId, int quantity = 2) =>
        new(requestId, new CheckoutRequest(
            [new CheckoutLineRequest(database.ProductId, quantity)],
            PaymentMethod.VietQr,
            0,
            confirmedPaymentAmount: 1,
            clientRequestId: Guid.NewGuid()));

    private static CheckoutRequest CheckoutRequest(
        HeldSaleTestDatabase database,
        int intentId,
        Guid requestId) =>
        new(
            [new CheckoutLineRequest(database.ProductId, 2)],
            PaymentMethod.VietQr,
            cashReceived: 0,
            confirmedPaymentAmount: 70_000,
            clientRequestId: requestId,
            paymentIntentId: intentId);
}
