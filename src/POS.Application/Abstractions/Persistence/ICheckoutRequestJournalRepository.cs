using POS.Domain.Entities;

namespace POS.Application.Abstractions.Persistence;

public interface ICheckoutRequestJournalRepository
{
    Task AddAsync(CheckoutRequestJournal journal, CancellationToken cancellationToken = default);
    Task<CheckoutRequestJournal?> GetTrackedAsync(Guid clientRequestId, CancellationToken cancellationToken = default);
    Task ReloadTrackedAsync(
        CheckoutRequestJournal journal,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    Task<CheckoutRequestJournal?> GetReadOnlyAsync(Guid clientRequestId, CancellationToken cancellationToken = default);
    Task<CheckoutRequestJournal?> GetByOrderIdReadOnlyAsync(int orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckoutRequestJournal>> GetActiveRecoveryAsync(
        int preparedByUserId, int limit, CancellationToken cancellationToken = default);
}
