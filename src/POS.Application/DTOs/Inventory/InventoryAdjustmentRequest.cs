using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Inventory;

/// <summary>
/// Yêu cầu điều chỉnh tồn kho thủ công.
///
/// Ý nghĩa của Quantity:
///
/// StockIn:
///     số lượng nhập, phải lớn hơn 0.
///
/// StockOut:
///     số lượng xuất, phải lớn hơn 0.
///
/// Adjustment:
///     độ thay đổi có dấu.
///     Ví dụ +5 hoặc -3.
///
/// Stocktake:
///     số tồn kho thực tế sau kiểm kê.
/// </summary>
public sealed class InventoryAdjustmentRequest
{
    public InventoryAdjustmentRequest(
        int productId,
        InventoryMovementType movementType,
        int quantity,
        string reason,
        string? referenceType = null,
        string? referenceId = null)
    {
        if (productId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(productId),
                productId,
                "Mã sản phẩm phải lớn hơn 0.");
        }

        ValidateManualMovementType(
            movementType);

        ValidateQuantity(
            movementType,
            quantity);

        ProductId = productId;
        MovementType = movementType;
        Quantity = quantity;

        Reason =
            NormalizeRequiredText(
                reason,
                nameof(reason),
                BusinessRules.Inventory.ReasonMaxLength);

        var normalizedReferenceType =
            NormalizeOptionalText(
                referenceType,
                BusinessRules.Inventory.ReferenceTypeMaxLength,
                nameof(referenceType));

        var normalizedReferenceId =
            NormalizeOptionalText(
                referenceId,
                BusinessRules.Inventory.ReferenceIdMaxLength,
                nameof(referenceId));

        if ((normalizedReferenceType is null) !=
            (normalizedReferenceId is null))
        {
            throw new ArgumentException(
                "Loại tham chiếu và mã tham chiếu phải được cung cấp cùng nhau.");
        }

        ReferenceType =
            normalizedReferenceType?
                .ToUpperInvariant();

        ReferenceId =
            normalizedReferenceId;
    }

    public int ProductId { get; }

    public InventoryMovementType MovementType { get; }

    public int Quantity { get; }

    public string Reason { get; }

    public string? ReferenceType { get; }

    public string? ReferenceId { get; }

    private static void ValidateManualMovementType(
        InventoryMovementType movementType)
    {
        if (!Enum.IsDefined(movementType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(movementType),
                movementType,
                "Loại biến động tồn kho không hợp lệ.");
        }

        if (movementType is not
            (
                InventoryMovementType.StockIn or
                InventoryMovementType.StockOut or
                InventoryMovementType.Adjustment or
                InventoryMovementType.Stocktake
            ))
        {
            throw new ArgumentOutOfRangeException(
                nameof(movementType),
                movementType,
                "Loại biến động này không được tạo thủ công.");
        }
    }

    private static void ValidateQuantity(
        InventoryMovementType movementType,
        int quantity)
    {
        switch (movementType)
        {
            case InventoryMovementType.StockIn:
            case InventoryMovementType.StockOut:
                if (quantity <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(quantity),
                        quantity,
                        "Số lượng nhập hoặc xuất phải lớn hơn 0.");
                }

                if (quantity >
                    BusinessRules.Inventory.MaximumQuantityDelta)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(quantity),
                        quantity,
                        "Số lượng biến động vượt quá giới hạn.");
                }

                break;

            case InventoryMovementType.Adjustment:
                if (quantity == 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(quantity),
                        quantity,
                        "Số lượng điều chỉnh không được bằng 0.");
                }

                if (quantity >
                        BusinessRules.Inventory.MaximumQuantityDelta ||
                    quantity <
                        -BusinessRules.Inventory.MaximumQuantityDelta)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(quantity),
                        quantity,
                        "Số lượng điều chỉnh vượt quá giới hạn.");
                }

                break;

            case InventoryMovementType.Stocktake:
                if (quantity >
                        BusinessRules.Products.MaximumStockQuantity ||
                    quantity <
                        -BusinessRules.Products.MaximumStockQuantity)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(quantity),
                        quantity,
                        "Số tồn kiểm kê vượt quá giới hạn.");
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(movementType),
                    movementType,
                    "Loại biến động tồn kho không hợp lệ.");
        }
    }

    private static string NormalizeRequiredText(
        string value,
        string parameterName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Giá trị không được để trống.",
                parameterName);
        }

        var normalized =
            value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                "Giá trị vượt quá giới hạn ký tự.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized =
            value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                "Giá trị vượt quá giới hạn ký tự.",
                parameterName);
        }

        return normalized;
    }
}