using POS.Wpf.Services;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace POS.Wpf.Views;

/// <summary>
/// Dialog hiển thị VietQR và yêu cầu thu ngân xác nhận.
///
/// Dialog không:
/// - gọi CheckoutService;
/// - tạo Order;
/// - tự xác nhận giao dịch ngân hàng;
/// - tự đóng với kết quả thành công.
/// </summary>
public partial class VietQrPaymentWindow :
    global::System.Windows.Window
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private readonly VietQrPaymentPresentation
        _presentation;

    private bool
        _isConfirmed;

    public VietQrPaymentWindow(
        VietQrPaymentPresentation presentation)
    {
        _presentation =
            presentation ??
            throw new ArgumentNullException(
                nameof(presentation));

        if (_presentation.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(presentation),
                "Số tiền VietQR phải lớn hơn 0.");
        }

        if (_presentation.QrPngBytes.Length == 0)
        {
            throw new ArgumentException(
                "Ảnh VietQR không được để trống.",
                nameof(presentation));
        }

        InitializeComponent();

        ApplyPresentation();

        Loaded +=
            OnWindowLoaded;
    }

    private void ApplyPresentation()
    {
        AmountText.Text =
            FormatMoney(
                _presentation.Amount);

        BankBinText.Text =
            _presentation.BankBin;

        AccountNumberText.Text =
            _presentation.AccountNumber;

        AccountNameText.Text =
            _presentation.AccountName;

        TransferContentText.Text =
            _presentation.TransferContent;

        PaymentReferenceText.Text =
            $"Mã tham chiếu: " +
            $"{_presentation.PaymentReference}";

        QrImage.Source =
            CreateBitmapImage(
                _presentation.QrPngBytes);

        Title =
            $"POS Enterprise - VietQR " +
            $"{_presentation.PaymentReference}";
    }

    private void OnWindowLoaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -=
            OnWindowLoaded;

        ConfirmationCheckBox.Focus();
    }

    private void OnConfirmationChanged(
        object sender,
        RoutedEventArgs e)
    {
        var isChecked =
            ConfirmationCheckBox.IsChecked ==
            true;

        ConfirmButton.IsEnabled =
            isChecked;

        StatusText.Text =
            isChecked
                ? "Đã tích xác nhận. Hãy kiểm tra lần cuối trước khi hoàn tất."
                : "Đang chờ thu ngân kiểm tra giao dịch.";
    }

    private void OnConfirmClick(
        object sender,
        RoutedEventArgs e)
    {
        ConfirmPayment();
    }

    private void ConfirmPayment()
    {
        if (ConfirmationCheckBox.IsChecked !=
            true)
        {
            SystemSounds.Exclamation
                .Play();

            StatusText.Text =
                "Bạn phải tích xác nhận đã nhận đủ tiền.";

            return;
        }

        _isConfirmed =
            true;

        DialogResult =
            true;

        Close();
    }

    private void OnCancelClick(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult =
            false;

        Close();
    }

    private void OnCopyAccountNumberClick(
        object sender,
        RoutedEventArgs e)
    {
        CopyText(
            _presentation.AccountNumber,
            "Đã sao chép số tài khoản.");
    }

    private void OnCopyTransferContentClick(
        object sender,
        RoutedEventArgs e)
    {
        CopyText(
            _presentation.TransferContent,
            "Đã sao chép nội dung chuyển khoản.");
    }

    private void CopyText(
        string text,
        string successMessage)
    {
        if (string.IsNullOrWhiteSpace(
                text))
        {
            SystemSounds.Exclamation
                .Play();

            StatusText.Text =
                "Không có dữ liệu để sao chép.";

            return;
        }

        try
        {
            Clipboard.SetText(
                text);

            StatusText.Text =
                successMessage;

            SystemSounds.Asterisk
                .Play();
        }
        catch (Exception exception)
        {
            SystemSounds.Exclamation
                .Play();

            StatusText.Text =
                "Không thể sao chép: " +
                exception
                    .GetBaseException()
                    .Message;
        }
    }

    private void OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key ==
            Key.Escape)
        {
            DialogResult =
                false;

            Close();

            e.Handled =
                true;

            return;
        }

        if ((e.Key ==
                Key.Enter ||
             e.Key ==
                Key.Return) &&
            Keyboard.Modifiers.HasFlag(
                ModifierKeys.Control))
        {
            ConfirmPayment();

            e.Handled =
                true;
        }
    }

    private void OnWindowClosing(
        object? sender,
        CancelEventArgs e)
    {
        /*
         * Nút X, Alt+F4 hoặc cancellation đều được xem
         * là hủy nếu chưa xác nhận.
         */
        if (!_isConfirmed &&
            DialogResult is null)
        {
            DialogResult =
                false;
        }
    }

    private static BitmapImage CreateBitmapImage(
        byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(
            pngBytes);

        using var stream =
            new MemoryStream(
                pngBytes,
                writable:
                    false);

        var bitmap =
            new BitmapImage();

        bitmap.BeginInit();

        /*
         * OnLoad đọc toàn bộ dữ liệu trước khi stream
         * bị dispose.
         */
        bitmap.CacheOption =
            BitmapCacheOption.OnLoad;

        bitmap.StreamSource =
            stream;

        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }

    private static string FormatMoney(
        long amount)
    {
        return
            $"{amount.ToString(
                "N0",
                VietnameseCulture)} ₫";
    }
}