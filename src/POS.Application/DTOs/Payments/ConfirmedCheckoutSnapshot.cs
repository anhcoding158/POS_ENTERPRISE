using System.Text.Json;
using POS.Application.DTOs.Checkout;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Payments;

public sealed record ConfirmedCheckoutSnapshot(
    int Version, Guid ClientRequestId, PaymentMethod PaymentMethod,
    int? PaymentIntentId, int? HeldSaleId, string? Notes,
    SalesDiscountRequest SalesDiscount, long Subtotal, long DiscountAmount,
    long Total, string QuoteFingerprint,
    IReadOnlyList<ConfirmedCheckoutLineSnapshot> Lines);

public sealed record ConfirmedCheckoutLineSnapshot(
    int ProductId, string ProductCode, string ProductName, string UnitName,
    int Quantity, long UnitPrice, long LineDiscountAmount, string? Notes,
    IReadOnlyList<CheckoutModifierRequest> Modifiers);

public static class ConfirmedCheckoutSnapshotJson
{
    public const int CurrentVersion = 1;
    public const int MaximumJsonLength = 256 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(ConfirmedCheckoutSnapshot value)
    {
        Validate(value);
        var json = JsonSerializer.Serialize(value, Options);
        if (json.Length > MaximumJsonLength)
            throw new InvalidOperationException("PaymentIntent checkout snapshot vượt giới hạn.");
        return json;
    }

    public static ConfirmedCheckoutSnapshot Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumJsonLength)
            throw new InvalidOperationException("PaymentIntent checkout snapshot không hợp lệ.");
        var value = JsonSerializer.Deserialize<ConfirmedCheckoutSnapshot>(json, Options)
            ?? throw new InvalidOperationException("PaymentIntent checkout snapshot không hợp lệ.");
        Validate(value);
        return value;
    }

    private static void Validate(ConfirmedCheckoutSnapshot value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Version != CurrentVersion || value.ClientRequestId == Guid.Empty ||
            value.PaymentMethod != PaymentMethod.VietQr || value.Total <= 0 ||
            value.Subtotal < value.DiscountAmount ||
            value.Subtotal - value.DiscountAmount != value.Total ||
            value.QuoteFingerprint.Length != 64 || value.Lines.Count is 0 or > 500)
            throw new InvalidOperationException("PaymentIntent checkout snapshot không hợp lệ.");
        if (value.Lines.Any(line =>
                line.ProductId <= 0 || line.Quantity <= 0 || line.UnitPrice < 0 ||
                string.IsNullOrWhiteSpace(line.ProductCode) ||
                string.IsNullOrWhiteSpace(line.ProductName) ||
                string.IsNullOrWhiteSpace(line.UnitName) || line.Modifiers.Count > 100))
            throw new InvalidOperationException("PaymentIntent checkout snapshot line không hợp lệ.");
    }
}
