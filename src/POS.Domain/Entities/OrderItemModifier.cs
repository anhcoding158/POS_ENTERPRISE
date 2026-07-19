using POS.Domain.Common;
using POS.Domain.Constants;

namespace POS.Domain.Entities;

/// <summary>
/// Snapshot của một modifier/topping được chọn tại thời điểm bán.
///
/// Dữ liệu tên và giá được lưu lại để hóa đơn cũ không thay đổi
/// khi modifier trong danh mục được chỉnh sửa sau này.
/// </summary>
public sealed class OrderItemModifier : Entity
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
        SetModifierId(modifierId);
        SetModifierGroupId(modifierGroupId);
        SetModifierGroupName(modifierGroupName);
        SetModifierName(modifierName);
        SetQuantity(quantity);
        SetUnitAdditionalPrice(unitAdditionalPrice);
    }

    public int OrderItemId { get; private set; }

    public int ModifierId { get; private set; }

    public int ModifierGroupId { get; private set; }

    public string ModifierGroupName { get; private set; } =
        string.Empty;

    public string ModifierName { get; private set; } =
        string.Empty;

    /// <summary>
    /// Số lượng modifier áp dụng trên mỗi đơn vị sản phẩm.
    ///
    /// Ví dụ một ly có 2 phần trân châu thì Quantity = 2.
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Giá cộng thêm của một modifier tại thời điểm bán.
    /// </summary>
    public long UnitAdditionalPrice { get; private set; }

    public OrderItem? OrderItem { get; private set; }

    /// <summary>
    /// Tổng tiền modifier trên một đơn vị sản phẩm.
    /// </summary>
    public long AmountPerProductUnit
    {
        get
        {
            try
            {
                return checked(
                    UnitAdditionalPrice * Quantity);
            }
            catch (OverflowException exception)
            {
                throw new DomainException(
                    "ORDER_ITEM_MODIFIER.AMOUNT_OVERFLOW",
                    "Tổng tiền lựa chọn bổ sung vượt giới hạn.",
                    exception);
            }
        }
    }

    internal void ChangeQuantity(int quantity)
    {
        SetQuantity(quantity);
    }

    private void SetModifierId(int modifierId)
    {
        if (modifierId <= 0)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.INVALID_MODIFIER_ID",
                "Modifier không hợp lệ.");
        }

        ModifierId = modifierId;
    }

    private void SetModifierGroupId(int modifierGroupId)
    {
        if (modifierGroupId <= 0)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.INVALID_GROUP_ID",
                "Nhóm modifier không hợp lệ.");
        }

        ModifierGroupId = modifierGroupId;
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

        var trimmed = modifierGroupName.Trim();

        if (trimmed.Length >
            BusinessRules.ModifierGroups.NameMaxLength)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.GROUP_NAME_TOO_LONG",
                "Tên nhóm modifier vượt quá giới hạn.");
        }

        ModifierGroupName = trimmed;
    }

    private void SetModifierName(string modifierName)
    {
        if (string.IsNullOrWhiteSpace(modifierName))
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.NAME_REQUIRED",
                "Tên modifier không được để trống.");
        }

        var trimmed = modifierName.Trim();

        if (trimmed.Length >
            BusinessRules.Modifiers.NameMaxLength)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.NAME_TOO_LONG",
                "Tên modifier vượt quá giới hạn.");
        }

        ModifierName = trimmed;
    }

    private void SetQuantity(int quantity)
    {
        if (quantity <= 0 ||
            quantity >
            BusinessRules.Orders.MaximumLineQuantity)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.INVALID_QUANTITY",
                "Số lượng modifier không hợp lệ.");
        }

        Quantity = quantity;
    }

    private void SetUnitAdditionalPrice(
        long unitAdditionalPrice)
    {
        if (unitAdditionalPrice < 0 ||
            unitAdditionalPrice >
            BusinessRules.Modifiers.MaximumAdditionalPrice)
        {
            throw new DomainException(
                "ORDER_ITEM_MODIFIER.INVALID_PRICE",
                "Giá cộng thêm của modifier không hợp lệ.");
        }

        UnitAdditionalPrice = unitAdditionalPrice;
    }
}