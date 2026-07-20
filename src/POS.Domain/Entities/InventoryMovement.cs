using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Một bản ghi lịch sử biến động tồn kho bất biến.
///
/// Sau khi được tạo, entity này không cung cấp phương thức
/// chỉnh sửa. Sửa lịch sử kho làm mất khả năng kiểm toán.
///
/// InventoryMovement kế thừa Entity thay vì AuditableEntity vì:
/// - OccurredAtUtc chính là thời điểm nghiệp vụ;
/// - bản ghi không được cập nhật;
/// - không cần concurrency token.
/// </summary>
public sealed class InventoryMovement : Entity
{
    private InventoryMovement()
    {
    }

    public InventoryMovement(
        int productId,
        InventoryMovementType movementType,
        int quantityDelta,
        int quantityBefore,
        int quantityAfter,
        string reason,
        DateTimeOffset occurredAtUtc,
        string? referenceType = null,
        string? referenceId = null,
        int? performedByUserId = null)
    {
        ValidateProductId(productId);
        ValidateMovementType(movementType);

        ValidateStockQuantity(
            quantityBefore,
            nameof(quantityBefore));

        ValidateStockQuantity(
            quantityAfter,
            nameof(quantityAfter));

        ValidateQuantityDelta(quantityDelta);

        ValidateQuantityEquation(
            quantityBefore,
            quantityDelta,
            quantityAfter);

        ValidateDirection(
            movementType,
            quantityBefore,
            quantityDelta);

        ProductId = productId;
        MovementType = movementType;

        QuantityDelta = quantityDelta;
        QuantityBefore = quantityBefore;
        QuantityAfter = quantityAfter;

        Reason = NormalizeReason(reason);

        SetReference(
            referenceType,
            referenceId);

        PerformedByUserId =
            NormalizePerformedByUserId(
                performedByUserId);

        OccurredAtUtc =
            NormalizeOccurredAtUtc(
                occurredAtUtc);
    }

    public int ProductId { get; private set; }

    public InventoryMovementType MovementType
    {
        get;
        private set;
    }

    /// <summary>
    /// Độ thay đổi của tồn kho.
    ///
    /// Dương: tăng kho.
    /// Âm: giảm kho.
    /// Bằng 0: chỉ hợp lệ cho Stocktake.
    /// </summary>
    public int QuantityDelta { get; private set; }

    public int QuantityBefore { get; private set; }

    public int QuantityAfter { get; private set; }

    public string Reason { get; private set; } =
        string.Empty;

    public string? ReferenceType { get; private set; }

    public string? ReferenceId { get; private set; }

    /// <summary>
    /// Người thực hiện.
    ///
    /// Nullable trong giai đoạn chưa hoàn thành Authentication.
    /// Sau khi đăng nhập được triển khai, thao tác thủ công
    /// sẽ luôn truyền UserId hiện tại.
    /// </summary>
    public int? PerformedByUserId { get; private set; }

    public DateTimeOffset OccurredAtUtc
    {
        get;
        private set;
    }

    public Product? Product { get; private set; }

    public bool IsIncrease =>
        QuantityDelta > 0;

    public bool IsDecrease =>
        QuantityDelta < 0;

    private static void ValidateProductId(
        int productId)
    {
        if (productId <= 0)
        {
            throw new DomainException(
                "INVENTORY.INVALID_PRODUCT_ID",
                "Mã sản phẩm của biến động kho không hợp lệ.");
        }
    }

    private static void ValidateMovementType(
        InventoryMovementType movementType)
    {
        if (movementType ==
                InventoryMovementType.Unknown ||
            !Enum.IsDefined(movementType))
        {
            throw new DomainException(
                "INVENTORY.INVALID_MOVEMENT_TYPE",
                "Loại biến động tồn kho không hợp lệ.");
        }
    }

    private static void ValidateStockQuantity(
        int quantity,
        string parameterName)
    {
        if (quantity >
                BusinessRules.Products.MaximumStockQuantity ||
            quantity <
                -BusinessRules.Products.MaximumStockQuantity)
        {
            throw new DomainException(
                "INVENTORY.STOCK_OUT_OF_RANGE",
                $"Giá trị {parameterName} vượt quá giới hạn hệ thống.");
        }
    }

    private static void ValidateQuantityDelta(
        int quantityDelta)
    {
        if (quantityDelta >
                BusinessRules.Inventory.MaximumQuantityDelta ||
            quantityDelta <
                -BusinessRules.Inventory.MaximumQuantityDelta)
        {
            throw new DomainException(
                "INVENTORY.DELTA_OUT_OF_RANGE",
                "Độ thay đổi tồn kho vượt quá giới hạn hệ thống.");
        }
    }

