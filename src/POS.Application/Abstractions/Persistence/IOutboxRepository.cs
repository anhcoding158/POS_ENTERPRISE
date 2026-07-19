using POS.Domain.Entities;

namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Truy cập dữ liệu Outbox Pattern.
///
/// Outbox đảm bảo dữ liệu nghiệp vụ và sự kiện đồng bộ
/// được lưu trong cùng transaction.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Lấy OutboxMessage theo khóa Guid.
    /// </summary>
    Task<OutboxMessage?> GetByIdAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy một lô message có thể xử lý.
    ///
    /// Infrastructure chỉ lấy các message:
    /// - Pending;
    /// - hoặc Failed nhưng chưa vượt giới hạn retry.
    ///
    /// Kết quả phải được sắp xếp theo CreatedAtUtc tăng dần.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetProcessableBatchAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm message mới nhưng chưa lưu database.
    ///
    /// Message thường được thêm cùng Order và commit
    /// trong một transaction duy nhất.
    /// </summary>
    Task AddAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);
}