using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentDomainTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_payment_intent_has_valid_created_state()
    {
        var intent = Create();
        Assert.Equal(PaymentIntentStatus.Created, intent.Status);
        Assert.Equal(PaymentProvider.VietQr, intent.Provider);
        Assert.Equal("VND", intent.Currency);
        Assert.Null(intent.CompletedOrderId);
    }

    [Fact]
    public void Created_can_be_presented_once()
    {
        var intent = Create();
        intent.MarkPresented(Now.AddMinutes(1));
        var token = intent.ConcurrencyToken;
        intent.MarkPresented(Now.AddMinutes(2));
        Assert.Equal(PaymentIntentStatus.Presented, intent.Status);
        Assert.Equal(token, intent.ConcurrencyToken);
    }

    [Fact]
    public void Created_can_be_cancelled_once()
    {
        var intent = Create();
        intent.Cancel(Now.AddMinutes(1));
        intent.Cancel(Now.AddMinutes(2));
        Assert.Equal(PaymentIntentStatus.Cancelled, intent.Status);
    }

    [Fact]
    public void Presented_can_be_confirmed_once()
    {
        var intent = Presented();
        intent.MarkConfirmed(2, Now.AddMinutes(2));
        var confirmedAt = intent.ConfirmedAtUtc;
        intent.MarkConfirmed(3, Now.AddMinutes(3));
        Assert.Equal(confirmedAt, intent.ConfirmedAtUtc);
        Assert.Equal(2, intent.ConfirmedByUserId);
    }

    [Fact]
    public void Presented_can_be_cancelled_once()
    {
        var intent = Presented();
        intent.Cancel(Now.AddMinutes(2));
        Assert.Equal(PaymentIntentStatus.Cancelled, intent.Status);
    }

    [Fact]
    public void Confirmed_can_be_completed_once()
    {
        var intent = Confirmed();
        intent.Complete(10, Now.AddMinutes(3));
        intent.Complete(10, Now.AddMinutes(4));
        Assert.Equal(10, intent.CompletedOrderId);
    }

    [Fact]
    public void Confirmed_cannot_be_cancelled() =>
        Assert.Throws<DomainException>(() => Confirmed().Cancel(Now.AddMinutes(3)));

    [Fact]
    public void Confirmed_cannot_expire_after_money_was_manually_confirmed() =>
        Assert.Throws<DomainException>(() => Confirmed().Expire(Now.AddMinutes(20), "timeout"));

    [Fact]
    public void Completed_is_terminal()
    {
        var intent = Completed();
        Assert.Throws<DomainException>(() => intent.Cancel(Now.AddMinutes(4)));
        Assert.Throws<DomainException>(() => intent.Expire(Now.AddMinutes(4), "timeout"));
    }

    [Fact]
    public void Cancelled_is_terminal()
    {
        var intent = Create();
        intent.Cancel(Now.AddMinutes(1));
        Assert.Throws<DomainException>(() => intent.MarkPresented(Now.AddMinutes(2)));
    }

    [Fact]
    public void Expired_is_terminal()
    {
        var intent = Presented();
        intent.Expire(Now.AddMinutes(2), "timeout");
        Assert.Throws<DomainException>(() => intent.MarkConfirmed(2, Now.AddMinutes(3)));
    }

    [Fact]
    public void Completed_order_id_cannot_change() =>
        Assert.Throws<DomainException>(() => Completed().Complete(11, Now.AddMinutes(4)));

    [Fact]
    public void Empty_client_request_id_is_rejected() =>
        Assert.Throws<DomainException>(() => Create(clientRequestId: Guid.Empty));

    [Fact]
    public void Invalid_amount_is_rejected() =>
        Assert.Throws<DomainException>(() => Create(amount: 0));

    [Fact]
    public void Invalid_payload_hash_is_rejected() =>
        Assert.Throws<DomainException>(() => Create(payloadHash: new string('a', 64)));

    [Fact]
    public void Invalid_quote_fingerprint_is_rejected() =>
        Assert.Throws<DomainException>(() => Create(quoteFingerprint: "ABC"));

    [Fact]
    public void Confirmed_requires_actor_and_time()
    {
        Assert.Throws<DomainException>(() => Presented().MarkConfirmed(0, Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => Presented().MarkConfirmed(2, default));
    }

    [Fact]
    public void Completed_requires_order_and_confirmation()
    {
        Assert.Throws<DomainException>(() => Create().Complete(1, Now.AddMinutes(1)));
        Assert.Throws<DomainException>(() => Confirmed().Complete(0, Now.AddMinutes(3)));
    }

    [Fact]
    public void Payment_intent_contains_no_cost_secret_or_receipt_fields()
    {
        var names = typeof(PaymentIntent).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(names, x =>
            x.Contains("Cost", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Token", StringComparison.OrdinalIgnoreCase) && x != "ConcurrencyToken" ||
            x.Contains("Receipt", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Bitmap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Payment_intent_exposes_no_delete_mutation() =>
        Assert.DoesNotContain(
            typeof(PaymentIntent).GetMethods(),
            method => method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));

    private static PaymentIntent Presented()
    {
        var value = Create();
        value.MarkPresented(Now.AddMinutes(1));
        return value;
    }

    private static PaymentIntent Confirmed()
    {
        var value = Presented();
        value.MarkConfirmed(2, Now.AddMinutes(2));
        return value;
    }

    private static PaymentIntent Completed()
    {
        var value = Confirmed();
        value.Complete(10, Now.AddMinutes(3));
        return value;
    }

    private static PaymentIntent Create(
        Guid? clientRequestId = null,
        long amount = 100_000,
        string? payloadHash = null,
        string? quoteFingerprint = null) =>
        new(
            clientRequestId ?? Guid.NewGuid(),
            "VQ123",
            amount,
            "POS VQ123",
            "PAYLOAD",
            payloadHash ?? new string('A', 64),
            "970415",
            "123456789",
            "NGUYEN VAN A",
            quoteFingerprint ?? new string('B', 64),
            "{\"version\":4}",
            1,
            Now,
            Now.AddMinutes(15));
}
