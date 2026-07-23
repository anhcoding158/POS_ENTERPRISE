using POS.Domain.Enums;

namespace POS.Application.DTOs.Printing;

/// <summary>
/// Loại bản hóa đơn được tạo.
///
/// Original là bản gốc của giao dịch.
/// Reprint là bản in lại từ snapshot đã lưu.
/// </summary>
public enum ReceiptCopyKind
{
    Original = 1,
    Reprint = 2
}

/// <summary>
/// Snapshot bất biến của thông tin cửa hàng được in
/// trên hóa đơn.
///
/// Không đưa mật khẩu Wi-Fi, secret thanh toán hoặc thông tin
/// nhạy cảm khác vào snapshot.
/// </summary>
public sealed class ReceiptStoreSnapshotDto
{
    private const int MaximumNameLength = 160;
    private const int MaximumAddressLength = 320;
    private const int MaximumPhoneLength = 40;
    private const int MaximumTaxCodeLength = 40;
    private const int MaximumFooterLength = 400;

    private ReceiptStoreSnapshotDto(
        string name,
        string? address,
        string? phone,
        string? taxCode,
        string? footerMessage,
        bool isConfigured)
    {
        Name = name;
        Address = address;
        Phone = phone;
        TaxCode = taxCode;
        FooterMessage = footerMessage;
        IsConfigured = isConfigured;
    }

    public ReceiptStoreSnapshotDto(
        string? name,
        string? address = null,
        string? phone = null,
        string? taxCode = null,
        string? footerMessage = null)
        : this(
            name:
                NormalizeRequiredText(
                    name,
                    nameof(name),
                    MaximumNameLength),

            address:
                NormalizeOptionalText(
                    address,
                    nameof(address),
                    MaximumAddressLength),

            phone:
                NormalizeOptionalText(
                    phone,
                    nameof(phone),
                    MaximumPhoneLength),

            taxCode:
                NormalizeOptionalText(
                    taxCode,
                    nameof(taxCode),
                    MaximumTaxCodeLength),

            footerMessage:
                NormalizeOptionalText(
                    footerMessage,
                    nameof(footerMessage),
                    MaximumFooterLength),

            isConfigured:
                true)
    {
    }

    /// <summary>
    /// Snapshot tạm dùng cho các call site cũ chưa được
    /// truyền cấu hình cửa hàng.
    ///
    /// Print pipeline sẽ không được phép in production
    /// khi IsConfigured bằng false.
    /// </summary>
    public static ReceiptStoreSnapshotDto Unconfigured
    {
        get;
    } =
        new(
            name:
                "CHƯA CẤU HÌNH CỬA HÀNG",

            address:
                null,

            phone:
                null,

            taxCode:
                null,

            footerMessage:
                null,

            isConfigured:
                false);

    public string Name { get; }

    public string? Address { get; }

    public string? Phone { get; }

    public string? TaxCode { get; }

    public string? FooterMessage { get; }

    public bool IsConfigured { get; }

    private static string NormalizeRequiredText(
        string? value,
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
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Giá trị không được vượt quá " +
                $"{maximumLength} ký tự.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value,
        string parameterName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized =
            value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Giá trị không được vượt quá " +
                $"{maximumLength} ký tự.");
        }

        return normalized;
    }
}

/// <summary>
/// Snapshot bất biến chứa toàn bộ dữ liệu nghiệp vụ
/// cần thiết để tạo và in hóa đơn.
///
/// Snapshot không được đọc lại Product, Modifier, Order
/// hoặc thông tin cửa hàng đang sống sau checkout.
/// </summary>
public sealed class ReceiptRequest
{
    /// <summary>
    /// Phiên bản contract snapshot hiện tại.
    /// </summary>
    public const int CurrentSnapshotVersion = 1;

