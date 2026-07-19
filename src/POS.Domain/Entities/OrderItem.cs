using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;
using static POS.Domain.Constants.BusinessRules;

namespace POS.Domain.Entities;

/// <summary>
/// Một dòng sản phẩm trong đơn hàng.
///
/// Dòng hàng lưu snapshot mã, tên, đơn vị, giá vốn và giá bán
/// tại thời điểm bán để dữ liệu lịch sử luôn chính xác.
/// </summary>
public sealed class OrderItem : AuditableEntity
{
    private readonly List<OrderItemModifier> _modifiers = [];

    private OrderItem()
    {
    }

    internal OrderItem(
        int productId,
        string productCode,
        string productName,
        string unitName,
        int quantity,
        long unitCostPrice,
        long unitSalePrice,
        DateTimeOffset utcNow,
        string? notes = null)
    {
        SetProductId(productId);
        SetProductCode(productCode);
        SetProductName(productName);
        SetUnitName(unitName);
        SetQuantity(quantity);
        SetUnitCostPrice(unitCostPrice);
        SetUnitSalePrice(unitSalePrice);
        SetNotes(notes);

        Status = OrderItemStatus.Active;

        MarkCreated(utcNow);
    }

    public int OrderId { get; private set; }

    public int ProductId { get; private set; }

    public string ProductCode { get; private set; } =
        string.Empty;

    public string ProductName { get; private set; } =
        string.Empty;

    public string UnitName { get; private set; } =
        string.Empty;

    public int Quantity { get; private set; }

    /// <summary>
    /// Giá vốn của một đơn vị tại thời điểm bán.
    /// </summary>
    public long UnitCostPrice { get; private set; }

    /// <summary>
    /// Giá bán của một đơn vị tại thời điểm bán,
    /// chưa gồm modifier.
    /// </summary>
    public long UnitSalePrice { get; private set; }

    /// <summary>
    /// Giảm giá trực tiếp trên dòng hàng.
    /// </summary>
    public long LineDiscountAmount { get; private set; }

    public string? Notes { get; private set; }

    public OrderItemStatus Status { get; private set; }

    public int RefundedQuantity { get; private set; }

    public Order? Order { get; private set; }

    public IReadOnlyCollection<OrderItemModifier> Modifiers =>
        _modifiers.AsReadOnly();

    /// <summary>
    /// Tổng tiền modifier trên một đơn vị sản phẩm.
    /// </summary>
    public long ModifierAmountPerUnit =>
        SafeSum(
            _modifiers.Select(
                modifier =>
                    modifier.AmountPerProductUnit),
            "ORDER_ITEM.MODIFIER_TOTAL_OVERFLOW",
            "Tổng tiền modifier vượt giới hạn.");

    /// <summary>
    /// Đơn giá cuối cùng gồm giá sản phẩm và modifier.
    /// </summary>
    public long FinalUnitPrice =>
        SafeAdd(
            UnitSalePrice,
            ModifierAmountPerUnit,
            "ORDER_ITEM.UNIT_PRICE_OVERFLOW",
            "Đơn giá sản phẩm vượt giới hạn.");

    /// <summary>
    /// Tổng trước giảm giá của dòng hàng.
    /// </summary>
    public long GrossAmount =>
        SafeMultiply(
            FinalUnitPrice,
            Quantity,
            "ORDER_ITEM.GROSS_AMOUNT_OVERFLOW",
            "Thành tiền dòng hàng vượt giới hạn.");

    /// <summary>
    /// Thành tiền cuối cùng sau giảm giá dòng hàng.
    /// </summary>
    public long NetAmount =>
        GrossAmount - LineDiscountAmount;

    /// <summary>
    /// Tổng giá vốn của dòng hàng.
    /// </summary>
    public long CostAmount =>
        SafeMultiply(
            UnitCostPrice,
            Quantity,
            "ORDER_ITEM.COST_AMOUNT_OVERFLOW",
            "Tổng giá vốn dòng hàng vượt giới hạn.");

    /// <summary>
    /// Lãi gộp tạm tính của dòng hàng.
    /// </summary>
    public long GrossProfit =>
        NetAmount - CostAmount;

    public int RemainingRefundableQuantity =>
        Quantity - RefundedQuantity;

    public void ChangeQuantity(
        int quantity,
        DateTimeOffset utcNow)
    {
        EnsureEditable();

        if (quantity < RefundedQuantity)
        {
            throw new DomainException(
                "ORDER_ITEM.QUANTITY_BELOW_REFUNDED",
                "Số lượng mới không được nhỏ hơn số lượng đã hoàn.");
        }

        SetQuantity(quantity);

        if (LineDiscountAmount > GrossAmount)
        {
            LineDiscountAmount = GrossAmount;
        }

        MarkUpdated(utcNow);
    }

    public void ChangeNotes(
        string? notes,
        DateTimeOffset utcNow)
    {
        EnsureEditable();
        SetNotes(notes);

        MarkUpdated(utcNow);
    }

