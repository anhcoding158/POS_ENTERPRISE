using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Payments;

namespace POS.Application.Services;

public sealed class AuthorizedPaymentIntentService(
    IPaymentIntentService inner,
    IPermissionService permissions) : IPaymentIntentService
{
    public Task<Result<PaymentIntentDto>> CreateAsync(CreatePaymentIntentRequest request, CancellationToken cancellationToken = default) =>
        Authorize(() => inner.CreateAsync(request, cancellationToken));

    public Task<Result<PaymentIntentDto>> MarkPresentedAsync(int paymentIntentId, CancellationToken cancellationToken = default) =>
        Authorize(() => inner.MarkPresentedAsync(paymentIntentId, cancellationToken));

    public Task<Result<PaymentIntentDto>> ConfirmReceivedAsync(int paymentIntentId, CancellationToken cancellationToken = default) =>
        Authorize(() => inner.ConfirmReceivedAsync(paymentIntentId, cancellationToken));

    public Task<Result<PaymentIntentDto>> CancelAsync(int paymentIntentId, CancellationToken cancellationToken = default) =>
        Authorize(() => inner.CancelAsync(paymentIntentId, cancellationToken));

    public Task<Result<PaymentIntentDto>> ExpireAsync(int paymentIntentId, string reason, CancellationToken cancellationToken = default) =>
        Authorize(() => inner.ExpireAsync(paymentIntentId, reason, cancellationToken));

    public Task<Result<PaymentIntentDto>> GetByIdAsync(int paymentIntentId, CancellationToken cancellationToken = default) =>
        Authorize(() => inner.GetByIdAsync(paymentIntentId, cancellationToken));

    public Task<Result<IReadOnlyList<PaymentIntentPendingDto>>> GetPendingAsync(int limit = 25, CancellationToken cancellationToken = default) =>
        Authorize(() => inner.GetPendingAsync(limit, cancellationToken));

    public Task<Result<IReadOnlyList<PaymentIntentPendingDto>>> RecoverPendingAsync(int limit = 25, CancellationToken cancellationToken = default) =>
        Authorize(() => inner.RecoverPendingAsync(limit, cancellationToken));

    public Task<Result<PaymentIntentManualResolutionDto>> ResolveManuallyAsync(
        ResolvePaymentIntentManuallyRequest request, CancellationToken cancellationToken = default) =>
        Authorize(() => inner.ResolveManuallyAsync(request, cancellationToken));

    public Task<Result<IReadOnlyList<PaymentIntentManualResolutionDto>>> GetManualResolutionHistoryAsync(
        int limit = 100, CancellationToken cancellationToken = default) =>
        Authorize(() => inner.GetManualResolutionHistoryAsync(limit, cancellationToken));

    private Task<Result<T>> Authorize<T>(Func<Task<Result<T>>> action)
    {
        var authorization = permissions.Authorize(SystemCapability.UseCheckout);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<T>(authorization.AppError))
            : action();
    }
}
