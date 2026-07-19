namespace POS.Application.Abstractions.Orders;

/// <summary>
/// Sinh mã đơn hàng thân thiện để in trên hóa đơn.
///
/// Ví dụ định dạng dự kiến:
/// HD-20260719-103015-482
/// </summary>
public interface IOrderCodeGenerator
{
    string Generate(DateTimeOffset utcNow);
}