using POS.Application.Common;
using POS.Application.DTOs.Payments;

namespace POS.Application.Abstractions.Services;

public interface IPaymentIntentService
{
    Task<Result<PaymentIntentDto>> CreateAsync(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentDto>> MarkPresentedAsync(
        int paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentDto>> ConfirmReceivedAsync(
        int paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentDto>> CancelAsync(
        int paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentDto>> ExpireAsync(
        int paymentIntentId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentDto>> GetByIdAsync(
        int paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PaymentIntentPendingDto>>> GetPendingAsync(
        int limit = 25,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PaymentIntentPendingDto>>> RecoverPendingAsync(
        int limit = 25,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentManualResolutionDto>> ResolveManuallyAsync(
        ResolvePaymentIntentManuallyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PaymentIntentManualResolutionDto>>> GetManualResolutionHistoryAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}
