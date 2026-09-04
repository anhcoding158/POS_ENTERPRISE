using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Aggregate root của Purchase Order.
/// Purchase Order chỉ ghi nhận cam kết mua hàng; không thay đổi tồn kho.
/// </summary>
public sealed class PurchaseOrder : AuditableEntity
{
    private readonly List<PurchaseOrderLine> _lines = [];

    private PurchaseOrder()
    {
    }

    public PurchaseOrder(
        string orderNumber,
        int supplierId,
        string supplierCode,
        string supplierName,
        string? supplierTaxCode,
        DateOnly orderDate,
        DateOnly? expectedDeliveryDate,
        string? notes,
        DateTimeOffset utcNow)
    {
        SetOrderNumber(orderNumber);
        SetSupplierId(supplierId);
        SetSupplierSnapshot(supplierCode, supplierName, supplierTaxCode);
        SetOrderDate(orderDate);
        SetExpectedDeliveryDate(expectedDeliveryDate);
        SetNotes(notes);

        Status = PurchaseOrderStatus.Draft;
        MarkCreated(utcNow);
    }

    public string OrderNumber { get; private set; } = string.Empty;

    public string NormalizedOrderNumber { get; private set; } = string.Empty;

    public int SupplierId { get; private set; }

    public string SupplierCode { get; private set; } = string.Empty;

    public string SupplierName { get; private set; } = string.Empty;

    public string? SupplierTaxCode { get; private set; }

    public DateOnly OrderDate { get; private set; }

    public DateOnly? ExpectedDeliveryDate { get; private set; }

    public string? Notes { get; private set; }

    public PurchaseOrderStatus Status { get; private set; }

    public DateTimeOffset? OrderedAtUtc { get; private set; }

    public int? OrderedByUserId { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public int? CancelledByUserId { get; private set; }

    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<PurchaseOrderLine> Lines =>
        _lines.AsReadOnly();

    public long GrandTotal => SafeSum(
        _lines.Select(line => line.LineTotal));

    public PurchaseOrderLine AddLine(
        int productId,
        string productCode,
        string productName,
        string unitName,
        int orderedQuantity,
        long agreedUnitCost,
        int sortOrder,
        DateTimeOffset utcNow)
    {
        EnsureDraft();
        EnsureLineCapacity();
        EnsureProductIsUnique(productId);

        var line = new PurchaseOrderLine(
            productId,
            productCode,
            productName,
            unitName,
            orderedQuantity,
            agreedUnitCost,
            sortOrder);
        _lines.Add(line);
        MarkUpdated(utcNow);
        EnsureTotalWithinLimit();
        return line;
    }

    public void UpdateDraftHeader(
        int supplierId,
        string supplierCode,
        string supplierName,
        string? supplierTaxCode,
        DateOnly orderDate,
        DateOnly? expectedDeliveryDate,
        string? notes,
        DateTimeOffset utcNow)
    {
        EnsureDraft();
        SetSupplierId(supplierId);
        SetSupplierSnapshot(supplierCode, supplierName, supplierTaxCode);
        SetOrderDate(orderDate);
        SetExpectedDeliveryDate(expectedDeliveryDate);
        SetNotes(notes);
        MarkUpdated(utcNow);
    }

    public void UpdateDraftLine(
        PurchaseOrderLine line,
        string productCode,
        string productName,
        string unitName,
        int orderedQuantity,
        long agreedUnitCost,
        int sortOrder,
        DateTimeOffset utcNow)
    {
        EnsureDraftLine(line);
        line.UpdateDraft(
            productCode,
            productName,
            unitName,
            orderedQuantity,
            agreedUnitCost,
            sortOrder);
        MarkUpdated(utcNow);
        EnsureTotalWithinLimit();
    }

    public void RemoveLine(
        PurchaseOrderLine line,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!_lines.Contains(line))
        {
            throw new DomainException(
                "PURCHASE_ORDER.LINE_NOT_FOUND",
                "Dòng không thuộc Purchase Order.");
        }

        EnsureNotCancelled();
        if (line.ReceivedQuantity > 0)
        {
            throw new DomainException(
                "PURCHASE_ORDER.RECEIVED_LINE_CANNOT_BE_REMOVED",
                "Không được xóa dòng đã nhận hàng.");
        }

        if (Status == PurchaseOrderStatus.Ordered && _lines.Count == 1)
        {
            throw new DomainException(
                "PURCHASE_ORDER.EMPTY_ORDER_NOT_ALLOWED",
                "Purchase Order phải còn ít nhất một dòng.");
        }

        _lines.Remove(line);
        MarkUpdated(utcNow);
    }

