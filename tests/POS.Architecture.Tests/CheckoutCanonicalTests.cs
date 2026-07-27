using POS.Application.DTOs.Checkout;
using POS.Application.Services;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class CheckoutCanonicalTests
{
    [Fact]
    public void Same_checkout_with_different_line_order_has_same_request_fingerprint()
    {
        var canonicalizer = new CheckoutRequestCanonicalizer();
        var first = canonicalizer.Canonicalize(Request(
            [new CheckoutLineRequest(2, 1), new CheckoutLineRequest(1, 3)]));
        var second = canonicalizer.Canonicalize(Request(
            [new CheckoutLineRequest(1, 3), new CheckoutLineRequest(2, 1)]));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Same_modifier_selection_in_different_order_has_same_request_fingerprint()
    {
        var canonicalizer = new CheckoutRequestCanonicalizer();
        var first = canonicalizer.Canonicalize(Request(new CheckoutLineRequest(
            1, 2, [new(8, 1), new(3, 2)])));
        var second = canonicalizer.Canonicalize(Request(new CheckoutLineRequest(
            1, 2, [new(3, 2), new(8, 1)])));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Whitespace_normalization_has_same_request_fingerprint()
    {
        var canonicalizer = new CheckoutRequestCanonicalizer();
        var first = canonicalizer.Canonicalize(Request(
            new CheckoutLineRequest(1, 1, notes: "  ghi   chú "), notes: " đơn   hàng "));
        var second = canonicalizer.Canonicalize(Request(
            new CheckoutLineRequest(1, 1, notes: "ghi chú"), notes: "đơn hàng"));
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(2, PaymentMethod.Cash, 100000, null)]
    [InlineData(1, PaymentMethod.VietQr, 0, null)]
    [InlineData(1, PaymentMethod.Cash, 200000, null)]
    [InlineData(1, PaymentMethod.Cash, 100000, "SALE")]
    public void Material_change_has_different_request_fingerprint(
        int quantity, PaymentMethod method, long cash, string? discount)
    {
        var canonicalizer = new CheckoutRequestCanonicalizer();
        var baseline = canonicalizer.Canonicalize(Request(new CheckoutLineRequest(1, 1)));
        var changed = canonicalizer.Canonicalize(new CheckoutRequest(
            [new CheckoutLineRequest(1, quantity)], method, cash,
            discountCode: discount, confirmedPaymentAmount: method == PaymentMethod.VietQr ? 30000 : 0));
        Assert.NotEqual(baseline.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public void Canonical_request_json_is_deterministic_roundtrips_and_has_no_secrets()
    {
        var canonicalizer = new CheckoutRequestCanonicalizer();
        var request = Request(new CheckoutLineRequest(1, 1), notes: " ghi chú ");
        var canonical = canonicalizer.Canonicalize(request);
        var roundtrip = canonicalizer.Canonicalize(
            canonicalizer.Deserialize(canonical.Json, Guid.NewGuid()));

        Assert.Equal(canonical, roundtrip);
        Assert.Matches("^[0-9A-F]{64}$", canonical.Fingerprint);
        Assert.DoesNotContain("Cost", canonical.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", canonical.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", canonical.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClientRequestId", canonical.Json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"version\":1", canonical.Json, StringComparison.Ordinal);
    }

    [Fact]
    public void Different_notes_change_fingerprint_when_notes_affect_request()
    {
        var canonicalizer = new CheckoutRequestCanonicalizer();
        var first = canonicalizer.Canonicalize(Request(
            new CheckoutLineRequest(1, 1), notes: "Mang đi"));
        var second = canonicalizer.Canonicalize(Request(
            new CheckoutLineRequest(1, 1), notes: "Dùng tại quầy"));
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void Different_modifier_changes_request_fingerprint()
    {
        var canonicalizer = new CheckoutRequestCanonicalizer();
        var first = canonicalizer.Canonicalize(Request(
            new CheckoutLineRequest(1, 1, [new(2, 1)])));
        var second = canonicalizer.Canonicalize(Request(
            new CheckoutLineRequest(1, 1, [new(3, 1)])));
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    private static CheckoutRequest Request(CheckoutLineRequest line, string? notes = null) =>
        Request([line], notes);

    private static CheckoutRequest Request(
        IEnumerable<CheckoutLineRequest> lines, string? notes = null) =>
        new(lines, PaymentMethod.Cash, 100000, notes: notes);
}
