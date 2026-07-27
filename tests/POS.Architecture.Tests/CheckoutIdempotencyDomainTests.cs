using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class CheckoutIdempotencyDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);
    private const string Fingerprint = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public void Prepared_can_complete_once_and_keeps_order()
    {
        var journal = Create();
        journal.Complete(42, Now.AddMinutes(1));

        Assert.Equal(CheckoutRequestStatus.Completed, journal.Status);
        Assert.Equal(42, journal.OrderId);
        Assert.NotNull(journal.CompletedAtUtc);
        Assert.Throws<DomainException>(() => journal.Complete(43, Now.AddMinutes(2)));
        Assert.Equal(42, journal.OrderId);
    }

    [Fact]
    public void Prepared_journal_has_valid_initial_shape()
    {
        var journal = Create();
        Assert.Equal(CheckoutRequestStatus.Prepared, journal.Status);
        Assert.Null(journal.OrderId);
        Assert.Null(journal.CompletedAtUtc);
        Assert.Null(journal.AcknowledgedAtUtc);
        Assert.Null(journal.AbandonedAtUtc);
        Assert.Null(journal.AbandonedByUserId);
    }

    [Fact]
    public void Prepared_can_be_abandoned_append_only()
    {
        var journal = Create();
        journal.Abandon(9, Now.AddMinutes(1));

        Assert.Equal(CheckoutRequestStatus.Abandoned, journal.Status);
        Assert.Equal(9, journal.AbandonedByUserId);
        Assert.Null(journal.OrderId);
        Assert.Throws<DomainException>(() => journal.Complete(1, Now.AddMinutes(2)));
    }

    [Fact]
    public void Acknowledgment_requires_completed_and_is_idempotent()
    {
        var journal = Create();
        Assert.Throws<DomainException>(() => journal.Acknowledge(Now.AddMinutes(1)));
        journal.Complete(42, Now.AddMinutes(1));
        journal.Acknowledge(Now.AddMinutes(2));
        var acknowledged = journal.AcknowledgedAtUtc;
        journal.Acknowledge(Now.AddMinutes(3));
        Assert.Equal(acknowledged, journal.AcknowledgedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abcdef")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void Invalid_fingerprint_is_rejected(string fingerprint) =>
        Assert.Throws<DomainException>(() => new CheckoutRequestJournal(
            Guid.NewGuid(), fingerprint, "{}", Fingerprint, "{}", 1, Now));

    [Fact]
    public void Empty_client_request_id_is_rejected() =>
        Assert.Throws<DomainException>(() => new CheckoutRequestJournal(
            Guid.Empty, Fingerprint, "{}", Fingerprint, "{}", 1, Now));

    [Fact]
    public void Invalid_quote_fingerprint_is_rejected() =>
        Assert.Throws<DomainException>(() => new CheckoutRequestJournal(
            Guid.NewGuid(), Fingerprint, "{}", "BAD", "{}", 1, Now));

    [Theory]
    [InlineData("", "{\"version\":1}")]
    [InlineData("{\"version\":1}", " ")]
    public void Journal_json_snapshots_are_required(string canonical, string quote) =>
        Assert.Throws<DomainException>(() => new CheckoutRequestJournal(
            Guid.NewGuid(), Fingerprint, canonical, Fingerprint, quote, 1, Now));

    [Fact]
    public void Journal_does_not_expose_delete_or_replace_mutation()
    {
        var forbidden = new[] { "Delete", "Remove", "Replace", "Reset", "Reopen" };
        var methods = typeof(CheckoutRequestJournal).GetMethods(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(methods, method =>
            forbidden.Any(name => method.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
    }

    private static CheckoutRequestJournal Create() =>
        new(Guid.NewGuid(), Fingerprint, "{\"version\":1}", Fingerprint,
            "{\"version\":1}", 7, Now);
}
