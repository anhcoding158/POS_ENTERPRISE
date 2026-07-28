using Microsoft.EntityFrameworkCore;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSaleCheckoutIntegrationTests
{
    [Fact]
    public async Task Checkout_with_active_held_sale_completes_atomically()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        await using var context = database.Context();
        var result = await database.CheckoutService(context).CheckoutAsync(
            database.CheckoutRequest(Guid.NewGuid(), heldSaleId));

        Assert.True(result.IsSuccess);
        await database.AssertBusinessStateAsync(
            1, HeldSaleStatus.Completed, 1, 18, 1, 1,
            CheckoutRequestStatus.Completed);
        await using var verify = database.Context();
        var held = await verify.HeldSales.SingleAsync();
        Assert.Equal(result.Value.OrderId, held.CompletedOrderId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HeldSaleRollback_receipt_failure_keeps_active_and_rolls_back_business_data(
        bool serializerFailure)
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        await using var context = database.Context();
        var service = serializerFailure
            ? database.CheckoutService(context, serializer: new HeldSaleThrowingSerializer())
            : database.CheckoutService(context, snapshots: new HeldSaleThrowingSnapshotRepository());
        var result = await service.CheckoutAsync(
            database.CheckoutRequest(Guid.NewGuid(), heldSaleId));

        Assert.True(result.IsFailure);
        await database.AssertBusinessStateAsync(
            1, HeldSaleStatus.Active, 0, 20, 0, 0,
            CheckoutRequestStatus.Prepared);
        await using var verify = database.Context();
        Assert.Null(await verify.HeldSales.Select(x => x.CompletedOrderId).SingleAsync());
    }

    [Fact]
    public async Task Checkout_replay_does_not_complete_held_sale_twice()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        var request = database.CheckoutRequest(Guid.NewGuid(), heldSaleId);
        await using (var firstContext = database.Context())
        {
            var first = await database.CheckoutService(firstContext).CheckoutAsync(request);
            Assert.True(first.IsSuccess);
        }
        await using (var replayContext = database.Context())
        {
            var replay = await database.CheckoutService(replayContext).CheckoutAsync(request);
            Assert.True(replay.IsSuccess);
            Assert.True(replay.Value.IsIdempotentReplay);
        }
        await database.AssertBusinessStateAsync(
            1, HeldSaleStatus.Completed, 1, 18, 1, 1,
            CheckoutRequestStatus.Completed);
    }

    [Fact]
    public async Task Cancelled_held_sale_cannot_checkout()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        var heldSaleId = await database.CreateHeldSaleAsync();
        await using (var cancelContext = database.Context())
        {
            var cancel = await database.HeldSaleService(cancelContext)
                .CancelHeldSaleAsync(heldSaleId);
            Assert.True(cancel.IsSuccess);
        }
        await using (var checkoutContext = database.Context())
        {
            var checkout = await database.CheckoutService(checkoutContext).CheckoutAsync(
                database.CheckoutRequest(Guid.NewGuid(), heldSaleId));
            Assert.True(checkout.IsFailure);
        }
        await database.AssertBusinessStateAsync(
            1, HeldSaleStatus.Cancelled, 0, 20, 0, 0,
            CheckoutRequestStatus.Prepared);
    }

    [Fact]
    public void HeldSaleId_is_in_checkout_canonical_fingerprint_and_null_is_v1_compatible()
    {
        var canonicalizer = new POS.Application.Services.CheckoutRequestCanonicalizer();
        var normal = new POS.Application.DTOs.Checkout.CheckoutRequest(
            [new(1, 1)], PaymentMethod.Cash, 50_000, clientRequestId: Guid.NewGuid());
        var heldOne = new POS.Application.DTOs.Checkout.CheckoutRequest(
            [new(1, 1)], PaymentMethod.Cash, 50_000,
            clientRequestId: Guid.NewGuid(), heldSaleId: 1);
        var heldTwo = new POS.Application.DTOs.Checkout.CheckoutRequest(
            [new(1, 1)], PaymentMethod.Cash, 50_000,
            clientRequestId: Guid.NewGuid(), heldSaleId: 2);

        Assert.Contains("\"version\":1", canonicalizer.Canonicalize(normal).Json);
        Assert.DoesNotContain("heldSaleId", canonicalizer.Canonicalize(normal).Json);
        Assert.Contains("\"version\":2", canonicalizer.Canonicalize(heldOne).Json);
        Assert.NotEqual(canonicalizer.Canonicalize(heldOne).Fingerprint,
            canonicalizer.Canonicalize(heldTwo).Fingerprint);
    }
}
