namespace POS.Wpf.Services;

public interface ICheckoutRecoveryConfirmationService
{
    bool ConfirmAbandon();
    bool ConfirmClearCart() => false;
    bool ConfirmCancelPaymentIntent(string displayCode) => false;
    bool ConfirmPaymentReceived(long amount) => false;
    bool ConfirmContinueSales() => false;
}

public sealed class CheckoutRecoveryConfirmationService :
    ICheckoutRecoveryConfirmationService
{
    public bool ConfirmAbandon() =>
        global::System.Windows.MessageBox.Show(
            "Giao dịch này chưa tạo đơn hàng. Bỏ giao dịch dang dở?",
            "Bỏ giao dịch dang dở",
            global::System.Windows.MessageBoxButton.YesNo,
            global::System.Windows.MessageBoxImage.Warning,
            global::System.Windows.MessageBoxResult.No) ==
        global::System.Windows.MessageBoxResult.Yes;

    public bool ConfirmClearCart() =>
        global::System.Windows.MessageBox.Show(
            "Làm trống toàn bộ giỏ hàng hiện tại?",
            "Làm trống giỏ hàng",
            global::System.Windows.MessageBoxButton.YesNo,
            global::System.Windows.MessageBoxImage.Warning,
            global::System.Windows.MessageBoxResult.No) ==
        global::System.Windows.MessageBoxResult.Yes;

    public bool ConfirmCancelPaymentIntent(string displayCode) =>
        global::System.Windows.MessageBox.Show(
            $"Hủy mã VietQR {displayCode}?\n\nMã đã hủy sẽ không thể xác nhận nhận tiền.",
            "Hủy mã VietQR",
            global::System.Windows.MessageBoxButton.YesNo,
            global::System.Windows.MessageBoxImage.Warning,
            global::System.Windows.MessageBoxResult.No) ==
        global::System.Windows.MessageBoxResult.Yes;

    public bool ConfirmPaymentReceived(long amount) =>
        global::System.Windows.MessageBox.Show(
            $"Bạn xác nhận cửa hàng đã nhận đủ {amount:N0} ₫ qua chuyển khoản?\n\n" +
            "Hệ thống không tự kiểm tra giao dịch ngân hàng.\n" +
            "Chỉ xác nhận sau khi nhân viên đã kiểm tra tiền thực tế.",
            "Xác nhận đã nhận tiền",
            global::System.Windows.MessageBoxButton.YesNo,
            global::System.Windows.MessageBoxImage.Warning,
            global::System.Windows.MessageBoxResult.No) ==
        global::System.Windows.MessageBoxResult.Yes;

    public bool ConfirmContinueSales() =>
        global::System.Windows.MessageBox.Show(
            "Giao dịch VietQR đã xác nhận nhận tiền vẫn chưa được xử lý.\n\n" +
            "Giao dịch sẽ tiếp tục được lưu và hiển thị cảnh báo trên quầy bán hàng.\n\n" +
            "Bạn có chắc muốn tiếp tục bán hàng?",
            "Tiếp tục bán hàng",
            global::System.Windows.MessageBoxButton.YesNo,
            global::System.Windows.MessageBoxImage.Warning,
            global::System.Windows.MessageBoxResult.No) ==
        global::System.Windows.MessageBoxResult.Yes;
}
