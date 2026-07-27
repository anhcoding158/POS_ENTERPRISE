namespace POS.Wpf.Services;

public interface ICheckoutRecoveryConfirmationService
{
    bool ConfirmAbandon();
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
}
