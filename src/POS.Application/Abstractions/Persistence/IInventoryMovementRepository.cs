using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Truy cập dữ liệu lịch sử tồn kho.
///
/// Repository không được làm lộ IQueryable ra Application.
/// </summary>
public interface IInventoryMovementRepository
{
    Task<InventoryMovement?> GetByIdAsync(
        int movementId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InventoryMovement>> SearchAsync(
        int? productId,
        InventoryMovementType? movementType,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? referenceType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm movement mới nhưng chưa lưu database.
    /// </summary>
    Task AddAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken = default);
}