    public void ApplyLineDiscount(
        long amount,
        DateTimeOffset utcNow)
    {
        EnsureEditable();

        if (amount < 0)
        {
            throw new DomainException(
                "ORDER_ITEM.INVALID_DISCOUNT",
                "Giảm giá dòng hàng không được nhỏ hơn 0.");
        }

        if (amount > GrossAmount)
        {
            throw new DomainException(
                "ORDER_ITEM.DISCOUNT_EXCEEDS_GROSS",
                "Giảm giá dòng hàng vượt quá thành tiền.");
        }

        LineDiscountAmount = amount;

        MarkUpdated(utcNow);
    }

    public void ClearLineDiscount(
        DateTimeOffset utcNow)
    {
        EnsureEditable();

        if (LineDiscountAmount == 0)
        {
            return;
        }

        LineDiscountAmount = 0;

        MarkUpdated(utcNow);
    }

    public void AddModifier(
        int modifierId,
        int modifierGroupId,
        string modifierGroupName,
        string modifierName,
        int quantity,
        long unitAdditionalPrice,
        DateTimeOffset utcNow)
    {
        EnsureEditable();

        var existing = _modifiers.FirstOrDefault(
            modifier =>
                modifier.ModifierId == modifierId);

        if (existing is not null)
        {
            if (existing.UnitAdditionalPrice !=
                    unitAdditionalPrice ||
                !string.Equals(
                    existing.ModifierName,
                    modifierName.Trim(),
                    StringComparison.Ordinal))
            {
                throw new DomainException(
                    "ORDER_ITEM.MODIFIER_SNAPSHOT_MISMATCH",
                    "Thông tin modifier không khớp với lựa chọn đã có.");
            }

            int newQuantity;

            try
            {
                newQuantity = checked(
                    existing.Quantity + quantity);
            }
            catch (OverflowException exception)
            {
                throw new DomainException(
                    "ORDER_ITEM.MODIFIER_QUANTITY_OVERFLOW",
                    "Số lượng modifier vượt giới hạn.",
                    exception);
            }

            existing.ChangeQuantity(newQuantity);
        }
        else
        {
            _modifiers.Add(
                new OrderItemModifier(
                    modifierId,
                    modifierGroupId,
                    modifierGroupName,
                    modifierName,
                    quantity,
                    unitAdditionalPrice));
        }

        if (LineDiscountAmount > GrossAmount)
        {
            LineDiscountAmount = GrossAmount;
        }

        MarkUpdated(utcNow);
    }

    public void ChangeModifierQuantity(
        int modifierId,
        int quantity,
        DateTimeOffset utcNow)
    {
        EnsureEditable();

        var modifier = FindModifier(modifierId);

        modifier.ChangeQuantity(quantity);

        if (LineDiscountAmount > GrossAmount)
        {
            LineDiscountAmount = GrossAmount;
        }

        MarkUpdated(utcNow);
    }

    public void RemoveModifier(
        int modifierId,
        DateTimeOffset utcNow)
    {
        EnsureEditable();

        var modifier = FindModifier(modifierId);

        _modifiers.Remove(modifier);

        if (LineDiscountAmount > GrossAmount)
        {
            LineDiscountAmount = GrossAmount;
        }

        MarkUpdated(utcNow);
    }

    public void Cancel(DateTimeOffset utcNow)
    {
        if (Status == OrderItemStatus.Cancelled)
        {
            return;
        }

        if (Status is
            OrderItemStatus.PartiallyRefunded or
            OrderItemStatus.Refunded)
        {
            throw new DomainException(
                "ORDER_ITEM.CANNOT_CANCEL_REFUNDED",
                "Không thể hủy dòng hàng đã hoàn tiền.");
        }

        Status = OrderItemStatus.Cancelled;

        MarkUpdated(utcNow);
    }

    public void RegisterRefund(
        int quantity,
        DateTimeOffset utcNow)
    {
        if (Status == OrderItemStatus.Cancelled)
        {
            throw new DomainException(
                "ORDER_ITEM.CANCELLED",
                "Không thể hoàn tiền dòng hàng đã bị hủy.");
        }

        if (quantity <= 0)
        {
            throw new DomainException(
                "ORDER_ITEM.INVALID_REFUND_QUANTITY",
                "Số lượng hoàn phải lớn hơn 0.");
        }

        if (quantity > RemainingRefundableQuantity)
        {
            throw new DomainException(
                "ORDER_ITEM.REFUND_EXCEEDS_QUANTITY",
                "Số lượng hoàn vượt quá số lượng có thể hoàn.");
        }

        RefundedQuantity += quantity;

        Status = RefundedQuantity == Quantity
            ? OrderItemStatus.Refunded
            : OrderItemStatus.PartiallyRefunded;

        MarkUpdated(utcNow);
    }