    public void FinalizeSnapshotsAndMarkOrdered(
        SupplierSnapshot supplier,
        IReadOnlyDictionary<int, ProductSnapshot> products,
        int orderedByUserId,
        DateTimeOffset utcNow)
    {
        EnsureDraft();
        EnsureHasLines();
        SetOrderedByUserId(orderedByUserId);

        foreach (var line in _lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new DomainException(
                    "PURCHASE_ORDER.PRODUCT_SNAPSHOT_REQUIRED",
                    "Thiếu snapshot sản phẩm khi xác nhận Purchase Order.");
            }

            line.RefreshProductSnapshot(
                product.Code,
                product.Name,
                product.UnitName);
        }

        SetSupplierSnapshot(
            supplier.Code,
            supplier.Name,
            supplier.TaxCode);
        Status = PurchaseOrderStatus.Ordered;
        OrderedAtUtc = NormalizeUtc(utcNow);
        MarkUpdated(utcNow);
    }

    public void ChangeOrderedHeader(
        DateOnly? expectedDeliveryDate,
        string? notes,
        DateTimeOffset utcNow)
    {
        EnsureOrdered();
        SetExpectedDeliveryDate(expectedDeliveryDate);
        SetNotes(notes);
        MarkUpdated(utcNow);
    }

    public void AmendOrderedLine(
        PurchaseOrderLine line,
        int orderedQuantity,
        long agreedUnitCost,
        int sortOrder,
        DateTimeOffset utcNow)
    {
        EnsureOrderedLine(line);
        line.AmendOrdered(orderedQuantity, agreedUnitCost, sortOrder);
        MarkUpdated(utcNow);
        EnsureTotalWithinLimit();
    }

    public void Cancel(
        string reason,
        int cancelledByUserId,
        DateTimeOffset utcNow)
    {
        if (Status is PurchaseOrderStatus.Cancelled)
        {
            throw new DomainException(
                "PURCHASE_ORDER.ALREADY_CANCELLED",
                "Purchase Order đã được hủy.");
        }

        if (Status == PurchaseOrderStatus.Ordered &&
            _lines.Any(line => line.ReceivedQuantity > 0))
        {
            throw new DomainException(
                "PURCHASE_ORDER.PARTIAL_CANCEL_NOT_SUPPORTED",
                "Purchase Order đã có hàng nhận; xử lý phần còn lại ở checkpoint Goods Receipt.");
        }

        SetCancellationReason(reason);
        if (cancelledByUserId <= 0)
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_CANCELLED_BY",
                "Người hủy Purchase Order không hợp lệ.");
        }

        Status = PurchaseOrderStatus.Cancelled;
        CancelledByUserId = cancelledByUserId;
        CancelledAtUtc = NormalizeUtc(utcNow);
        MarkUpdated(utcNow);
    }

    public sealed record SupplierSnapshot(
        string Code,
        string Name,
        string? TaxCode);

    public sealed record ProductSnapshot(
        string Code,
        string Name,
        string UnitName);

    private void EnsureDraft()
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new DomainException(
                "PURCHASE_ORDER.NOT_DRAFT",
                "Purchase Order không còn ở trạng thái nháp.");
        }
    }

    private void EnsureOrdered()
    {
        if (Status != PurchaseOrderStatus.Ordered)
        {
            throw new DomainException(
                "PURCHASE_ORDER.NOT_ORDERED",
                "Chỉ Purchase Order đã đặt mới được amendment.");
        }
    }

    private void EnsureNotCancelled()
    {
        if (Status == PurchaseOrderStatus.Cancelled)
        {
            throw new DomainException(
                "PURCHASE_ORDER.CANCELLED_READ_ONLY",
                "Purchase Order đã hủy chỉ được xem.");
        }
    }

    private void EnsureDraftLine(PurchaseOrderLine line)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(line);
        if (!_lines.Contains(line))
        {
            throw new DomainException(
                "PURCHASE_ORDER.LINE_NOT_FOUND",
                "Dòng không thuộc Purchase Order.");
        }
    }

    private void EnsureOrderedLine(PurchaseOrderLine line)
    {
        EnsureOrdered();
        ArgumentNullException.ThrowIfNull(line);
        if (!_lines.Contains(line))
        {
            throw new DomainException(
                "PURCHASE_ORDER.LINE_NOT_FOUND",
                "Dòng không thuộc Purchase Order.");
        }
    }

    private void EnsureLineCapacity()
    {
        if (_lines.Count >= BusinessRules.PurchaseOrders.MaximumLines)
        {
            throw new DomainException(
                "PURCHASE_ORDER.TOO_MANY_LINES",
                "Purchase Order vượt quá số dòng cho phép.");
        }
    }

    private void EnsureProductIsUnique(int productId)
    {
        if (productId <= 0)
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_PRODUCT_ID",
                "Sản phẩm của dòng mua hàng không hợp lệ.");
        }

        if (_lines.Any(line => line.ProductId == productId))
        {
            throw new DomainException(
                "PURCHASE_ORDER.DUPLICATE_PRODUCT",
                "Mỗi sản phẩm chỉ được xuất hiện một lần trong Purchase Order.");
        }
    }

    private void EnsureHasLines()
    {
        if (_lines.Count == 0)
        {
            throw new DomainException(
                "PURCHASE_ORDER.EMPTY",
                "Purchase Order phải có ít nhất một dòng.");
        }
    }

    private void EnsureTotalWithinLimit()
    {
        _ = GrandTotal;
    }

    private void SetOrderNumber(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length == 0 ||
            normalized.Length > BusinessRules.PurchaseOrders.CodeMaxLength ||
            !normalized.All(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_' or '.'))
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_ORDER_NUMBER",
                "Số Purchase Order không hợp lệ.");
        }

        OrderNumber = normalized;
        NormalizedOrderNumber = normalized;
    }

    private void SetSupplierId(int value)
    {
        if (value <= 0)
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_SUPPLIER_ID",
                "Nhà cung cấp của Purchase Order không hợp lệ.");
        }

        SupplierId = value;
    }

    private void SetSupplierSnapshot(
        string code,
        string name,
        string? taxCode)
    {
        SupplierCode = NormalizeRequired(
            code,
            BusinessRules.Suppliers.CodeMaxLength,
            "PURCHASE_ORDER.INVALID_SUPPLIER_CODE",
            "Mã nhà cung cấp trong chứng từ không hợp lệ.");
        SupplierName = NormalizeRequired(
            name,
            BusinessRules.Suppliers.NameMaxLength,
            "PURCHASE_ORDER.INVALID_SUPPLIER_NAME",
            "Tên nhà cung cấp trong chứng từ không hợp lệ.");
        SupplierTaxCode = NormalizeOptional(
            taxCode,
            BusinessRules.Suppliers.TaxCodeMaxLength,
            "PURCHASE_ORDER.INVALID_SUPPLIER_TAX_CODE");
    }

    private void SetOrderDate(DateOnly value)
    {
        if (value == default)
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_ORDER_DATE",
                "Ngày Purchase Order không hợp lệ.");
        }

        OrderDate = value;
    }

    private void SetExpectedDeliveryDate(DateOnly? value)
    {
        if (value.HasValue && value.Value < OrderDate)
        {
            throw new DomainException(
                "PURCHASE_ORDER.EXPECTED_DATE_BEFORE_ORDER_DATE",
                "Ngày giao dự kiến không được trước ngày đặt hàng.");
        }

        ExpectedDeliveryDate = value;
    }

    private void SetNotes(string? value)
    {
        Notes = NormalizeOptional(
            value,
            BusinessRules.PurchaseOrders.NotesMaxLength,
            "PURCHASE_ORDER.INVALID_NOTES");
    }

    private void SetCancellationReason(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 ||
            normalized.Length > BusinessRules.PurchaseOrders.CancellationReasonMaxLength ||
            normalized.Any(char.IsControl))
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_CANCELLATION_REASON",
                "Lý do hủy Purchase Order không hợp lệ.");
        }

        CancellationReason = normalized;
    }

    private void SetOrderedByUserId(int value)
    {
        if (value <= 0)
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_ORDERED_BY",
                "Người đặt Purchase Order không hợp lệ.");
        }

        OrderedByUserId = value;
    }

    private static string NormalizeRequired(
        string value,
        int maxLength,
        string code,
        string message)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 ||
            normalized.Length > maxLength ||
            normalized.Any(char.IsControl))
        {
            throw new DomainException(code, message);
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maxLength,
        string code)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
        {
            throw new DomainException(
                code,
                "Thông tin Purchase Order không hợp lệ.");
        }

        return normalized;
    }

    private static long SafeSum(IEnumerable<long> values)
    {
        var total = 0L;
        try
        {
            foreach (var value in values)
            {
                total = checked(total + value);
                if (total > BusinessRules.PurchaseOrders.MaximumOrderAmount)
                {
                    throw new DomainException(
                        "PURCHASE_ORDER.TOTAL_TOO_LARGE",
                        "Tổng Purchase Order vượt quá giới hạn.");
                }
            }

            return total;
        }
        catch (OverflowException exception)
        {
            throw new DomainException(
                "PURCHASE_ORDER.TOTAL_OVERFLOW",
                "Tổng Purchase Order vượt quá giới hạn.",
                exception);
        }
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
    {
        if (value == default)
        {
            throw new DomainException(
                "PURCHASE_ORDER.TIME_REQUIRED",
                "Thời điểm Purchase Order không được để trống.");
        }

        return value.ToUniversalTime();
    }
}
