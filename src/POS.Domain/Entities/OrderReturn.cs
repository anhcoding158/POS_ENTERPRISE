using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class OrderReturn : Entity
{
    private readonly List<OrderReturnItem> _items = [];

    private OrderReturn()
    {
    }

    public OrderReturn(
        Guid clientRequestId,
        string requestFingerprint,
        int orderId,
        int processedByUserId,
        DateTimeOffset createdAtUtc,
        string reason,
        PaymentMethod refundMethod,
        string? refundReference,
        IEnumerable<OrderReturnItem> items)
    {
        if (clientRequestId == Guid.Empty)
            throw new DomainException("ORDER_RETURN.REQUEST_ID_REQUIRED", "ClientRequestId không hợp lệ.");
        if (requestFingerprint.Length != 64 ||
            requestFingerprint.Any(character => !Uri.IsHexDigit(character)))
            throw new DomainException("ORDER_RETURN.INVALID_FINGERPRINT", "Fingerprint phải là SHA-256 hex 64 ký tự.");
        if (orderId <= 0 || processedByUserId <= 0)
            throw new DomainException("ORDER_RETURN.INVALID_REFERENCE", "Order hoặc người xử lý không hợp lệ.");
        if (createdAtUtc == default)
            throw new DomainException("ORDER_RETURN.TIME_REQUIRED", "Thời điểm trả hàng không hợp lệ.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("ORDER_RETURN.REASON_REQUIRED", "Phải nhập lý do trả hàng.");
        if (!Enum.IsDefined(refundMethod))
            throw new DomainException("ORDER_RETURN.INVALID_REFUND_METHOD", "Phương thức hoàn không hợp lệ.");

        var materializedItems = items?.ToArray() ??
            throw new ArgumentNullException(nameof(items));
        if (materializedItems.Length == 0)
            throw new DomainException("ORDER_RETURN.LINES_REQUIRED", "Chứng từ phải có ít nhất một dòng.");

        var total = materializedItems.Aggregate(
            0L,
            (current, item) => checked(current + item.RefundAmount));
        if (total <= 0)
            throw new DomainException("ORDER_RETURN.INVALID_TOTAL", "Tổng tiền hoàn phải lớn hơn 0.");

        ClientRequestId = clientRequestId;
        RequestFingerprint = requestFingerprint.ToUpperInvariant();
        OrderId = orderId;
        ProcessedByUserId = processedByUserId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Reason = reason.Trim();
        RefundMethod = refundMethod;
        RefundReference = string.IsNullOrWhiteSpace(refundReference) ? null : refundReference.Trim();
        TotalRefundAmount = total;
        _items.AddRange(materializedItems);
    }

    public Guid ClientRequestId { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public int OrderId { get; private set; }
    public int ProcessedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public PaymentMethod RefundMethod { get; private set; }
    public string? RefundReference { get; private set; }
    public long TotalRefundAmount { get; private set; }
    public Order? Order { get; private set; }
    public User? ProcessedByUser { get; private set; }
    public IReadOnlyCollection<OrderReturnItem> Items => _items.AsReadOnly();
}
