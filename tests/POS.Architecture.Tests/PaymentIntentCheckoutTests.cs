using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentCheckoutTests
{
    [Fact]
    public async Task Confirmed_checkout_uses_persisted_request_snapshot_and_locked_price()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        int intentId;
        await using (var create = database.Context())
        {
            var payment = database.PaymentIntentService(create);
            var intent = await payment.CreateAsync(CreateIntentRequest(database));
            intentId = intent.Value.Id;
            await payment.MarkPresentedAsync(intentId);
            Assert.True((await payment.ConfirmReceivedAsync(intentId)).IsSuccess);

            var product = await create.Products.SingleAsync();
            product.ChangePrices(product.CostPrice, 99_000, DateTimeOffset.UtcNow);
            await create.SaveChangesAsync();
        }

        await using var retry = database.Context();
        var result = await database.CheckoutService(retry)
            .RetryConfirmedPaymentIntentAsync(intentId);

        Assert.True(result.IsSuccess, result.IsFailure ? result.AppError.Message : null);
        Assert.Equal(70_000, result.Value.TotalAmount);
        Assert.Equal(35_000,
            await retry.OrderItems.Select(item => item.UnitSalePrice).SingleAsync());
        Assert.Equal(1, await retry.Orders.CountAsync());
    }

    [Fact]
    public async Task Confirmed_intent_checkout_completes_atomically()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var payment = database.PaymentIntentService(context);
        var intent = await payment.CreateAsync(CreateIntentRequest(database));
        Assert.True((await payment.MarkPresentedAsync(intent.Value.Id)).IsSuccess);
        Assert.True((await payment.ConfirmReceivedAsync(intent.Value.Id)).IsSuccess);

        var checkout = database.CheckoutService(context);
        var request = CheckoutRequest(database, intent.Value.Id, Guid.NewGuid());
        var result = await checkout.CheckoutAsync(request);

        Assert.True(result.IsSuccess, result.IsFailure ? result.AppError.Message : null);
        var persisted = await context.PaymentIntents.AsNoTracking().SingleAsync();
        Assert.Equal(PaymentIntentStatus.Completed, persisted.Status);
        Assert.Equal(result.Value.OrderId, persisted.CompletedOrderId);
        Assert.Equal(intent.Value.Id, result.Value.PaymentIntentId);
        Assert.Equal(intent.Value.DisplayCode, result.Value.PaymentIntentDisplayCode);
        Assert.Equal(intent.Value.DisplayCode,
            result.Value.ReceiptSnapshot?.PaymentIntentDisplayCode);
        Assert.Equal(1, await context.Orders.CountAsync());
        Assert.Equal(18, await context.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.Equal(1, await context.InventoryMovements.CountAsync());
        Assert.Equal(1, await context.OrderReceiptSnapshots.CountAsync());
        Assert.Equal(CheckoutRequestStatus.Completed,
            await context.CheckoutRequestJournals.Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Confirmation_yes_confirms_intent_before_checkout_prepare()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var payment = database.PaymentIntentService(context);
        var created = await payment.CreateAsync(CreateIntentRequest(database));
        await payment.MarkPresentedAsync(created.Value.Id);
        Assert.True((await payment.ConfirmReceivedAsync(created.Value.Id)).IsSuccess);
        Assert.Equal(0, await context.CheckoutRequestJournals.CountAsync());

        var result = await database.CheckoutService(context).CheckoutAsync(
            CheckoutRequest(database, created.Value.Id, Guid.NewGuid()));

        Assert.True(result.IsSuccess, result.IsFailure ? result.AppError.Message : null);
        Assert.Equal(CheckoutRequestStatus.Completed,
            await context.CheckoutRequestJournals.Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Confirmation_no_creates_no_checkout_journal()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var payment = database.PaymentIntentService(context);
        var created = await payment.CreateAsync(CreateIntentRequest(database));
        await payment.MarkPresentedAsync(created.Value.Id);

        Assert.Equal(0, await context.CheckoutRequestJournals.CountAsync());
        Assert.Equal(PaymentIntentStatus.Presented,
            await context.PaymentIntents.Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Created_intent_restart_has_no_checkout_recovery()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using (var create = database.Context())
            Assert.True((await database.PaymentIntentService(create)
                .CreateAsync(CreateIntentRequest(database))).IsSuccess);
        await using var restart = database.Context();
        Assert.Empty((await database.CheckoutService(restart)
            .GetCheckoutRecoveryAsync()).Value);
        Assert.Single((await database.PaymentIntentService(restart)
            .RecoverPendingAsync()).Value);
    }

    [Fact]
    public async Task Presented_intent_restart_has_no_checkout_recovery()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using (var create = database.Context())
        {
            var payment = database.PaymentIntentService(create);
            var intent = await payment.CreateAsync(CreateIntentRequest(database));
            await payment.MarkPresentedAsync(intent.Value.Id);
        }
        await using var restart = database.Context();
        Assert.Empty((await database.CheckoutService(restart)
            .GetCheckoutRecoveryAsync()).Value);
        Assert.Equal(PaymentIntentStatus.Presented,
            Assert.Single((await database.PaymentIntentService(restart)
                .RecoverPendingAsync()).Value).Status);
    }

    [Fact]
    public async Task Confirmed_intent_restart_offers_payment_retry()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using (var create = database.Context())
        {
            var payment = database.PaymentIntentService(create);
            var intent = await payment.CreateAsync(CreateIntentRequest(database));
            await payment.MarkPresentedAsync(intent.Value.Id);
            await payment.ConfirmReceivedAsync(intent.Value.Id);
        }
        await using var restart = database.Context();
        Assert.Empty((await database.CheckoutService(restart)
            .GetCheckoutRecoveryAsync()).Value);
        Assert.True(Assert.Single((await database.PaymentIntentService(restart)
            .RecoverPendingAsync()).Value).CanRetryCheckout);
    }

    [Fact]
    public async Task Confirmed_payment_intent_cannot_be_silently_abandoned()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var payment = database.PaymentIntentService(context);
        var intent = await payment.CreateAsync(CreateIntentRequest(database));
        await payment.MarkPresentedAsync(intent.Value.Id);
        await payment.ConfirmReceivedAsync(intent.Value.Id);
        var checkout = database.CheckoutService(context);
        var request = CheckoutRequest(database, intent.Value.Id, Guid.NewGuid());
        Assert.True((await checkout.PrepareCheckoutAsync(request)).IsSuccess);

        var recovery = Assert.Single((await checkout.GetCheckoutRecoveryAsync()).Value);
        Assert.False(recovery.CanAbandon);
        Assert.True(recovery.HasConfirmedPayment);
        var abandon = await checkout.AbandonCheckoutAsync(request.ClientRequestId);
        Assert.True(abandon.IsFailure);
        Assert.Equal("CHECKOUT.CONFIRMED_PAYMENT_CANNOT_ABANDON", abandon.AppError.Code);
        Assert.Equal(CheckoutRequestStatus.Prepared,
            await context.CheckoutRequestJournals.Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Checkout_journal_is_created_only_after_manual_confirmation()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var payment = database.PaymentIntentService(context);
        var created = await payment.CreateAsync(CreateIntentRequest(database));
        var checkout = database.CheckoutService(context);
        var request = CheckoutRequest(database, created.Value.Id, Guid.NewGuid());
        var prepareCreated = await checkout.PrepareCheckoutAsync(request);
        Assert.True(prepareCreated.IsFailure);
        var result = await checkout.CheckoutAsync(request);
        Assert.True(result.IsFailure);
        Assert.Equal(0, await context.CheckoutRequestJournals.CountAsync());

        Assert.True((await payment.MarkPresentedAsync(created.Value.Id)).IsSuccess);
        var preparePresented = await checkout.PrepareCheckoutAsync(request);
        Assert.True(preparePresented.IsFailure);
        Assert.Equal(0, await context.CheckoutRequestJournals.CountAsync());

        Assert.True((await payment.ConfirmReceivedAsync(created.Value.Id)).IsSuccess);
        Assert.True((await checkout.PrepareCheckoutAsync(request)).IsSuccess);
        Assert.Equal(1, await context.CheckoutRequestJournals.CountAsync());
        Assert.Equal(0, await context.Orders.CountAsync());
        Assert.Equal(20, await context.Products.Select(x => x.StockQuantity).SingleAsync());
    }

    [Fact]
    public async Task Replay_returns_original_order_and_does_not_complete_twice()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var payment = database.PaymentIntentService(context);
        var created = await payment.CreateAsync(CreateIntentRequest(database));
        await payment.MarkPresentedAsync(created.Value.Id);
        await payment.ConfirmReceivedAsync(created.Value.Id);
        var checkout = database.CheckoutService(context);
        var request = CheckoutRequest(database, created.Value.Id, Guid.NewGuid());
        await checkout.PrepareCheckoutAsync(request);
        var first = await checkout.CheckoutAsync(request);
        var replay = await checkout.CheckoutAsync(request);
        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value.OrderId, replay.Value.OrderId);
        Assert.Equal(1, await context.Orders.CountAsync());
        Assert.Equal(1, await context.InventoryMovements.CountAsync());
        Assert.Equal(1, await context.OrderReceiptSnapshots.CountAsync());
    }

    private static CreatePaymentIntentRequest CreateIntentRequest(HeldSaleTestDatabase database) =>
        new(Guid.NewGuid(), new CheckoutRequest(
            [new CheckoutLineRequest(database.ProductId, 2)],
            PaymentMethod.VietQr,
            0,
            confirmedPaymentAmount: 1,
            clientRequestId: Guid.NewGuid()));

    private static CheckoutRequest CheckoutRequest(
        HeldSaleTestDatabase database, int intentId, Guid checkoutId) =>
        new(
            [new CheckoutLineRequest(database.ProductId, 2)],
            PaymentMethod.VietQr,
            0,
            confirmedPaymentAmount: 70_000,
            clientRequestId: checkoutId,
            paymentIntentId: intentId);
}
