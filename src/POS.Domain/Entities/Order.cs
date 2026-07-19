using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;

using PaymentMethodType =
    POS.Domain.Enums.PaymentMethod;

namespace POS.Domain.Entities;

/// <summary>
/// Aggregate root của giao dịch bán hàng.
///
/// Mọi thay đổi liên quan đến dòng hàng, tổng tiền,
/// thanh toán và trạng thái đơn phải đi qua Order.
/// </summary>
public sealed class Order : AuditableEntity
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public Order(
        string orderCode,
        int cashierUserId,
        DateTimeOffset utcNow,
        int? customerId = null,
        int? restaurantTableId = null,
        string? notes = null)
    {
        SetOrderCode(orderCode);
        SetCashierUserId(cashierUserId);
        SetCustomerId(customerId);
        SetRestaurantTableId(restaurantTableId);
        SetNotes(notes);

        Status = OrderStatus.Draft;

        MarkCreated(utcNow);
    }

    public string OrderCode { get; private set; } =
        string.Empty;

    public int CashierUserId { get; private set; }

    public int? CustomerId { get; private set; }

    public int? RestaurantTableId { get; private set; }

    public int? DiscountId { get; private set; }

    public string? DiscountCode { get; private set; }

    public string? Notes { get; private set; }

    public OrderStatus Status { get; private set; }

    /// <summary>
    /// Tổng tiền các dòng hàng sau giảm giá từng dòng,
    /// trước giảm giá toàn đơn.
    /// </summary>
    public long Subtotal { get; private set; }

    /// <summary>
    /// Giảm giá cấp hóa đơn.
    /// </summary>
    public long DiscountAmount { get; private set; }

    /// <summary>
    /// Tổng tiền cuối cùng khách phải thanh toán.
    /// </summary>
    public long TotalAmount { get; private set; }

    public PaymentMethodType? PaymentMethod { get; private set; }

    public long CashReceived { get; private set; }

    public long ChangeAmount { get; private set; }

    public long RefundedAmount { get; private set; }

    public DateTimeOffset? PaidAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? CancelReason { get; private set; }

    public User? CashierUser { get; private set; }

    public Customer? Customer { get; private set; }

    public RestaurantTable? RestaurantTable { get; private set; }

    public Discount? Discount { get; private set; }

    public IReadOnlyCollection<OrderItem> Items =>
        _items.AsReadOnly();

    public int ActiveItemCount =>
        _items.Count(
            item => item.Status ==
                    OrderItemStatus.Active);

    public long TotalCostAmount =>
        SafeSum(
            _items
                .Where(
                    item =>
                        item.Status !=
                        OrderItemStatus.Cancelled)
                .Select(
                    item => item.CostAmount),
            "ORDER.COST_TOTAL_OVERFLOW",
            "Tổng giá vốn đơn hàng vượt giới hạn.");

    public long GrossProfit =>
        TotalAmount - TotalCostAmount;

    public long RemainingRefundableAmount =>
        TotalAmount - RefundedAmount;

    public OrderItem AddItem(
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
        EnsureEditable();

        if (ActiveItemCount >=
            BusinessRules.Orders.MaximumLinesPerOrder)
        {
            throw new DomainException(
                "ORDER.TOO_MANY_LINES",
                "Đơn hàng vượt quá số dòng cho phép.");
        }

        var item = new OrderItem(
            productId,
            productCode,
            productName,
            unitName,
            quantity,
            unitCostPrice,
            unitSalePrice,
            utcNow,
            notes);

        _items.Add(item);

        RecalculateTotals();
        MarkUpdated(utcNow);

        return item;
    }

    public void RemoveItem(
        OrderItem item,
        DateTimeOffset utcNow)
    {
        EnsureEditable();

        ArgumentNullException.ThrowIfNull(item);

        if (!_items.Contains(item))
        {
            throw new DomainException(
                "ORDER.ITEM_NOT_FOUND",
                "Dòng hàng không thuộc đơn hiện tại.");
        }

        if (item.IsTransient)
        {
            _items.Remove(item);
        }
        else
        {
            item.Cancel(utcNow);
        }

        RecalculateTotals();
        MarkUpdated(utcNow);
    }

    public void ChangeItemQuantity(
        OrderItem item,
        int quantity,
        DateTimeOffset utcNow)
    {
        EnsureItemBelongsToOrder(item);
        EnsureEditable();

        item.ChangeQuantity(quantity, utcNow);

        RecalculateTotals();
        MarkUpdated(utcNow);
    }

    public void AddItemModifier(
        OrderItem item,
        int modifierId,
        int modifierGroupId,
        string modifierGroupName,
        string modifierName,
        int quantity,
        long unitAdditionalPrice,
        DateTimeOffset utcNow)
    {
        EnsureItemBelongsToOrder(item);
        EnsureEditable();

        item.AddModifier(
            modifierId,
            modifierGroupId,
            modifierGroupName,
            modifierName,
            quantity,
            unitAdditionalPrice,
            utcNow);

        RecalculateTotals();
        MarkUpdated(utcNow);
    }

    public void RemoveItemModifier(
        OrderItem item,
        int modifierId,
        DateTimeOffset utcNow)
    {
        EnsureItemBelongsToOrder(item);
        EnsureEditable();

        item.RemoveModifier(
            modifierId,
            utcNow);

        RecalculateTotals();
        MarkUpdated(utcNow);
    }

    public void ApplyItemDiscount(
        OrderItem item,
        long amount,
        DateTimeOffset utcNow)
    {
        EnsureItemBelongsToOrder(item);
        EnsureEditable();

        item.ApplyLineDiscount(amount, utcNow);

        RecalculateTotals();
        MarkUpdated(utcNow);
    }

    public void AttachCustomer(
        int? customerId,
        DateTimeOffset utcNow)
    {
        EnsureEditable();
        SetCustomerId(customerId);

        MarkUpdated(utcNow);
    }

    public void AssignRestaurantTable(
        int? restaurantTableId,
        DateTimeOffset utcNow)
    {
        EnsureEditable();
        SetRestaurantTableId(restaurantTableId);

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

    public void ApplyDiscount(
        int? discountId,
        string? discountCode,
        long amount,
        DateTimeOffset utcNow)
    {
        EnsureEditable();

        if (discountId.HasValue &&
            discountId.Value <= 0)
        {
            throw new DomainException(
                "ORDER.INVALID_DISCOUNT_ID",
                "Khuyến mãi không hợp lệ.");
        }

        if (amount < 0)
        {
            throw new DomainException(
                "ORDER.INVALID_DISCOUNT_AMOUNT",
                "Số tiền giảm giá không được nhỏ hơn 0.");
        }

        RecalculateSubtotal();

        if (amount > Subtotal)
        {
            throw new DomainException(
                "ORDER.DISCOUNT_EXCEEDS_SUBTOTAL",
                "Giảm giá vượt quá tiền hàng.");
        }

        DiscountId = discountId;
        DiscountCode =
            NormalizeOptionalCode(discountCode);

        DiscountAmount = amount;

        RecalculateTotalAmount();
        MarkUpdated(utcNow);
    }

    public void ClearDiscount(DateTimeOffset utcNow)
    {
        EnsureEditable();

        if (DiscountAmount == 0 &&
            !DiscountId.HasValue &&
            DiscountCode is null)
        {
            return;
        }

        DiscountId = null;
        DiscountCode = null;
        DiscountAmount = 0;

        RecalculateTotalAmount();
        MarkUpdated(utcNow);
    }

    public void PrepareForPayment(
        DateTimeOffset utcNow)
    {
        if (Status != OrderStatus.Draft)
        {
            throw new DomainException(
                "ORDER.CANNOT_PREPARE_FOR_PAYMENT",
                "Chỉ đơn nháp mới có thể chuyển sang chờ thanh toán.");
        }

        EnsureHasActiveItems();
        RecalculateTotals();

        Status = OrderStatus.PendingPayment;

        MarkUpdated(utcNow);
    }

    public void MarkPaid(
        PaymentMethodType paymentMethod,
        long cashReceived,
        DateTimeOffset utcNow)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new DomainException(
                "ORDER.NOT_PENDING_PAYMENT",
                "Đơn hàng chưa ở trạng thái chờ thanh toán.");
        }

        if (!Enum.IsDefined(
                typeof(PaymentMethodType),
                paymentMethod))
        {
            throw new DomainException(
                "ORDER.INVALID_PAYMENT_METHOD",
                "Phương thức thanh toán không hợp lệ.");
        }

        EnsureHasActiveItems();
        RecalculateTotals();

        if (paymentMethod == PaymentMethodType.Cash)
        {
            if (cashReceived < TotalAmount)
            {
                throw new DomainException(
                    "ORDER.INSUFFICIENT_CASH",
                    "Tiền khách đưa không đủ thanh toán.");
            }

            CashReceived = cashReceived;
            ChangeAmount = cashReceived - TotalAmount;
        }
        else
        {
            if (cashReceived != 0)
            {
                throw new DomainException(
                    "ORDER.NON_CASH_AMOUNT_NOT_ALLOWED",
                    "Thanh toán không dùng tiền mặt không được nhập tiền khách đưa.");
            }

            CashReceived = 0;
            ChangeAmount = 0;
        }

        PaymentMethod = paymentMethod;
        PaidAtUtc = utcNow.ToUniversalTime();
        Status = OrderStatus.Paid;

        MarkUpdated(utcNow);
    }

    public void Complete(DateTimeOffset utcNow)
    {
        if (Status == OrderStatus.Completed)
        {
            return;
        }

        if (Status != OrderStatus.Paid)
        {
            throw new DomainException(
                "ORDER.NOT_PAID",
                "Chỉ đơn đã thanh toán mới được hoàn tất.");
        }

        Status = OrderStatus.Completed;
        CompletedAtUtc = utcNow.ToUniversalTime();

        MarkUpdated(utcNow);
    }

    public void Cancel(
        string reason,
        DateTimeOffset utcNow)
    {
        if (Status == OrderStatus.Cancelled)
        {
            return;
        }

        if (Status is
            OrderStatus.Paid or
            OrderStatus.Completed or
            OrderStatus.PartiallyRefunded or
            OrderStatus.Refunded)
        {
            throw new DomainException(
                "ORDER.CANNOT_CANCEL_PAID_ORDER",
                "Đơn đã thanh toán phải thực hiện hoàn tiền, không được hủy trực tiếp.");
        }

        SetCancelReason(reason);

        foreach (var item in _items.Where(
                     item =>
                         item.Status ==
                         OrderItemStatus.Active))
        {
            item.Cancel(utcNow);
        }

        Status = OrderStatus.Cancelled;
        CancelledAtUtc = utcNow.ToUniversalTime();

        RecalculateTotals();
        MarkUpdated(utcNow);
    }

    public void RegisterRefund(
        long amount,
        DateTimeOffset utcNow)
    {
        if (Status is not (
            OrderStatus.Paid or
            OrderStatus.Completed or
            OrderStatus.PartiallyRefunded))
        {
            throw new DomainException(
                "ORDER.CANNOT_REFUND",
                "Trạng thái đơn hiện tại không cho phép hoàn tiền.");
        }

        if (amount <= 0)
        {
            throw new DomainException(
                "ORDER.INVALID_REFUND_AMOUNT",
                "Số tiền hoàn phải lớn hơn 0.");
        }

        if (amount > RemainingRefundableAmount)
        {
            throw new DomainException(
                "ORDER.REFUND_EXCEEDS_REMAINING",
                "Số tiền hoàn vượt quá số tiền còn có thể hoàn.");
        }

        RefundedAmount = SafeAdd(
            RefundedAmount,
            amount,
            "ORDER.REFUND_AMOUNT_OVERFLOW",
            "Tổng tiền hoàn vượt giới hạn.");

        Status = RefundedAmount == TotalAmount
            ? OrderStatus.Refunded
            : OrderStatus.PartiallyRefunded;

        MarkUpdated(utcNow);
    }

    private void RecalculateTotals()
    {
        RecalculateSubtotal();

        if (DiscountAmount > Subtotal)
        {
            DiscountAmount = Subtotal;
        }

        RecalculateTotalAmount();
    }

    private void RecalculateSubtotal()
    {
        Subtotal = SafeSum(
            _items
                .Where(
                    item =>
                        item.Status ==
                        OrderItemStatus.Active)
                .Select(
                    item => item.NetAmount),
            "ORDER.SUBTOTAL_OVERFLOW",
            "Tiền hàng vượt quá giới hạn.");
    }

    private void RecalculateTotalAmount()
    {
        TotalAmount =
            Subtotal - DiscountAmount;

        if (TotalAmount < 0 ||
            TotalAmount >
            BusinessRules.Orders.MaximumOrderAmount)
        {
            throw new DomainException(
                "ORDER.INVALID_TOTAL_AMOUNT",
                "Tổng tiền đơn hàng không hợp lệ.");
        }
    }

    private void EnsureEditable()
    {
        if (Status is not (
            OrderStatus.Draft or
            OrderStatus.PendingPayment))
        {
            throw new DomainException(
                "ORDER.NOT_EDITABLE",
                "Đơn hàng không còn được phép chỉnh sửa.");
        }
    }

    private void EnsureHasActiveItems()
    {
        if (!_items.Any(
                item =>
                    item.Status ==
                    OrderItemStatus.Active))
        {
            throw new DomainException(
                "ORDER.EMPTY",
                "Đơn hàng phải có ít nhất một sản phẩm.");
        }
    }

    private void EnsureItemBelongsToOrder(
        OrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!_items.Contains(item))
        {
            throw new DomainException(
                "ORDER.ITEM_NOT_FOUND",
                "Dòng hàng không thuộc đơn hiện tại.");
        }
    }

    private void SetOrderCode(string orderCode)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            throw new DomainException(
                "ORDER.CODE_REQUIRED",
                "Mã đơn hàng không được để trống.");
        }

        var normalized = orderCode
            .Trim()
            .ToUpperInvariant();

        if (normalized.Length >
            BusinessRules.Orders.CodeMaxLength)
        {
            throw new DomainException(
                "ORDER.CODE_TOO_LONG",
                "Mã đơn hàng vượt quá giới hạn.");
        }

        OrderCode = normalized;
    }

    private void SetCashierUserId(int cashierUserId)
    {
        if (cashierUserId <= 0)
        {
            throw new DomainException(
                "ORDER.INVALID_CASHIER_ID",
                "Thu ngân không hợp lệ.");
        }

        CashierUserId = cashierUserId;
    }

    private void SetCustomerId(int? customerId)
    {
        if (customerId.HasValue &&
            customerId.Value <= 0)
        {
            throw new DomainException(
                "ORDER.INVALID_CUSTOMER_ID",
                "Khách hàng không hợp lệ.");
        }

        CustomerId = customerId;
    }

    private void SetRestaurantTableId(
        int? restaurantTableId)
    {
        if (restaurantTableId.HasValue &&
            restaurantTableId.Value <= 0)
        {
            throw new DomainException(
                "ORDER.INVALID_TABLE_ID",
                "Bàn phục vụ không hợp lệ.");
        }

        RestaurantTableId = restaurantTableId;
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
                "ORDER.NOTES_TOO_LONG",
                "Ghi chú đơn hàng vượt quá giới hạn.");
        }

        Notes = normalized;
    }

    private void SetCancelReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(
                "ORDER.CANCEL_REASON_REQUIRED",
                "Phải nhập lý do hủy đơn.");
        }

        var trimmed = reason.Trim();

        if (trimmed.Length >
            BusinessRules.Orders.CancelReasonMaxLength)
        {
            throw new DomainException(
                "ORDER.CANCEL_REASON_TOO_LONG",
                "Lý do hủy đơn vượt quá giới hạn.");
        }

        CancelReason = trimmed;
    }

    private static string? NormalizeOptionalCode(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value
            .Trim()
            .ToUpperInvariant();

        if (normalized.Length >
            BusinessRules.Discounts.CodeMaxLength)
        {
            throw new DomainException(
                "ORDER.DISCOUNT_CODE_TOO_LONG",
                "Mã giảm giá vượt quá giới hạn.");
        }

        return normalized;
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