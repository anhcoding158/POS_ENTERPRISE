using System.Globalization;
using POS.Application.DTOs.Orders;
using POS.Domain.Enums;

namespace POS.Wpf.ViewModels;

public sealed class OrderHistoryRowViewModel
{
    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    public OrderHistoryRowViewModel(OrderHistoryListItemDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        OrderId = source.OrderId;
        OrderCode = source.OrderCode;
        CreatedAtUtc = source.CreatedAtUtc;
        PaidAtUtc = source.PaidAtUtc;
        CashierName = source.CashierName;
        Status = source.Status;
        PaymentMethod = source.PaymentMethod;
        TotalAmount = source.TotalAmount;
        CashReceived = source.CashReceived;
        ChangeAmount = source.ChangeAmount;
    }

    public int OrderId { get; }
    public string OrderCode { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset? PaidAtUtc { get; }
    public string CashierName { get; }
    public OrderStatus Status { get; }
    public PaymentMethod? PaymentMethod { get; }
    public long TotalAmount { get; }
    public long CashReceived { get; }
    public long ChangeAmount { get; }
    public string CreatedAtText =>
        CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", VietnameseCulture);
    public string TotalAmountText =>
        TotalAmount.ToString("N0", VietnameseCulture) + " ₫";
    public string PaymentText => PaymentMethod switch
    {
        global::POS.Domain.Enums.PaymentMethod.Cash => "Tiền mặt",
        global::POS.Domain.Enums.PaymentMethod.VietQr => "VietQR",
        global::POS.Domain.Enums.PaymentMethod.BankTransfer => "Chuyển khoản",
        global::POS.Domain.Enums.PaymentMethod.Card => "Thẻ",
        _ => "Chưa thanh toán"
    };
    public string StatusText => Status switch
    {
        OrderStatus.Draft => "Nháp",
        OrderStatus.PendingPayment => "Chờ thanh toán",
        OrderStatus.Paid => "Đã thanh toán",
        OrderStatus.Completed => "Hoàn thành",
        OrderStatus.Cancelled => "Đã hủy",
        OrderStatus.PartiallyRefunded => "Hoàn một phần",
        OrderStatus.Refunded => "Đã hoàn",
        _ => Status.ToString()
    };
}
