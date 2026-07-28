using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class HeldSale : AuditableEntity
{
    private readonly List<HeldSaleLine> _lines = [];

    private HeldSale()
    {
    }

    public HeldSale(
        Guid clientRequestId,
        string requestFingerprint,
        string displayCode,
        string label,
        string? notes,
        int createdByUserId,
        DateTimeOffset utcNow,
        IEnumerable<(int ProductId, string Code, string? Barcode, string Name,
            int Quantity, long UnitPrice, int SortOrder, string? Notes)> lines)
    {
        if (clientRequestId == Guid.Empty)
            throw new DomainException("HELD_SALE.REQUEST_ID_REQUIRED", "ClientRequestId không hợp lệ.");
        if (requestFingerprint?.Length != BusinessRules.HeldSales.FingerprintLength ||
            requestFingerprint.Any(character => !Uri.IsHexDigit(character)) ||
            !string.Equals(
                requestFingerprint,
                requestFingerprint.ToUpperInvariant(),
                StringComparison.Ordinal))
            throw new DomainException("HELD_SALE.FINGERPRINT_INVALID", "Fingerprint đơn giữ không hợp lệ.");
        if (createdByUserId <= 0)
            throw new DomainException("HELD_SALE.USER_REQUIRED", "Người tạo đơn giữ không hợp lệ.");

        ClientRequestId = clientRequestId;
        RequestFingerprint = requestFingerprint;
        DisplayCode = Required(displayCode, BusinessRules.HeldSales.DisplayCodeMaxLength, "mã hiển thị");
        Label = Required(label, BusinessRules.HeldSales.LabelMaxLength, "nhãn");
        Notes = Optional(notes, BusinessRules.HeldSales.NotesMaxLength);
        CreatedByUserId = createdByUserId;
        Status = HeldSaleStatus.Active;

        foreach (var line in lines ?? throw new ArgumentNullException(nameof(lines)))
            _lines.Add(new HeldSaleLine(line.ProductId, line.Code, line.Barcode, line.Name,
                line.Quantity, line.UnitPrice, line.SortOrder, line.Notes));

        if (_lines.Count is 0 or > BusinessRules.HeldSales.MaximumLines)
            throw new DomainException("HELD_SALE.LINES_REQUIRED", "Đơn giữ phải có ít nhất một dòng hợp lệ.");
        MarkCreated(utcNow);
    }

    public Guid ClientRequestId { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public string DisplayCode { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public HeldSaleStatus Status { get; private set; }
    public int CreatedByUserId { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public int? CompletedOrderId { get; private set; }
    public long TotalSnapshot => _lines.Sum(line => line.LineTotalSnapshot);
    public IReadOnlyCollection<HeldSaleLine> Lines => _lines.AsReadOnly();
    public User? CreatedByUser { get; private set; }
    public Order? CompletedOrder { get; private set; }

    public void Complete(int orderId, DateTimeOffset utcNow)
    {
        EnsureActive();
        if (orderId <= 0)
            throw new DomainException("HELD_SALE.ORDER_REQUIRED", "Order hoàn tất không hợp lệ.");
        Status = HeldSaleStatus.Completed;
        CompletedOrderId = orderId;
        CompletedAtUtc = utcNow.ToUniversalTime();
        MarkUpdated(utcNow);
    }

    public void Cancel(DateTimeOffset utcNow)
    {
        EnsureActive();
        Status = HeldSaleStatus.Cancelled;
        CancelledAtUtc = utcNow.ToUniversalTime();
        MarkUpdated(utcNow);
    }

    private void EnsureActive()
    {
        if (Status != HeldSaleStatus.Active)
            throw new DomainException("HELD_SALE.NOT_ACTIVE", "Đơn giữ không còn ở trạng thái đang giữ.");
    }

    private static string Required(string value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("HELD_SALE.TEXT_REQUIRED", $"{field} không được để trống.");
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new DomainException("HELD_SALE.TEXT_TOO_LONG", $"{field} vượt quá giới hạn.");
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength)
            throw new DomainException("HELD_SALE.TEXT_TOO_LONG", "Ghi chú vượt quá giới hạn.");
        return normalized;
    }
}