    private static void ValidateQuantityEquation(
        int quantityBefore,
        int quantityDelta,
        int quantityAfter)
    {
        var calculatedAfter =
            (long)quantityBefore +
            quantityDelta;

        if (calculatedAfter != quantityAfter)
        {
            throw new DomainException(
                "INVENTORY.INCONSISTENT_QUANTITIES",
                "Tồn sau phải bằng tồn trước cộng độ thay đổi.");
        }
    }

    private static void ValidateDirection(
        InventoryMovementType movementType,
        int quantityBefore,
        int quantityDelta)
    {
        switch (movementType)
        {
            case InventoryMovementType.StockIn:
            case InventoryMovementType.Refund:
                if (quantityDelta <= 0)
                {
                    throw new DomainException(
                        "INVENTORY.INCREASE_REQUIRED",
                        "Biến động nhập kho phải làm tăng tồn kho.");
                }

                break;

            case InventoryMovementType.StockOut:
            case InventoryMovementType.Sale:
                if (quantityDelta >= 0)
                {
                    throw new DomainException(
                        "INVENTORY.DECREASE_REQUIRED",
                        "Biến động xuất kho phải làm giảm tồn kho.");
                }

                break;

            case InventoryMovementType.Adjustment:
                if (quantityDelta == 0)
                {
                    throw new DomainException(
                        "INVENTORY.ZERO_ADJUSTMENT",
                        "Điều chỉnh kho không được có số lượng bằng 0.");
                }

                break;

            case InventoryMovementType.Stocktake:
                /*
                 * Kiểm kê có thể không phát hiện chênh lệch.
                 * Vẫn lưu movement bằng 0 để chứng minh
                 * lần kiểm kê đã được thực hiện.
                 */
                break;

            case InventoryMovementType.OpeningBalance:
                if (quantityBefore != 0)
                {
                    throw new DomainException(
                        "INVENTORY.INVALID_OPENING_BALANCE",
                        "Tồn đầu kỳ phải bắt đầu từ số lượng 0.");
                }

                if (quantityDelta == 0)
                {
                    throw new DomainException(
                        "INVENTORY.ZERO_OPENING_BALANCE",
                        "Không cần tạo biến động tồn đầu kỳ bằng 0.");
                }

                break;

            default:
                throw new DomainException(
                    "INVENTORY.INVALID_MOVEMENT_TYPE",
                    "Loại biến động tồn kho không hợp lệ.");
        }
    }

    private static string NormalizeReason(
        string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(
                "INVENTORY.REASON_REQUIRED",
                "Lý do biến động tồn kho không được để trống.");
        }

        var normalized =
            reason.Trim();

        if (normalized.Length >
            BusinessRules.Inventory.ReasonMaxLength)
        {
            throw new DomainException(
                "INVENTORY.REASON_TOO_LONG",
                "Lý do biến động tồn kho vượt quá giới hạn.");
        }

        return normalized;
    }

    private void SetReference(
        string? referenceType,
        string? referenceId)
    {
        var normalizedType =
            NormalizeOptionalText(
                referenceType);

        var normalizedId =
            NormalizeOptionalText(
                referenceId);

        if ((normalizedType is null) !=
            (normalizedId is null))
        {
            throw new DomainException(
                "INVENTORY.INCOMPLETE_REFERENCE",
                "Loại tham chiếu và mã tham chiếu phải được cung cấp cùng nhau.");
        }

        if (normalizedType?.Length >
            BusinessRules.Inventory.ReferenceTypeMaxLength)
        {
            throw new DomainException(
                "INVENTORY.REFERENCE_TYPE_TOO_LONG",
                "Loại tham chiếu vượt quá giới hạn.");
        }

        if (normalizedId?.Length >
            BusinessRules.Inventory.ReferenceIdMaxLength)
        {
            throw new DomainException(
                "INVENTORY.REFERENCE_ID_TOO_LONG",
                "Mã tham chiếu vượt quá giới hạn.");
        }

        ReferenceType =
            normalizedType?
                .ToUpperInvariant();

        ReferenceId = normalizedId;
    }

    private static int? NormalizePerformedByUserId(
        int? performedByUserId)
    {
        if (performedByUserId.HasValue &&
            performedByUserId.Value <= 0)
        {
            throw new DomainException(
                "INVENTORY.INVALID_PERFORMED_BY_USER_ID",
                "Người thực hiện biến động kho không hợp lệ.");
        }

        return performedByUserId;
    }

    private static DateTimeOffset NormalizeOccurredAtUtc(
        DateTimeOffset occurredAtUtc)
    {
        if (occurredAtUtc == default)
        {
            throw new DomainException(
                "INVENTORY.OCCURRED_TIME_REQUIRED",
                "Thời điểm biến động kho không được để trống.");
        }

        return occurredAtUtc.ToUniversalTime();
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}