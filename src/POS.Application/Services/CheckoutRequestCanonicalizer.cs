using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Checkout;
using POS.Domain.Enums;

namespace POS.Application.Services;

public sealed class CheckoutRequestCanonicalizer : ICheckoutRequestCanonicalizer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CanonicalCheckoutRequest Canonicalize(CheckoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = ToDocument(request);
        var json = JsonSerializer.Serialize(document, Options);
        return new(json, Hash(json));
    }

    public CheckoutRequest Deserialize(string canonicalJson, Guid clientRequestId)
    {
        var document = JsonSerializer.Deserialize<CanonicalDocument>(canonicalJson, Options) ??
            throw new InvalidOperationException("Canonical checkout request không hợp lệ.");
        if (document.Version is not (1 or 2 or 3 or 4))
            throw new InvalidOperationException("Canonical checkout request version không được hỗ trợ.");
        return new CheckoutRequest(
            document.Lines.Select(line => new CheckoutLineRequest(
                line.ProductId, line.Quantity,
                line.Modifiers.Select(modifier => new CheckoutModifierRequest(modifier.ModifierId, modifier.Quantity)),
                line.LineDiscountAmount, line.Notes)),
            document.PaymentMethod, document.CashReceived, document.CustomerId,
            document.RestaurantTableId, document.DiscountCode, document.Notes,
            document.ConfirmedPaymentAmount, clientRequestId, document.HeldSaleId,
            new SalesDiscountRequest(
                document.SalesDiscountType,
                document.SalesDiscountValue,
                document.SalesDiscountReason),
            document.PaymentIntentId);
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static object ToDocument(CheckoutRequest request)
    {
        var lines = request.Lines
            .Select(line => new CanonicalLine(
                line.ProductId,
                line.Quantity,
                line.LineDiscountAmount,
                NormalizeText(line.Notes),
                line.Modifiers
                    .OrderBy(modifier => modifier.ModifierId)
                    .ThenBy(modifier => modifier.Quantity)
                    .Select(modifier => new CanonicalModifier(modifier.ModifierId, modifier.Quantity))
                    .ToArray()))
            .OrderBy(line => line.ProductId)
            .ThenBy(line => line.Quantity)
            .ThenBy(line => line.LineDiscountAmount)
            .ThenBy(line => line.Notes, StringComparer.Ordinal)
            .ThenBy(line => JsonSerializer.Serialize(line.Modifiers, Options), StringComparer.Ordinal)
            .ToArray();

        if (request.PaymentIntentId.HasValue)
        {
            return new CanonicalDocument(
                4,
                request.PaymentMethod,
                request.CashReceived,
                request.ConfirmedPaymentAmount,
                request.CustomerId,
                request.RestaurantTableId,
                NormalizeCode(request.DiscountCode),
                NormalizeText(request.Notes),
                request.HeldSaleId,
                request.PaymentIntentId,
                request.SalesDiscount.Type,
                request.SalesDiscount.Value,
                NormalizeText(request.SalesDiscount.Reason),
                lines);
        }

        if (request.SalesDiscount.Type == SalesDiscountType.None)
        {
            if (request.HeldSaleId is null)
            {
                return new LegacyCanonicalDocument(
                    1,
                    request.PaymentMethod,
                    request.CashReceived,
                    request.ConfirmedPaymentAmount,
                    request.CustomerId,
                    request.RestaurantTableId,
                    NormalizeCode(request.DiscountCode),
                    NormalizeText(request.Notes),
                    lines);
            }

            return new
            {
                version = 2,
                paymentMethod = request.PaymentMethod,
                cashReceived = request.CashReceived,
                confirmedPaymentAmount = request.ConfirmedPaymentAmount,
                customerId = request.CustomerId,
                restaurantTableId = request.RestaurantTableId,
                discountCode = NormalizeCode(request.DiscountCode),
                notes = NormalizeText(request.Notes),
                heldSaleId = request.HeldSaleId,
                lines
            };
        }

        return new CanonicalDocument(
            3,
            request.PaymentMethod,
            request.CashReceived,
            request.ConfirmedPaymentAmount,
            request.CustomerId,
            request.RestaurantTableId,
            NormalizeCode(request.DiscountCode),
            NormalizeText(request.Notes),
            request.HeldSaleId,
            null,
            request.SalesDiscount.Type,
            request.SalesDiscount.Value,
            NormalizeText(request.SalesDiscount.Reason),
            lines);
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? NormalizeCode(string? value) =>
        NormalizeText(value)?.ToUpperInvariant();

    private sealed record CanonicalDocument(
        int Version,
        PaymentMethod PaymentMethod,
        long CashReceived,
        long ConfirmedPaymentAmount,
        int? CustomerId,
        int? RestaurantTableId,
        string? DiscountCode,
        string? Notes,
        int? HeldSaleId,
        int? PaymentIntentId,
        SalesDiscountType SalesDiscountType,
        long SalesDiscountValue,
        string? SalesDiscountReason,
        IReadOnlyList<CanonicalLine> Lines);

    private sealed record LegacyCanonicalDocument(
        int Version,
        PaymentMethod PaymentMethod,
        long CashReceived,
        long ConfirmedPaymentAmount,
        int? CustomerId,
        int? RestaurantTableId,
        string? DiscountCode,
        string? Notes,
        IReadOnlyList<CanonicalLine> Lines);

    private sealed record CanonicalLine(
        int ProductId,
        int Quantity,
        long LineDiscountAmount,
        string? Notes,
        IReadOnlyList<CanonicalModifier> Modifiers);

    private sealed record CanonicalModifier(int ModifierId, int Quantity);
}
