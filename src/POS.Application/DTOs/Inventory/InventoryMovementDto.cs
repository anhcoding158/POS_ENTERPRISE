using POS.Domain.Enums;

namespace POS.Application.DTOs.Inventory;

/// <summary>
/// Một dòng lịch sử biến động tồn kho.
/// </summary>
public sealed record InventoryMovementDto(
    int Id,
    int ProductId,
    string ProductCode,
    string ProductName,
    string UnitName,
    InventoryMovementType MovementType,
    int QuantityBefore,
    int QuantityDelta,
    int QuantityAfter,
    string Reason,
    string? ReferenceType,
    string? ReferenceId,
    int? PerformedByUserId,
    DateTimeOffset OccurredAtUtc);