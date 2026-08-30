using POS.Application.Common;
using POS.Application.DTOs.Inventory;
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
        string? productSearchTerm = null,
        CancellationToken cancellationToken = default);

    Task<InventoryMovementSummaryDto> GetSummaryAsync(
        int? productId,
        InventoryMovementType? movementType,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? referenceType,
        string? productSearchTerm = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryMovement>> ExportAsync(
        int? productId,
        InventoryMovementType? movementType,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? referenceType,
        string? productSearchTerm,
        int maximumRows,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Repository chưa hỗ trợ xuất dữ liệu.");

    /// <summary>
    /// Thêm movement mới nhưng chưa lưu database.
    /// </summary>
    Task AddAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken = default);
}
