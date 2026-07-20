using POS.Domain.Enums;

namespace POS.Application.DTOs.Inventory;

/// <summary>
/// Kết quả sau khi biến động kho được commit thành công.
/// </summary>
public sealed record InventoryAdjustmentResultDto(
    int MovementId,
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