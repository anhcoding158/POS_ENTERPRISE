using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using POS.Application.DTOs.Payments;
using POS.Domain.Enums;

namespace POS.Wpf.Views;

public partial class PaymentIntentManualResolutionWindow : Window
{
    private readonly int _paymentIntentId;

    public PaymentIntentManualResolutionWindow(int paymentIntentId)
    {
        InitializeComponent();
        _paymentIntentId = paymentIntentId;
        UpdateFields();
    }

    public ResolvePaymentIntentManuallyRequest? Request { get; private set; }

    private PaymentIntentManualResolutionType SelectedType =>
        (PaymentIntentManualResolutionType)int.Parse(
            ((ComboBoxItem)ResolutionTypeInput.SelectedItem).Tag!.ToString()!,
            CultureInfo.InvariantCulture);

    private void OnResolutionTypeChanged(object sender, SelectionChangedEventArgs e) => UpdateFields();

    private void UpdateFields()
    {
        if (ExternalReferenceInput is null || LinkedOrderIdInput is null)
            return;
        ExternalReferenceInput.Visibility = SelectedType == PaymentIntentManualResolutionType.RefundedExternally
            ? Visibility.Visible : Visibility.Collapsed;
        LinkedOrderIdInput.Visibility = SelectedType == PaymentIntentManualResolutionType.LinkExistingOrder
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReasonInput.Text))
        {
            MessageBox.Show(this, "Phải nhập lý do xử lý.", "Thiếu lý do",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        int? orderId = null;
        if (SelectedType == PaymentIntentManualResolutionType.LinkExistingOrder)
        {
            if (!int.TryParse(LinkedOrderIdInput.Text, out var parsed) || parsed <= 0)
            {
                MessageBox.Show(this, "Phải nhập ID hóa đơn chính xác.", "Thiếu hóa đơn",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            orderId = parsed;
        }
        if (SelectedType == PaymentIntentManualResolutionType.RefundedExternally &&
            string.IsNullOrWhiteSpace(ExternalReferenceInput.Text))
        {
            MessageBox.Show(this, "Phải nhập mã hoặc ghi chú hoàn tiền ngoài POS.",
                "Thiếu tham chiếu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show(this,
                "Thao tác này kết thúc hồ sơ recovery và được ghi audit. Bạn chắc chắn muốn tiếp tục?",
                "Xác nhận xử lý thủ công", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
        Request = new ResolvePaymentIntentManuallyRequest(
            _paymentIntentId, SelectedType, ReasonInput.Text,
            ExternalReferenceInput.Text, orderId);
        DialogResult = true;
    }
}