    /// <summary>
    /// Constructor tương thích với call site cũ.
    ///
    /// Snapshot được đánh dấu chưa có cấu hình cửa hàng.
    /// Các call site production mới phải dùng constructor
    /// đầy đủ có ReceiptStoreSnapshotDto.
    /// </summary>
    public ReceiptRequest(
        int orderId,
        string? orderCode,
        string? cashierName,
        DateTimeOffset createdAtUtc,
        PaymentMethod paymentMethod,
        long subtotal,
        long discountAmount,
        long totalAmount,
        long cashReceived,
        long changeAmount,
        IEnumerable<ReceiptLineDto>? lines,
        string? customerName = null,
        string? restaurantTableName = null,
        string? discountCode = null,
        string? notes = null,
        DateTimeOffset? paidAtUtc = null)
        : this(
            store:
                ReceiptStoreSnapshotDto.Unconfigured,

            copyKind:
                ReceiptCopyKind.Original,

            copyNumber:
                0,

            orderId:
                orderId,

            orderCode:
                orderCode,

            cashierName:
                cashierName,

            createdAtUtc:
                createdAtUtc,

            paymentMethod:
                paymentMethod,

            subtotal:
                subtotal,

            discountAmount:
                discountAmount,

            totalAmount:
                totalAmount,

            cashReceived:
                cashReceived,

            changeAmount:
                changeAmount,

            lines:
                lines,

            customerName:
                customerName,

            restaurantTableName:
                restaurantTableName,

            discountCode:
                discountCode,

            notes:
                notes,

            paidAtUtc:
                paidAtUtc)
    {
    }

