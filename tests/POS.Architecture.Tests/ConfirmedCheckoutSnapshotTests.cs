using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ConfirmedCheckoutSnapshotTests
{
    [Fact]
    public void Confirmed_checkout_snapshot_is_versioned_deterministic_and_has_no_cost_or_secrets()
    {
        var value = Snapshot();
        var first = ConfirmedCheckoutSnapshotJson.Serialize(value);
        var second = ConfirmedCheckoutSnapshotJson.Serialize(
            ConfirmedCheckoutSnapshotJson.Deserialize(first));

        Assert.Equal(ConfirmedCheckoutSnapshotJson.CurrentVersion, value.Version);
        Assert.Equal(first, second);
        Assert.Contains(value.ClientRequestId.ToString(), first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"unitPrice\":35000", first, StringComparison.Ordinal);
        Assert.DoesNotContain("cost", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$type", first, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"version\":999}")]
    public void Malformed_snapshot_is_rejected_without_unsafe_fallback(string json)
    {
        Assert.ThrowsAny<Exception>(() => ConfirmedCheckoutSnapshotJson.Deserialize(json));
    }

    private static ConfirmedCheckoutSnapshot Snapshot() =>
        new(ConfirmedCheckoutSnapshotJson.CurrentVersion, Guid.NewGuid(),
            PaymentMethod.VietQr, 12, null, "ghi chú",
            new SalesDiscountRequest(SalesDiscountType.FixedAmount, 5_000, "khuyến mại"),
            70_000, 5_000, 65_000, new string('A', 64),
            [new ConfirmedCheckoutLineSnapshot(
                1, "SP01", "Sản phẩm", "cái", 2, 35_000, 0, null, [])]);
}
