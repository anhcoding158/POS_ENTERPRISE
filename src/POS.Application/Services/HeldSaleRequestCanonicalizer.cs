using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.HeldSales;
using POS.Application.DTOs.Checkout;

namespace POS.Application.Services;

public sealed class HeldSaleRequestCanonicalizer : IHeldSaleRequestCanonicalizer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CanonicalHeldSaleRequest Canonicalize(CreateHeldSaleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = new
        {
            version = 2,
            label = Normalize(request.Label),
            notes = Normalize(request.Notes),
            discount = new
            {
                type = (request.SalesDiscount ?? SalesDiscountRequest.None).Type,
                value = (request.SalesDiscount ?? SalesDiscountRequest.None).Value,
                reason = Normalize((request.SalesDiscount ?? SalesDiscountRequest.None).Reason)
            },
            lines = (request.Lines ?? [])
                .Select(line => new
                {
                    productId = line.ProductId,
                    quantity = line.Quantity,
                    notes = Normalize(line.Notes)
                })
                .OrderBy(line => line.productId)
                .ThenBy(line => line.quantity)
                .ThenBy(line => line.notes, StringComparer.Ordinal)
                .ToArray()
        };
        var json = JsonSerializer.Serialize(document, Options);
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return new(json, fingerprint);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
}
