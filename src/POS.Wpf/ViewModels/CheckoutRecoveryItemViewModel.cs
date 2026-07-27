using System.Globalization;
using POS.Application.DTOs.Checkout;
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
        IsCompleted ? "Giao dịch đã hoàn tất" : "Giao dịch chưa hoàn tất";

    public string StateDescription =>
        IsCompleted
            ? "Đơn đã được lưu. Không thanh toán lại; hãy mở hóa đơn hoặc xác nhận đã xem."
            : "Chưa tạo đơn hàng. Có thể thử lại đúng dữ liệu đã chuẩn bị hoặc bỏ giao dịch.";

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
