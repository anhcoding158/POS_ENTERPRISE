using POS.Application.Common;
using POS.Application.DTOs.Checkout;

namespace POS.Application.Abstractions.Services;

/// <summary>
/// Use case trung tâm của quá trình bán hàng.
/// </summary>
public interface ICheckoutService
{
    /// <summary>
    /// Kiểm tra giỏ hàng, đọc giá thật từ database,
    /// kiểm tra tồn kho, tạo Order, thanh toán,
    /// lưu Outbox và commit trong cùng transaction.
    /// </summary>
    Task<Result<CheckoutResultDto>> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default);
}