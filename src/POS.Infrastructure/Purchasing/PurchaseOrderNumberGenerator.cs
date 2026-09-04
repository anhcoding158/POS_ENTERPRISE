using System.Security.Cryptography;
using POS.Application.Abstractions.Purchasing;

namespace POS.Infrastructure.Purchasing;

public sealed class PurchaseOrderNumberGenerator : IPurchaseOrderNumberGenerator
{
    public string Generate(DateTimeOffset utcNow)
    {
        if (utcNow == default)
            throw new ArgumentException("Thời điểm UTC không hợp lệ.", nameof(utcNow));

        Span<byte> randomBytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(randomBytes);
        var suffix = Convert.ToHexString(randomBytes);
        var normalizedUtc = utcNow.ToUniversalTime();
        return $"PO-{normalizedUtc:yyyyMMdd-HHmmssfff}-{suffix}";
    }
}
