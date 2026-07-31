using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentManualResolutionApplicationProofTests
{
    [Theory]
    [InlineData(PaymentIntentManualResolutionType.LinkExistingOrder)]
    [InlineData(PaymentIntentManualResolutionType.NoRealMoneyTestTransaction)]
    [InlineData(PaymentIntentManualResolutionType.RefundedExternally)]
    public async Task Administrator_can_execute_all_three_resolution_paths(
        PaymentIntentManualResolutionType type)
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        int intentId;
        int? orderId = null;
        await using (var setup = database.Context())
        {
            intentId = await ConfirmAsync(database, setup);
            if (type == PaymentIntentManualResolutionType.LinkExistingOrder)
            {
                var order = CreateMatchingOrder(database.UserId);
                setup.Orders.Add(order);
                await setup.SaveChangesAsync();
                orderId = order.Id;
            }
        }

        await using (var action = database.Context())
        {
            var result = await database.PaymentIntentService(
                    action, role: Role.Administrator)
                .ResolveManuallyAsync(new(
                    intentId, type, "Đối chiếu tự động",
                    type == PaymentIntentManualResolutionType.RefundedExternally
                        ? "RF-PROOF-01" : null,
                    orderId));
            Assert.True(result.IsSuccess,
                result.IsFailure ? result.AppError.Message : null);
            Assert.Equal(type, result.Value.ResolutionType);
        }

        await using var verify = database.Context();
        Assert.Equal(1, await verify.PaymentIntentManualResolutions.CountAsync());
        Assert.Equal(1, await verify.PaymentIntents.CountAsync());
        Assert.Equal(orderId.HasValue ? 1 : 0, await verify.Orders.CountAsync());
        Assert.Equal(20, await verify.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.Equal(0, await verify.InventoryMovements.CountAsync());
        Assert.Equal(0, await verify.OrderReceiptSnapshots.CountAsync());
    }

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.Cashier)]
    public async Task Unauthorized_manual_resolution_has_no_business_mutation(Role role)
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        int intentId;
        await using (var setup = database.Context())
            intentId = await ConfirmAsync(database, setup);

        await using (var action = database.Context())
        {
            var result = await database.PaymentIntentService(action, role: role)
                .ResolveManuallyAsync(new(
                    intentId,
                    PaymentIntentManualResolutionType.NoRealMoneyTestTransaction,
                    "Không được phép"));
            Assert.True(result.IsFailure);
        }

        await using var verify = database.Context();
        Assert.Equal(0, await verify.PaymentIntentManualResolutions.CountAsync());
        Assert.Equal(PaymentIntentStatus.Confirmed,
            await verify.PaymentIntents.Select(x => x.Status).SingleAsync());
        Assert.Equal(0, await verify.Orders.CountAsync());
        Assert.Equal(20, await verify.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.Equal(0, await verify.InventoryMovements.CountAsync());
        Assert.Equal(0, await verify.OrderReceiptSnapshots.CountAsync());
    }

    [Theory]
    [InlineData(PaymentIntentManualResolutionType.LinkExistingOrder)]
    [InlineData(PaymentIntentManualResolutionType.NoRealMoneyTestTransaction)]
    [InlineData(PaymentIntentManualResolutionType.RefundedExternally)]
    public async Task Reason_is_required_for_all_three_paths(
        PaymentIntentManualResolutionType type)
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        int intentId;
        await using (var setup = database.Context())
            intentId = await ConfirmAsync(database, setup);
        await using var action = database.Context();
        var result = await database.PaymentIntentService(
                action, role: Role.Administrator)
            .ResolveManuallyAsync(new(intentId, type, " "));
        Assert.True(result.IsFailure);
        Assert.Equal(0, await action.PaymentIntentManualResolutions.CountAsync());
    }

    [Fact]
    public async Task Repeated_identical_manual_resolution_is_idempotent()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        int intentId;
        await using (var setup = database.Context())
            intentId = await ConfirmAsync(database, setup);
        var request = new ResolvePaymentIntentManuallyRequest(
            intentId,
            PaymentIntentManualResolutionType.RefundedExternally,
            "Đã hoàn tiền", "RF-IDEMPOTENT");

        int firstId;
        await using (var first = database.Context())
        {
            var result = await database.PaymentIntentService(
                first, role: Role.Administrator).ResolveManuallyAsync(request);
            Assert.True(result.IsSuccess);
            firstId = result.Value.Id;
        }
        await using (var retry = database.Context())
        {
            var result = await database.PaymentIntentService(
                retry, role: Role.Administrator).ResolveManuallyAsync(request);
            Assert.True(result.IsSuccess);
            Assert.Equal(firstId, result.Value.Id);
        }
        await using var verify = database.Context();
        Assert.Equal(1, await verify.PaymentIntentManualResolutions.CountAsync());
    }

    private static async Task<int> ConfirmAsync(
        HeldSaleTestDatabase database,
        POS.Infrastructure.Persistence.PosDbContext context)
    {
        var service = database.PaymentIntentService(context);
        var created = await service.CreateAsync(new(
            Guid.NewGuid(),
            new CheckoutRequest(
                [new CheckoutLineRequest(database.ProductId, 2)],
                PaymentMethod.VietQr, 0, confirmedPaymentAmount: 1,
                clientRequestId: Guid.NewGuid())));
        Assert.True(created.IsSuccess);
        Assert.True((await service.MarkPresentedAsync(created.Value.Id)).IsSuccess);
        Assert.True((await service.ConfirmReceivedAsync(created.Value.Id)).IsSuccess);
        return created.Value.Id;
    }

    private static Order CreateMatchingOrder(int userId)
    {
        var now = HeldSaleTestDatabase.Now;
        var order = new Order($"HD-LINK-{Guid.NewGuid():N}"[..18], userId, now);
        order.AddItem(1, "LINK", "Linked order", "Unit", 1, 1, 70_000, now);
        order.PrepareForPayment(now);
        order.MarkPaid(PaymentMethod.VietQr, 0, now);
        order.Complete(now);
        return order;
    }
}
