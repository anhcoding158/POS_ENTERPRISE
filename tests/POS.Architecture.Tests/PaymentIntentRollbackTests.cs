using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentRollbackTests
{
    [Fact]
    public async Task Receipt_serializer_failure_rolls_back_all_and_retry_completes_once()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        int intentId;
        var checkoutId = Guid.NewGuid();

        await using (var context = database.Context())
        {
            var payment = database.PaymentIntentService(context);
            var created = await payment.CreateAsync(IntentRequest(database));
            intentId = created.Value.Id;
            await payment.MarkPresentedAsync(intentId);
            await payment.ConfirmReceivedAsync(intentId);

            var failing = database.CheckoutService(
                context, serializer: new HeldSaleThrowingSerializer());
            var request = CheckoutRequest(database, intentId, checkoutId);
            Assert.True((await failing.PrepareCheckoutAsync(request)).IsSuccess);
            var failed = await failing.CheckoutAsync(request);
            Assert.True(failed.IsFailure);
        }

        await using (var verify = database.Context())
        {
            var intent = await verify.PaymentIntents.SingleAsync();
            Assert.Equal(PaymentIntentStatus.Confirmed, intent.Status);
            Assert.Null(intent.CompletedOrderId);
            Assert.Equal(0, await verify.Orders.CountAsync());
            Assert.Equal(0, await verify.OrderItems.CountAsync());
            Assert.Equal(20, await verify.Products.Select(x => x.StockQuantity).SingleAsync());
            Assert.Equal(0, await verify.InventoryMovements.CountAsync());
            Assert.Equal(0, await verify.OrderReceiptSnapshots.CountAsync());
            Assert.Equal(0, await verify.OrderDiscountSnapshots.CountAsync());
            Assert.Equal(CheckoutRequestStatus.Prepared,
                await verify.CheckoutRequestJournals.Select(x => x.Status).SingleAsync());
        }

        await using (var retryContext = database.Context())
        {
            var retry = await database.CheckoutService(retryContext)
                .CheckoutAsync(CheckoutRequest(database, intentId, checkoutId));
            Assert.True(retry.IsSuccess, retry.IsFailure ? retry.AppError.Message : null);
        }

        await using (var final = database.Context())
        {
            Assert.Equal(1, await final.Orders.CountAsync());
            Assert.Equal(1, await final.InventoryMovements.CountAsync());
            Assert.Equal(1, await final.OrderReceiptSnapshots.CountAsync());
            Assert.Equal(PaymentIntentStatus.Completed,
                await final.PaymentIntents.Select(x => x.Status).SingleAsync());
        }
    }

    private static CreatePaymentIntentRequest IntentRequest(HeldSaleTestDatabase database) =>
        new(Guid.NewGuid(), new CheckoutRequest(
            [new CheckoutLineRequest(database.ProductId, 2)],
            PaymentMethod.VietQr, 0, confirmedPaymentAmount: 1,
            clientRequestId: Guid.NewGuid()));

    private static CheckoutRequest CheckoutRequest(
        HeldSaleTestDatabase database, int intentId, Guid requestId) =>
        new([new CheckoutLineRequest(database.ProductId, 2)],
            PaymentMethod.VietQr, 0, confirmedPaymentAmount: 70_000,
            clientRequestId: requestId, paymentIntentId: intentId);
}
