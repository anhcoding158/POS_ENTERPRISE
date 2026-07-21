using POS.Application.Common;
using POS.Application.DTOs.Checkout;

namespace POS.Application.Abstractions.Services;

/// <summary>
/// Use case hoàn tất một giao dịch bán hàng.
///
/// Implementation phải đảm bảo:
/// - xác thực và phân quyền;
/// - giá được lấy lại từ database;
/// - Order, tồn kho và lịch sử kho được lưu nguyên tử;
/// - không trả thành công nếu transaction chưa commit.
/// </summary>
public interface ICheckoutService
{
    Task<Result<CheckoutResultDto>> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default);
}