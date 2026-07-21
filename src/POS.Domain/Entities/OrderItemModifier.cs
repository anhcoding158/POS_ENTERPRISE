using POS.Domain.Common;
using POS.Domain.Constants;

namespace POS.Domain.Entities;

/// <summary>
/// Snapshot modifier/topping thuộc một dòng đơn hàng.
///
/// Dữ liệu tên, nhóm và giá được đóng băng tại thời điểm bán.
/// Việc đổi tên hoặc đổi giá Modifier sau này không làm thay đổi
/// hóa đơn lịch sử.
/// </summary>
public sealed class OrderItemModifier :
    Entity
{
    private OrderItemModifier()
    {
    }

    internal OrderItemModifier(
        int modifierId,
        int modifierGroupId,
        string modifierGroupName,
        string modifierName,
        int quantity,
        long unitAdditionalPrice)
    {
        SetModifierId(
            modifierId);

        SetModifierGroupId(
            modifierGroupId);

        SetModifierGroupName(
            modifierGroupName);

        SetModifierName(
            modifierName);

        SetQuantity(
            quantity);

        SetUnitAdditionalPrice(
            unitAdditionalPrice);
    }

    public int OrderItemId
    {
        get;
        private set;
    }

    public int ModifierId
    {
        get;
        private set;
    }

    public int ModifierGroupId
    {
        get;
        private set;
    }

    public string ModifierGroupName
    {
        get;
        private set;
    } = string.Empty;

    public string ModifierName
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>
    /// Số lượng modifier áp dụng trên một đơn vị sản phẩm.
    ///
    /// Ví dụ một ly có 2 phần trân châu.
    /// </summary>
    public int Quantity
    {
        get;
        private set;
    }

    public long UnitAdditionalPrice
    {
        get;
        private set;
    }

    public OrderItem? OrderItem
    {
        get;
        private set;
    }

    public long AmountPerProductUnit =>
        SafeMultiply(
            UnitAdditionalPrice,
            Quantity);

    internal void ChangeQuantity(
        int quantity)
    {
        SetQuantity(
            quantity);
    }

    private void SetModifierId(
        int modifierId)
    {
        if (modifierId <= 0)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.INVALID_MODIFIER_ID",
                "Modifier không hợp lệ.");
        }

        ModifierId =
            modifierId;
    }

    private void SetModifierGroupId(
        int modifierGroupId)
    {
        if (modifierGroupId <= 0)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.INVALID_GROUP_ID",
                "Nhóm modifier không hợp lệ.");
        }

        ModifierGroupId =
            modifierGroupId;
    }

    private void SetModifierGroupName(
        string modifierGroupName)
    {
        if (string.IsNullOrWhiteSpace(
                modifierGroupName))
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.GROUP_NAME_REQUIRED",
                "Tên nhóm modifier không được để trống.");
        }

        var normalized =
            modifierGroupName.Trim();

        if (normalized.Length >
            BusinessRules.ModifierGroups
                .NameMaxLength)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.GROUP_NAME_TOO_LONG",
                "Tên nhóm modifier vượt quá giới hạn.");
        }

        ModifierGroupName =
            normalized;
    }

    private void SetModifierName(
        string modifierName)
    {
        if (string.IsNullOrWhiteSpace(
                modifierName))
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.NAME_REQUIRED",
                "Tên modifier không được để trống.");
        }

        var normalized =
            modifierName.Trim();

        if (normalized.Length >
            BusinessRules.Modifiers
                .NameMaxLength)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.NAME_TOO_LONG",
                "Tên modifier vượt quá giới hạn.");
        }

        ModifierName =
            normalized;
    }

    private void SetQuantity(
        int quantity)
    {
        if (quantity <= 0 ||
            quantity >
            BusinessRules.Orders
                .MaximumLineQuantity)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.INVALID_QUANTITY",
                "Số lượng modifier không hợp lệ.");
        }

        Quantity =
            quantity;
    }

    private void SetUnitAdditionalPrice(
        long unitAdditionalPrice)
    {
        if (unitAdditionalPrice < 0 ||
            unitAdditionalPrice >
            BusinessRules.Modifiers
                .MaximumAdditionalPrice)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.INVALID_PRICE",
                "Giá modifier không hợp lệ.");
        }

        UnitAdditionalPrice =
            unitAdditionalPrice;
    }

    private static long SafeMultiply(
        long left,
        int right)
    {
        try
        {
            var result =
                checked(
                    left * right);

            if (result >
                BusinessRules.Orders
                    .MaximumOrderAmount)
            {
                throw new DomainException(
                    "ORDER_ITEM_MODIFIER.AMOUNT_OVERFLOW",
                    "Thành tiền modifier vượt giới hạn.");
            }

            return result;
        }
        catch (OverflowException exception)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.AMOUNT_OVERFLOW",
                "Thành tiền modifier vượt giới hạn.",
                exception);
        }
    }
}