    /// <summary>
    /// Constructor đầy đủ dành cho snapshot production.
    /// </summary>
    public ReceiptRequest(
        ReceiptStoreSnapshotDto store,
        ReceiptCopyKind copyKind,
        int copyNumber,
        int orderId,
        string? orderCode,
        string? cashierName,
        DateTimeOffset createdAtUtc,
        PaymentMethod paymentMethod,
        long subtotal,
        long discountAmount,
        long totalAmount,
        long cashReceived,
        long changeAmount,
        IEnumerable<ReceiptLineDto>? lines,
        string? customerName = null,
        string? restaurantTableName = null,
        string? discountCode = null,
        string? notes = null,
        DateTimeOffset? paidAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(
            store);

        if (!Enum.IsDefined(copyKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(copyKind),
                "Loại bản hóa đơn không hợp lệ.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(
            copyNumber);

        if (copyKind == ReceiptCopyKind.Original &&
            copyNumber != 0)
        {
            throw new ArgumentException(
                "Bản gốc phải có số thứ tự bản sao bằng 0.",
                nameof(copyNumber));
        }

        if (copyKind == ReceiptCopyKind.Reprint &&
            copyNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(copyNumber),
                "Bản in lại phải có số thứ tự lớn hơn 0.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            orderId);

        if (createdAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(createdAtUtc),
                "Thời điểm tạo đơn không hợp lệ.");
        }

        if (!Enum.IsDefined(paymentMethod))
        {
            throw new ArgumentOutOfRangeException(
                nameof(paymentMethod),
                "Phương thức thanh toán không hợp lệ.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(
            subtotal);

        ArgumentOutOfRangeException.ThrowIfNegative(
            discountAmount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            totalAmount);

        ArgumentOutOfRangeException.ThrowIfNegative(
            cashReceived);

        ArgumentOutOfRangeException.ThrowIfNegative(
            changeAmount);

        var normalizedCreatedAtUtc =
            createdAtUtc.ToUniversalTime();

        var normalizedPaidAtUtc =
            (paidAtUtc ?? createdAtUtc)
                .ToUniversalTime();

        if (normalizedPaidAtUtc <
            normalizedCreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paidAtUtc),
                "Thời điểm thanh toán không được trước " +
                "thời điểm tạo đơn.");
        }

        var lineSnapshots =
            lines?.ToArray() ??
            Array.Empty<ReceiptLineDto>();

        if (lineSnapshots.Length == 0)
        {
            throw new ArgumentException(
                "Hóa đơn phải có ít nhất một dòng hàng.",
                nameof(lines));
        }

        foreach (var line in lineSnapshots)
        {
            ArgumentNullException.ThrowIfNull(
                line);
        }

        var expectedSubtotal =
            SumLineNetAmountsChecked(
                lineSnapshots,
                nameof(subtotal));

        if (subtotal != expectedSubtotal)
        {
            throw new ArgumentException(
                "Tiền hàng không khớp tổng thành tiền " +
                "của các dòng hóa đơn.",
                nameof(subtotal));
        }

        if (discountAmount > subtotal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountAmount),
                "Giảm giá hóa đơn không được lớn hơn " +
                "tiền hàng.");
        }

        var expectedTotalAmount =
            subtotal - discountAmount;

        if (totalAmount != expectedTotalAmount)
        {
            throw new ArgumentException(
                "Tổng thanh toán không khớp tiền hàng " +
                "và giảm giá hóa đơn.",
                nameof(totalAmount));
        }

        ValidatePaymentAmounts(
            paymentMethod,
            totalAmount,
            cashReceived,
            changeAmount);

        SnapshotVersion =
            CurrentSnapshotVersion;

        Store =
            store;

        CopyKind =
            copyKind;

        CopyNumber =
            copyNumber;

        OrderId =
            orderId;

        OrderCode =
            NormalizeRequiredText(
                orderCode,
                nameof(orderCode));

        CashierName =
            NormalizeRequiredText(
                cashierName,
                nameof(cashierName));

        CreatedAtUtc =
            normalizedCreatedAtUtc;

        PaidAtUtc =
            normalizedPaidAtUtc;

        PaymentMethod =
            paymentMethod;

        Subtotal =
            subtotal;

        DiscountAmount =
            discountAmount;

        TotalAmount =
            totalAmount;

        CashReceived =
            cashReceived;

        ChangeAmount =
            changeAmount;

        Lines =
            Array.AsReadOnly(
                lineSnapshots);

        CustomerName =
            NormalizeOptionalText(
                customerName);

        RestaurantTableName =
            NormalizeOptionalText(
                restaurantTableName);

        DiscountCode =
            NormalizeOptionalText(
                discountCode);

        Notes =
            NormalizeOptionalText(
                notes);
    }

    public int SnapshotVersion { get; }

    public ReceiptStoreSnapshotDto Store { get; }

    public ReceiptCopyKind CopyKind { get; }

    /// <summary>
    /// Bản gốc có CopyNumber bằng 0.
    /// Bản in lại đầu tiên có CopyNumber bằng 1.
    /// </summary>
    public int CopyNumber { get; }

    public bool IsReprint =>
        CopyKind == ReceiptCopyKind.Reprint;

    public int OrderId { get; }

    public string OrderCode { get; }

    public string CashierName { get; }

    public string? CustomerName { get; }

    public string? RestaurantTableName { get; }

    public string? DiscountCode { get; }

    public string? Notes { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset PaidAtUtc { get; }

    public PaymentMethod PaymentMethod { get; }

    public long Subtotal { get; }

    public long DiscountAmount { get; }

    public long TotalAmount { get; }

    public long CashReceived { get; }

    public long ChangeAmount { get; }

    public IReadOnlyList<ReceiptLineDto> Lines { get; }

    private static void ValidatePaymentAmounts(
        PaymentMethod paymentMethod,
        long totalAmount,
        long cashReceived,
        long changeAmount)
    {
        if (paymentMethod == PaymentMethod.Cash)
        {
            if (cashReceived < totalAmount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cashReceived),
                    "Tiền khách đưa không đủ thanh toán.");
            }

            var expectedChangeAmount =
                cashReceived - totalAmount;

            if (changeAmount !=
                expectedChangeAmount)
            {
                throw new ArgumentException(
                    "Tiền thừa không khớp tổng thanh toán " +
                    "và tiền khách đưa.",
                    nameof(changeAmount));
            }

            return;
        }

        if (cashReceived != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cashReceived),
                "Thanh toán không dùng tiền mặt không được " +
                "ghi nhận tiền khách đưa.");
        }

        if (changeAmount != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(changeAmount),
                "Thanh toán không dùng tiền mặt không được " +
                "ghi nhận tiền thừa.");
        }
    }

    private static long SumLineNetAmountsChecked(
        IEnumerable<ReceiptLineDto> lines,
        string parameterName)
    {
        try
        {
            var total = 0L;

            foreach (var line in lines)
            {
                total =
                    checked(
                        total +
                        line.NetAmount);
            }

            return total;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Tổng tiền các dòng hóa đơn vượt giới hạn.");
        }
    }

    private static string NormalizeRequiredText(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Giá trị không được để trống.",
                parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}