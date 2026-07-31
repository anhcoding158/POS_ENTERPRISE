using System.Globalization;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Payments;
using POS.Domain.Enums;

namespace POS.Wpf.ViewModels;

public sealed class CheckoutRecoveryItemViewModel
{
    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    public CheckoutRecoveryItemViewModel(CheckoutRecoveryDto recovery)
    {
        Recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }

    public CheckoutRecoveryDto Recovery { get; }

    public Guid ClientRequestId => Recovery.ClientRequestId;

    public bool IsPrepared => Recovery.Status == CheckoutRequestStatus.Prepared;

    public bool IsCompleted => Recovery.Status == CheckoutRequestStatus.Completed;

    public bool CanRetry => Recovery.CanRetry;

    public bool CanAbandon => Recovery.CanAbandon;

    public bool CanOpenReceipt => IsCompleted && Recovery.OrderId.HasValue;

    public string StateTitle =>
        IsCompleted
            ? "ĐƠN ĐÃ ĐƯỢC LƯU"
            : Recovery.HasConfirmedPayment
                ? "Đã xác nhận nhận tiền nhưng đơn hàng chưa hoàn tất"
                : "Giao dịch chưa hoàn tất";

    public string StateDescription =>
        IsCompleted
            ? "Đơn đã được lưu. Không thanh toán lại; hãy mở hóa đơn hoặc tiếp tục bán hàng."
            : Recovery.HasConfirmedPayment
                ? "Đã lưu xác nhận nhận tiền nhưng chưa hoàn tất đơn. Hãy thử hoàn tất đơn; không thể bỏ giao dịch."
                : "Chưa tạo đơn hàng. Có thể thử lại đúng dữ liệu đã chuẩn bị hoặc bỏ giao dịch.";

    public string RetryActionText =>
        Recovery.HasConfirmedPayment
            ? "THỬ HOÀN TẤT ĐƠN"
            : "THỬ LẠI";

    public string OrderDisplay =>
        IsCompleted
            ? Recovery.OrderCode ?? $"Đơn #{Recovery.OrderId}"
            : $"Yêu cầu {Recovery.ClientRequestId:N}";

    public string CreatedAtText =>
        Recovery.CreatedAtUtc.ToLocalTime().ToString(
            "dd/MM/yyyy HH:mm:ss",
            VietnameseCulture);

    public string TotalText =>
        $"{Recovery.TotalAmount.ToString("N0", VietnameseCulture)} ₫";

    public string PaymentMethodText =>
        Recovery.PaymentMethod switch
        {
            PaymentMethod.Cash => "Tiền mặt",
            PaymentMethod.VietQr => "VietQR",
            _ => Recovery.PaymentMethod.ToString()
        };

    public string LineSummary =>
        string.Join(
            " • ",
            Recovery.Lines.Select(
                line => $"{line.ProductName} × {line.Quantity:N0}"));
}

public sealed class PaymentIntentRecoveryItemViewModel
{
    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    public PaymentIntentRecoveryItemViewModel(
        PaymentIntentPendingDto recovery,
        CheckoutRecoveryDto? checkoutRecovery = null)
    {
        Recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        LineSummary = checkoutRecovery is null
            ? string.Empty
            : string.Join(
                " • ",
                checkoutRecovery.Lines.Select(
                    line => $"{line.ProductName} × {line.Quantity:N0}"));
    }

    public PaymentIntentPendingDto Recovery { get; }
    public int Id => Recovery.Id;
    public PaymentIntentStatus Status => Recovery.Status;
    public string DisplayCode => Recovery.DisplayCode;
    public string PayloadText => Recovery.PayloadText;
    public string TransferContent => Recovery.TransferContent;
    public bool CanShowQr => Recovery.CanShowQr;
    public bool CanConfirm => Recovery.CanConfirm;
    public bool CanCancel => Recovery.CanCancel;
    public bool CanRetryCheckout => Recovery.CanRetryCheckout;
    public bool IsManualReview =>
        (Recovery.IsStale || Recovery.IsCrossPaymentConflict) &&
        Status == PaymentIntentStatus.Confirmed;
    public bool CanViewDetails => IsManualReview;
    public bool CanContinueSales => IsManualReview;
    public bool CanResolveManually => IsManualReview;
    public bool CanCloseForLater =>
        Status == PaymentIntentStatus.Confirmed && !IsManualReview;
    public string QrActionText => Status == PaymentIntentStatus.Presented
        ? "XEM LẠI MÃ QR"
        : "XEM MÃ QR";

    public string StateTitle => Status switch
    {
        PaymentIntentStatus.Created => "ĐÃ TẠO MÃ · CHƯA HIỂN THỊ QR",
        PaymentIntentStatus.Presented => "ĐÃ HIỂN THỊ QR · CHƯA XÁC NHẬN TIỀN",
        PaymentIntentStatus.Confirmed when IsManualReview =>
            Recovery.IsCrossPaymentConflict
                ? "ĐÃ NHẬN TIỀN · ĐƠN GIỮ ĐÃ ĐƯỢC THANH TOÁN BẰNG PHƯƠNG THỨC KHÁC"
                : "ĐÃ NHẬN TIỀN · CẦN XỬ LÝ THỦ CÔNG",
        PaymentIntentStatus.Confirmed => "ĐÃ NHẬN TIỀN · ĐƠN CHƯA LƯU",
        _ => Status.ToString().ToUpperInvariant()
    };

    public string Warning => Recovery.IsCrossPaymentConflict
        ? Recovery.Warning ?? string.Empty
        : IsManualReview
            ? "Giao dịch VietQR đã được nhân viên xác nhận nhận tiền nhưng dữ liệu đơn thuộc phiên bản cũ hoặc không thể đọc an toàn.\n\nKhông yêu cầu khách chuyển thêm.\n\nGiao dịch vẫn được giữ nguyên để quản trị viên xử lý."
        : Status == PaymentIntentStatus.Presented
            ? "Hệ thống không tự kiểm tra giao dịch ngân hàng. Chỉ xác nhận sau khi nhân viên đã kiểm tra tiền thực tế."
            : Recovery.Warning ?? string.Empty;

    public string AmountText =>
        $"{Recovery.Amount.ToString("N0", VietnameseCulture)} ₫";
    public string CreatedAtText =>
        Recovery.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", VietnameseCulture);
    public string ExpiresAtText => Recovery.ExpiresAtUtc.HasValue
        ? Recovery.ExpiresAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", VietnameseCulture)
        : "Không giới hạn";
    public string ConfirmedAtText => Recovery.ConfirmedAtUtc.HasValue
        ? Recovery.ConfirmedAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", VietnameseCulture)
        : string.Empty;
    public string ConfirmedByText => Recovery.ConfirmedByUserId.HasValue
        ? $"Nhân viên #{Recovery.ConfirmedByUserId.Value}"
        : string.Empty;
    public bool HasConfirmedMetadata =>
        Status is PaymentIntentStatus.Confirmed or PaymentIntentStatus.Completed;
    public string LineSummary { get; }
    public bool HasLineSummary => !string.IsNullOrWhiteSpace(LineSummary);
}
