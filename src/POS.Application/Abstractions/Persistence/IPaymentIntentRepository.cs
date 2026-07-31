using POS.Domain.Entities;

namespace POS.Application.Abstractions.Persistence;

public interface IPaymentIntentRepository
{
    Task AddAsync(PaymentIntent intent, CancellationToken cancellationToken = default);
    Task<PaymentIntent?> GetByIdAsync(int id, bool tracked, CancellationToken cancellationToken = default);
    Task<PaymentIntent?> GetByClientRequestIdAsync(Guid clientRequestId, bool tracked, CancellationToken cancellationToken = default);
    Task<PaymentIntent?> GetByCompletedOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<PaymentIntent?> GetActiveByHeldSaleIdAsync(
        int heldSaleId, bool tracked, CancellationToken cancellationToken = default) =>
        Task.FromResult<PaymentIntent?>(null);
    Task ReloadTrackedAsync(PaymentIntent intent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentIntent>> GetPendingAsync(int createdByUserId, int limit, CancellationToken cancellationToken = default);
    Task<PaymentIntentManualResolution?> GetResolutionAsync(int paymentIntentId, CancellationToken cancellationToken = default);
    Task AddResolutionAsync(PaymentIntentManualResolution resolution, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentIntentManualResolution>> GetResolutionHistoryAsync(int limit, CancellationToken cancellationToken = default);
}
