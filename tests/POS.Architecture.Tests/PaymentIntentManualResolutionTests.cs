using Microsoft.EntityFrameworkCore;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentManualResolutionTests
{
    [Fact]
    public void Manual_resolution_requires_reason()
    {
        Assert.Throws<DomainException>(() => new PaymentIntentManualResolution(
            1, PaymentIntentManualResolutionType.NoRealMoneyTestTransaction,
            1, " ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void External_refund_resolution_requires_external_reference()
    {
        Assert.Throws<DomainException>(() => new PaymentIntentManualResolution(
            1, PaymentIntentManualResolutionType.RefundedExternally,
            1, "Đã hoàn ngoài POS", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Link_existing_order_requires_exact_identity()
    {
        Assert.Throws<DomainException>(() => new PaymentIntentManualResolution(
            1, PaymentIntentManualResolutionType.LinkExistingOrder,
            1, "Đối chiếu hóa đơn", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Manual_resolution_is_immutable()
    {
        var writableProperties = typeof(PaymentIntentManualResolution)
            .GetProperties()
            .Where(x => x.Name is nameof(PaymentIntentManualResolution.ResolutionType)
                or nameof(PaymentIntentManualResolution.ResolvedAtUtc)
                or nameof(PaymentIntentManualResolution.ResolvedByUserId)
                or nameof(PaymentIntentManualResolution.Reason)
                or nameof(PaymentIntentManualResolution.ExternalReference)
                or nameof(PaymentIntentManualResolution.LinkedOrderId))
            .Where(x => x.SetMethod?.IsPublic == true)
            .ToArray();

        Assert.Empty(writableProperties);
    }

    [Fact]
    public async Task Manually_resolved_intent_not_in_active_pending()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var intent = CreateConfirmedIntent(database.UserId);
        context.PaymentIntents.Add(intent);
        await context.SaveChangesAsync();
        context.PaymentIntentManualResolutions.Add(new PaymentIntentManualResolution(
            intent.Id, PaymentIntentManualResolutionType.NoRealMoneyTestTransaction,
            database.UserId, "Giao dịch thử nghiệm", DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();

        var pending = await new PaymentIntentRepository(context)
            .GetPendingAsync(database.UserId, 25);

        Assert.DoesNotContain(pending, x => x.Id == intent.Id);
        Assert.Single(await context.PaymentIntentManualResolutions
            .Where(x => x.PaymentIntentId == intent.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Manual_resolution_is_idempotent_at_storage_boundary()
    {
        await using var database = await HeldSaleTestDatabase.CreateAsync();
        await using var context = database.Context();
        var intent = CreateConfirmedIntent(database.UserId);
        context.PaymentIntents.Add(intent);
        await context.SaveChangesAsync();
        context.PaymentIntentManualResolutions.Add(new PaymentIntentManualResolution(
            intent.Id, PaymentIntentManualResolutionType.NoRealMoneyTestTransaction,
            database.UserId, "Test", DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.PaymentIntentManualResolutions.Add(new PaymentIntentManualResolution(
            intent.Id, PaymentIntentManualResolutionType.NoRealMoneyTestTransaction,
            database.UserId, "Test lần hai", DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static PaymentIntent CreateConfirmedIntent(int userId)
    {
        var now = DateTimeOffset.UtcNow;
        var intent = new PaymentIntent(
            Guid.NewGuid(), $"VQ{Guid.NewGuid():N}"[..14], 145_000, "TEST",
            "payload", new string('A', 64), "BANK", "123", "POS",
            new string('B', 64), "{}", userId, now, now.AddMinutes(15));
        intent.MarkPresented(now.AddSeconds(1));
        intent.MarkConfirmed(userId, now.AddSeconds(2));
        return intent;
    }
}
