using System.Security.Cryptography;
using POS.Application.Abstractions.Orders;

namespace POS.Infrastructure.Orders;

/// <summary>
/// Sinh mã hóa đơn gồm thời gian UTC và hậu tố ngẫu nhiên.
///
/// Ví dụ:
/// HD-20260721-163015125-A12B45CD
/// </summary>
public sealed class OrderCodeGenerator :
    IOrderCodeGenerator
{
    public string Generate(
        DateTimeOffset utcNow)
    {
        if (utcNow == default)
        {
            throw new ArgumentException(
                "Thời điểm sinh mã đơn không hợp lệ.",
                nameof(utcNow));
        }

        Span<byte> randomBytes =
            stackalloc byte[4];

        RandomNumberGenerator.Fill(
            randomBytes);

        var randomSuffix =
            Convert.ToHexString(
                randomBytes);

        var normalizedUtc =
            utcNow.ToUniversalTime();

        return
            $"HD-{normalizedUtc:yyyyMMdd-HHmmssfff}-" +
            $"{randomSuffix}";
    }
}