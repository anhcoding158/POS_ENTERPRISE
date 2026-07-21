namespace POS.Application.Abstractions.Orders;

/// <summary>
/// Sinh mã đơn hàng mới.
///
/// Generator chỉ tạo mã ứng viên.
/// CheckoutService vẫn phải kiểm tra unique bằng repository
/// và database unique index.
/// </summary>
public interface IOrderCodeGenerator
{
    string Generate(
        DateTimeOffset utcNow);
}