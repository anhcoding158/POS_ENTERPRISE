using System.Numerics;
using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Domain.Services;

public static class SalesDiscountCalculator
{
    public const int MaximumReasonLength = 200;

    public static string? NormalizeReason(SalesDiscountType type, string? reason)
    {
        if (!Enum.IsDefined(type))
            throw Failure("SALES_DISCOUNT.TYPE_INVALID", "Loại giảm giá không hợp lệ.");
        if (type == SalesDiscountType.None)
            return null;
        var normalized = string.IsNullOrWhiteSpace(reason)
            ? null
            : string.Join(' ', reason.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized is null)
            throw Failure("SALES_DISCOUNT.REASON_REQUIRED", "Phải nhập lý do giảm giá.");
        if (normalized.Length > MaximumReasonLength)
            throw Failure("SALES_DISCOUNT.REASON_TOO_LONG", "Lý do giảm giá không được vượt quá 200 ký tự.");
        return normalized;
    }

    public static long Resolve(long subtotal, SalesDiscountType type, long value, string? reason)
    {
        if (subtotal <= 0)
            throw Failure("SALES_DISCOUNT.SUBTOTAL_INVALID", "Tạm tính phải lớn hơn 0.");
        _ = NormalizeReason(type, reason);
        if (type == SalesDiscountType.None)
        {
            if (value != 0)
                throw Failure("SALES_DISCOUNT.VALUE_INVALID", "Không giảm giá phải có giá trị bằng 0.");
            return 0;
        }
        if (value <= 0)
            throw Failure("SALES_DISCOUNT.VALUE_INVALID", "Giá trị giảm giá phải lớn hơn 0.");

        var amount = type switch
        {
            SalesDiscountType.FixedAmount when value <= subtotal => value,
            SalesDiscountType.FixedAmount =>
                throw Failure("SALES_DISCOUNT.EXCEEDS_SUBTOTAL", "Giảm giá vượt quá tạm tính."),
            SalesDiscountType.Percentage when value <= 10_000 =>
                checked((long)((BigInteger)subtotal * value / 10_000)),
            SalesDiscountType.Percentage =>
                throw Failure("SALES_DISCOUNT.PERCENTAGE_INVALID", "Phần trăm giảm phải từ trên 0% đến 100%."),
            _ => throw Failure("SALES_DISCOUNT.TYPE_INVALID", "Loại giảm giá không hợp lệ.")
        };
        if (amount <= 0)
            throw Failure("SALES_DISCOUNT.AMOUNT_ZERO", "Giảm giá phải tạo ra ít nhất 1 VND.");
        if (amount >= subtotal)
            throw Failure("SALES_DISCOUNT.ZERO_TOTAL_NOT_SUPPORTED", "Checkout đơn 0 đồng chưa được hỗ trợ.");
        return amount;
    }

    private static DomainException Failure(string code, string message) => new(code, message);
}
