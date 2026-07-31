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
    Task<Result<CheckoutPreparationDto>> PrepareCheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<CheckoutPreparationDto>(
            new AppError("CHECKOUT.IDEMPOTENCY_NOT_SUPPORTED", "Service chưa hỗ trợ durable checkout.")));

    Task<Result<CheckoutResultDto>> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CheckoutResultDto>> RetryConfirmedPaymentIntentAsync(
        int paymentIntentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<CheckoutResultDto>(
            new AppError("PAYMENT_INTENT.RETRY_NOT_SUPPORTED",
                "Service chưa hỗ trợ retry PaymentIntent.")));

    Task<Result<IReadOnlyList<CheckoutRecoveryDto>>> GetCheckoutRecoveryAsync(
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<IReadOnlyList<CheckoutRecoveryDto>>(
            new AppError("CHECKOUT.IDEMPOTENCY_NOT_SUPPORTED", "Service chưa hỗ trợ recovery checkout.")));

    Task<Result> AcknowledgeCheckoutAsync(
        Guid clientRequestId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure(
            new AppError("CHECKOUT.IDEMPOTENCY_NOT_SUPPORTED", "Service chưa hỗ trợ acknowledgment checkout.")));

    Task<Result> AbandonCheckoutAsync(
        Guid clientRequestId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure(
            new AppError("CHECKOUT.IDEMPOTENCY_NOT_SUPPORTED", "Service chưa hỗ trợ abandon checkout.")));
}

