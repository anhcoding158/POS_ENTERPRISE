using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSalePaymentOwnershipTests
{
    [Fact]
    public Task Created_payment_intent_excludes_held_sale_from_resumable_list() =>
        AssertExcludedAsync(PaymentIntentStatus.Created);

    [Fact]
    public Task Presented_payment_intent_excludes_held_sale_from_resumable_list() =>
        AssertExcludedAsync(PaymentIntentStatus.Presented);

    [Fact]
    public Task Confirmed_payment_intent_excludes_held_sale_from_resumable_list() =>
        AssertExcludedAsync(PaymentIntentStatus.Confirmed);

    [Fact]
    public async Task Cancelled_payment_intent_releases_active_held_sale()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var (_, intentId) = await CreateOwnedIntentAsync(database);
        await using var context = database.Context();
        Assert.True((await database.PaymentIntentService(context).CancelAsync(intentId)).IsSuccess);
        Assert.Single((await database.HeldSaleService(context).GetActiveHeldSalesAsync()).Value);
    }

    [Fact]
    public async Task Expired_payment_intent_releases_active_held_sale_according_to_policy()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var (_, intentId) = await CreateOwnedIntentAsync(database);
        await using var context = database.Context();
        Assert.True((await database.PaymentIntentService(context)
            .ExpireAsync(intentId, "Hết thời gian")).IsSuccess);
        Assert.Single((await database.HeldSaleService(context).GetActiveHeldSalesAsync()).Value);
    }

    [Fact]
    public async Task Stale_resume_is_blocked_by_authoritative_ownership_check()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var (heldSaleId, _) = await CreateOwnedIntentAsync(database);
        await using var context = database.Context();
        var result = await database.HeldSaleService(context)
            .GetHeldSaleForResumeAsync(heldSaleId);
        Assert.True(result.IsFailure);
        Assert.Equal("HELD_SALE.PAYMENT_OWNED", result.AppError.Code);
    }

    [Fact]
    public async Task Held_sale_and_payment_intent_have_one_UI_owner()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await CreateOwnedIntentAsync(database);
        await using var context = database.Context();
        Assert.Empty((await database.HeldSaleService(context).GetActiveHeldSalesAsync()).Value);
        Assert.Single((await database.PaymentIntentService(context).GetPendingAsync()).Value);
    }

    [Fact]
    public Task Cash_checkout_is_blocked_for_created_payment_owned_held_sale() =>
        AssertCashBlockedAsync(PaymentIntentStatus.Created);

    [Fact]
    public Task Cash_checkout_is_blocked_for_presented_payment_owned_held_sale() =>
        AssertCashBlockedAsync(PaymentIntentStatus.Presented);

    [Fact]
    public Task Cash_checkout_is_blocked_for_confirmed_payment_owned_held_sale() =>
        AssertCashBlockedAsync(PaymentIntentStatus.Confirmed);

    [Fact]
    public Task Blocked_cash_checkout_creates_no_order() =>
        AssertCashBlockedAsync(PaymentIntentStatus.Created);

    [Fact]
    public Task Blocked_cash_checkout_does_not_change_stock() =>
        AssertCashBlockedAsync(PaymentIntentStatus.Presented);

    [Fact]
    public Task Blocked_cash_checkout_does_not_complete_held_sale() =>
        AssertCashBlockedAsync(PaymentIntentStatus.Confirmed);

    private static async Task AssertCashBlockedAsync(PaymentIntentStatus status)
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var (heldSaleId, _) = await CreateOwnedIntentAsync(database, status);
        await using var context = database.Context();
        var stock = await context.Products.Select(x => x.StockQuantity).SingleAsync();
        var result = await database.CheckoutService(context).CheckoutAsync(
            database.CheckoutRequest(Guid.NewGuid(), heldSaleId));
        Assert.True(result.IsFailure);
        Assert.Equal("CHECKOUT.HELD_SALE_PAYMENT_OWNED", result.AppError.Code);
        Assert.Equal(0, await context.Orders.CountAsync());
        Assert.Equal(stock, await context.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.Equal(HeldSaleStatus.Active,
            await context.HeldSales.Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Cash_checkout_after_cancelled_payment_intent_is_allowed()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var (heldSaleId, intentId) = await CreateOwnedIntentAsync(database);
        await using var context = database.Context();
        Assert.True((await database.PaymentIntentService(context).CancelAsync(intentId)).IsSuccess);
        var result = await database.CheckoutService(context).CheckoutAsync(
            database.CheckoutRequest(Guid.NewGuid(), heldSaleId));
        Assert.True(result.IsSuccess, result.IsFailure ? result.AppError.Message : null);
    }

    [Fact]
    public async Task Two_payment_intents_cannot_own_same_held_sale()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var (heldSaleId, _) = await CreateOwnedIntentAsync(database);
        await using var context = database.Context();
        var second = await database.PaymentIntentService(context).CreateAsync(
            IntentRequest(database, heldSaleId));
        Assert.True(second.IsFailure);
        Assert.Equal(1, await context.PaymentIntents.CountAsync());
    }

    private static async Task AssertExcludedAsync(PaymentIntentStatus status)
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await CreateOwnedIntentAsync(database, status);
        await using var context = database.Context();
        Assert.Empty((await database.HeldSaleService(context).GetActiveHeldSalesAsync()).Value);
    }

    private static async Task<(int HeldSaleId, int IntentId)> CreateOwnedIntentAsync(
        HeldSaleTestDatabase database,
        PaymentIntentStatus status = PaymentIntentStatus.Created)
    {
        var heldSaleId = await database.CreateHeldSaleAsync();
        await using var context = database.Context();
        var service = database.PaymentIntentService(context);
        var created = await service.CreateAsync(IntentRequest(database, heldSaleId));
        Assert.True(created.IsSuccess, created.IsFailure ? created.AppError.Message : null);
        if (status is PaymentIntentStatus.Presented or PaymentIntentStatus.Confirmed)
            Assert.True((await service.MarkPresentedAsync(created.Value.Id)).IsSuccess);
        if (status == PaymentIntentStatus.Confirmed)
            Assert.True((await service.ConfirmReceivedAsync(created.Value.Id)).IsSuccess);
        return (heldSaleId, created.Value.Id);
    }

    private static CreatePaymentIntentRequest IntentRequest(
        HeldSaleTestDatabase database,
        int heldSaleId) =>
        new(Guid.NewGuid(), new CheckoutRequest(
            [new CheckoutLineRequest(database.ProductId, 2, notes: "Ít đá")],
            PaymentMethod.VietQr,
            0,
            notes: "Giao sau",
            confirmedPaymentAmount: 70_000,
            clientRequestId: Guid.NewGuid(),
            heldSaleId: heldSaleId));
}
