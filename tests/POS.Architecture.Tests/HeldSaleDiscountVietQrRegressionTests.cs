using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Domain.Enums;
using POS.Infrastructure.Printing;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSaleDiscountVietQrRegressionTests
{
    [Fact]
    public async Task Resumed_held_sale_with_changed_fixed_discount_can_checkout_cash()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        var request = new CheckoutRequest(
            [new CheckoutLineRequest(database.ProductId, 2, notes: "Ít đá")],
            PaymentMethod.Cash,
            cashReceived: 100_000,
            notes: "Giao sau",
            clientRequestId: Guid.NewGuid(),
            heldSaleId: heldSaleId,
            salesDiscount: new SalesDiscountRequest(
                SalesDiscountType.FixedAmount, 10_000, "Khuyến mãi sau khi mở lại"));

        await using var context = database.Context();
        var result = await database.CheckoutService(
            context,
            permissionService: new HeldSaleAllowAllPermissionService()).CheckoutAsync(request);

        Assert.True(result.IsSuccess,
            result.IsFailure ? $"{result.AppError.Code}: {result.AppError.Message}" : null);
        Assert.Equal(60_000, result.Value.TotalAmount);
        var held = await context.HeldSales.SingleAsync(x => x.Id == heldSaleId);
        Assert.Equal(SalesDiscountType.None, held.DiscountType);
        Assert.Equal(0, held.RequestedDiscountValue);
        Assert.Null(held.DiscountReason);
        var discount = await context.OrderDiscountSnapshots.SingleAsync();
        Assert.Equal(SalesDiscountType.FixedAmount, discount.Type);
        Assert.Equal(10_000, discount.ResolvedAmount);
    }

    [Fact]
    public async Task Retry_confirmed_held_sale_with_changed_discount_completes_order()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();

        await using (var checkoutBContext = database.Context())
        {
            var checkoutB = await database.CheckoutService(checkoutBContext).CheckoutAsync(
                database.CheckoutRequest(Guid.NewGuid(), heldSaleId: null, quantity: 1));
            Assert.True(checkoutB.IsSuccess, checkoutB.IsFailure ? checkoutB.AppError.Message : null);
        }

        var checkoutRequestId = Guid.NewGuid();
        var changedDiscount = new SalesDiscountRequest(
            SalesDiscountType.FixedAmount, 10_000, "Khuyến mãi sau khi mở lại");
        var resumedCheckout = new CheckoutRequest(
            [new CheckoutLineRequest(database.ProductId, 2, notes: "Ít đá")],
            PaymentMethod.VietQr,
            cashReceived: 0,
            notes: "Giao sau",
            confirmedPaymentAmount: 60_000,
            clientRequestId: checkoutRequestId,
            heldSaleId: heldSaleId,
            salesDiscount: changedDiscount);

        int intentId;
        await using (var paymentContext = database.Context())
        {
            var payment = database.PaymentIntentService(paymentContext);
            var created = await payment.CreateAsync(
                new CreatePaymentIntentRequest(Guid.NewGuid(), resumedCheckout));
            Assert.True(created.IsSuccess, created.IsFailure ? created.AppError.Message : null);
            intentId = created.Value.Id;
            Assert.True((await payment.MarkPresentedAsync(intentId)).IsSuccess);
            Assert.True((await payment.ConfirmReceivedAsync(intentId)).IsSuccess);
        }

        await using var retryContext = database.Context();
        var checkout = database.CheckoutService(
            retryContext,
            permissionService: new HeldSaleAllowAllPermissionService());
        var result = await checkout.RetryConfirmedPaymentIntentAsync(intentId);

        Assert.True(result.IsSuccess,
            result.IsFailure ? $"{result.AppError.Code}: {result.AppError.Message}" : null);
        Assert.Equal(60_000, result.Value.TotalAmount);

        var orderA = await retryContext.Orders
            .Include(x => x.DiscountSnapshot)
            .SingleAsync(x => x.Id == result.Value.OrderId);
        Assert.Equal(70_000, orderA.Subtotal);
        Assert.Equal(10_000, orderA.DiscountAmount);
        Assert.Equal(SalesDiscountType.FixedAmount, orderA.DiscountSnapshot!.Type);
        Assert.Equal(10_000, orderA.DiscountSnapshot.RequestedValue);
        Assert.Equal("Khuyến mãi sau khi mở lại", orderA.DiscountSnapshot.Reason);

        var receiptEntity = await retryContext.OrderReceiptSnapshots
            .SingleAsync(x => x.OrderId == orderA.Id);
        var receipt = new ReceiptSnapshotJsonSerializer().Deserialize(receiptEntity.PayloadJson);
        Assert.Equal(10_000, receipt.DiscountAmount);
        Assert.Equal(SalesDiscountType.FixedAmount, receipt.SalesDiscountType);
        Assert.Equal(10_000, receipt.RequestedDiscountValue);

        var intent = await retryContext.PaymentIntents.SingleAsync(x => x.Id == intentId);
        Assert.Equal(PaymentIntentStatus.Completed, intent.Status);
        Assert.Equal(orderA.Id, intent.CompletedOrderId);
        var confirmedSnapshot = ConfirmedCheckoutSnapshotJson.Deserialize(
            intent.CheckoutRequestJson);
        var journal = await retryContext.CheckoutRequestJournals
            .SingleAsync(x => x.ClientRequestId == confirmedSnapshot.ClientRequestId);
        Assert.Equal(CheckoutRequestStatus.Completed, journal.Status);
        Assert.Equal(orderA.Id, journal.OrderId);
        var heldSale = await retryContext.HeldSales.SingleAsync(x => x.Id == heldSaleId);
        Assert.Equal(HeldSaleStatus.Completed, heldSale.Status);
        Assert.Equal(orderA.Id, heldSale.CompletedOrderId);
        Assert.Equal(17, await retryContext.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.Equal(2, await retryContext.InventoryMovements.CountAsync());
        Assert.Equal(2, await retryContext.OrderReceiptSnapshots.CountAsync());
        Assert.Empty((await database.PaymentIntentService(retryContext).GetPendingAsync()).Value);
        Assert.Empty((await database.HeldSaleService(retryContext).GetActiveHeldSalesAsync()).Value);

        var replay = await checkout.RetryConfirmedPaymentIntentAsync(intentId);
        Assert.True(replay.IsSuccess, replay.IsFailure ? replay.AppError.Message : null);
        Assert.Equal(orderA.Id, replay.Value.OrderId);
        Assert.True(replay.Value.IsIdempotentReplay);
        Assert.Equal(2, await retryContext.Orders.CountAsync());
        Assert.Equal(17, await retryContext.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.Equal(2, await retryContext.InventoryMovements.CountAsync());
        Assert.Equal(2, await retryContext.OrderReceiptSnapshots.CountAsync());
    }
}