    private OrderItemModifier FindModifier(int modifierId)
    {
        var modifier = _modifiers.FirstOrDefault(
            item => item.ModifierId == modifierId);

        if (modifier is null)
        {
            throw new DomainException(
                "ORDER_ITEM.MODIFIER_NOT_FOUND",
                "Không tìm thấy modifier trong dòng hàng.");
        }

        return modifier;
    }

    private void EnsureEditable()
    {
        if (Status != OrderItemStatus.Active)
        {
            throw new DomainException(
                "ORDER_ITEM.NOT_EDITABLE",
                "Chỉ có thể sửa dòng hàng đang hoạt động.");
        }
    }

    private void SetProductId(int productId)
    {
        if (productId <= 0)
        {
            throw new DomainException(
                "ORDER_ITEM.INVALID_PRODUCT_ID",
                "Sản phẩm không hợp lệ.");
        }

        ProductId = productId;
    }

    private void SetProductCode(string productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            throw new DomainException(
                "ORDER_ITEM.PRODUCT_CODE_REQUIRED",
                "Mã sản phẩm không được để trống.");
        }

        var normalized = productCode
            .Trim()
            .ToUpperInvariant();

        if (normalized.Length >
            BusinessRules.Products.CodeMaxLength)
        {
            throw new DomainException(
                "ORDER_ITEM.PRODUCT_CODE_TOO_LONG",
                "Mã sản phẩm vượt quá giới hạn.");
        }

        ProductCode = normalized;
    }

    private void SetProductName(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainException(
                "ORDER_ITEM.PRODUCT_NAME_REQUIRED",
                "Tên sản phẩm không được để trống.");
        }

        var trimmed = productName.Trim();

        if (trimmed.Length >
            BusinessRules.Products.NameMaxLength)
        {
            throw new DomainException(
                "ORDER_ITEM.PRODUCT_NAME_TOO_LONG",
                "Tên sản phẩm vượt quá giới hạn.");
        }

        ProductName = trimmed;
    }

    private void SetUnitName(string unitName)
    {
        if (string.IsNullOrWhiteSpace(unitName))
        {
            throw new DomainException(
                "ORDER_ITEM.UNIT_REQUIRED",
                "Đơn vị tính không được để trống.");
        }

        var trimmed = unitName.Trim();

        if (trimmed.Length >
            BusinessRules.Products.UnitNameMaxLength)
        {
            throw new DomainException(
                "ORDER_ITEM.UNIT_TOO_LONG",
                "Đơn vị tính vượt quá giới hạn.");
        }

        UnitName = trimmed;
    }

    private void SetQuantity(int quantity)
    {
        if (quantity <= 0 ||
            quantity >
            BusinessRules.Orders.MaximumLineQuantity)
        {
            throw new DomainException(
                "ORDER_ITEM.INVALID_QUANTITY",
                "Số lượng sản phẩm không hợp lệ.");
        }

        Quantity = quantity;
    }

    private void SetUnitCostPrice(long value)
    {
        ValidatePrice(
            value,
            "ORDER_ITEM.INVALID_COST_PRICE",
            "Giá vốn");

        UnitCostPrice = value;
    }

    private void SetUnitSalePrice(long value)
    {
        ValidatePrice(
            value,
            "ORDER_ITEM.INVALID_SALE_PRICE",
            "Giá bán");

        UnitSalePrice = value;
    }

    private void SetNotes(string? notes)
    {
        var normalized = string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim();

        if (normalized?.Length >
            BusinessRules.Orders.NotesMaxLength)
        {
            throw new DomainException(
                "ORDER_ITEM.NOTES_TOO_LONG",
                "Ghi chú dòng hàng vượt quá giới hạn.");
        }

        Notes = normalized;
    }

    private static void ValidatePrice(
        long value,
        string code,
        string fieldName)
    {
        if (value < 0 ||
            value > BusinessRules.Products.MaximumPrice)
        {
            throw new DomainException(
                code,
                $"{fieldName} không hợp lệ.");
        }
    }

    private static long SafeMultiply(
        long left,
        int right,
        string code,
        string message)
    {
        try
        {
            var result = checked(left * right);

            if (result >
                BusinessRules.Orders.MaximumOrderAmount)
            {
                throw new DomainException(
                    code,
                    message);
            }

            return result;
        }
        catch (OverflowException exception)
        {
            throw new DomainException(
                code,
                message,
                exception);
        }
    }

    private static long SafeAdd(
        long left,
        long right,
        string code,
        string message)
    {
        try
        {
            var result = checked(left + right);

            if (result >
                BusinessRules.Orders.MaximumOrderAmount)
            {
                throw new DomainException(
                    code,
                    message);
            }

            return result;
        }
        catch (OverflowException exception)
        {
            throw new DomainException(
                code,
                message,
                exception);
        }
    }

    private static long SafeSum(
        IEnumerable<long> values,
        string code,
        string message)
    {
        var total = 0L;

        foreach (var value in values)
        {
            total = SafeAdd(
                total,
                value,
                code,
                message);
        }

        return total;
    }
}