using POS.Wpf.Services;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace POS.Wpf.Views;

/// <summary>
/// Hiển thị VietQR, in phiếu thanh toán và yêu cầu
/// thu ngân xác nhận thủ công.
///
/// Việc in phiếu không:
/// - gọi CheckoutService;
/// - tạo Order;
/// - xác nhận tiền đã vào;
/// - đánh dấu giao dịch đã thanh toán.
///
/// Mã tham chiếu kỹ thuật đầy đủ vẫn được giữ trong
/// VietQrPaymentPresentation để phục vụ retry và đối soát.
///
/// Giao diện khách hàng và phiếu in chỉ hiển thị
/// mã thanh toán ngắn, dễ đọc.
/// </summary>
public partial class VietQrPaymentWindow :
    global::System.Windows.Window
{
    private const double
        K80PageWidth =
            302.36;

    private const double
        K80HorizontalMargin =
            11.34;

    private const double
        K80VerticalMargin =
            9.45;

    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private static readonly FontFamily
        SlipFontFamily =
            new("Segoe UI");

    private readonly VietQrPaymentPresentation
        _presentation;

    private bool
        _isConfirmed;

    private bool
        _isPrinting;

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

        if (_presentation.QrPngBytes is null ||
            _presentation.QrPngBytes.Length == 0)
        {
            throw new ArgumentException(
                "Ảnh VietQR không được để trống.",
                nameof(presentation));
        }

        if (string.IsNullOrWhiteSpace(
                _presentation.PaymentReference))
        {
            throw new ArgumentException(
                "Mã tham chiếu VietQR không được để trống.",
                nameof(presentation));
        }

        if (string.IsNullOrWhiteSpace(
                _presentation.TransferContent))
        {
            throw new ArgumentException(
                "Nội dung chuyển khoản không được để trống.",
                nameof(presentation));
        }

        InitializeComponent();

        ApplyPresentation();

        Loaded +=
            OnWindowLoaded;
    }

    private void ApplyPresentation()
    {
        var customerPaymentCode =
            CreateCustomerPaymentCode(
                _presentation.TransferContent);

        AmountText.Text =
            FormatMoney(
                _presentation.Amount);

        TransferContentText.Text =
            _presentation.TransferContent;

        /*
         * Không đưa PaymentReference kỹ thuật dạng QR2026...
         * lên màn hình cho khách hoặc thu ngân.
         *
         * Giá trị đầy đủ vẫn còn nguyên trong
         * _presentation.PaymentReference và được trả về
         * cho SalesPaymentFlowService khi xác nhận.
         */
        PaymentReferenceText.Text =
            $"Mã thanh toán: " +
            $"{customerPaymentCode}";

        QrImage.Source =
            CreateBitmapImage(
                _presentation.QrPngBytes);

        Title =
            $"POS Enterprise - VietQR " +
            $"{customerPaymentCode}";

        var hasRecipientDetails =
            !string.IsNullOrWhiteSpace(
                _presentation.BankName) &&
            !string.IsNullOrWhiteSpace(
                _presentation.AccountName);

        if (hasRecipientDetails)
        {
            BankNameText.Text =
                _presentation.BankName;

            AccountNameText.Text =
                _presentation.AccountName;

            RecipientDetailsPanel.Visibility =
                Visibility.Visible;

            RecipientInformationMessagePanel.Visibility =
                Visibility.Collapsed;
        }
        else
        {
            RecipientDetailsPanel.Visibility =
                Visibility.Collapsed;

            RecipientInformationMessageText.Text =
                string.IsNullOrWhiteSpace(
                    _presentation
                        .RecipientInformationMessage)
                    ? "Thông tin người nhận chưa khả dụng."
                    : _presentation
                        .RecipientInformationMessage;

            RecipientInformationMessagePanel.Visibility =
                Visibility.Visible;
        }
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
            isChecked &&
            !_isPrinting;

        StatusText.Text =
            isChecked
                ? "Đã tích xác nhận. Hãy kiểm tra lần cuối " +
                  "trước khi hoàn tất."
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
        if (_isPrinting)
        {
            SystemSounds.Exclamation.Play();

            StatusText.Text =
                "Hãy chờ tác vụ in hoàn tất.";

            return;
        }

        if (ConfirmationCheckBox.IsChecked !=
            true)
        {
            SystemSounds.Exclamation.Play();

            StatusText.Text =
                "Bạn phải kiểm tra tài khoản và tích xác nhận " +
                "đã nhận đủ tiền.";

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
        if (_isPrinting)
        {
            SystemSounds.Exclamation.Play();

            StatusText.Text =
                "Hãy chờ tác vụ in hoàn tất.";

            return;
        }

        DialogResult =
            false;

        Close();
    }

    /*
     * Handler được giữ để tương thích nếu một XAML cũ
     * vẫn còn gọi đến nó.
     *
     * XAML production hiện tại không hiển thị nút sao chép.
     */
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
            SystemSounds.Exclamation.Play();

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

            SystemSounds.Asterisk.Play();
        }
        catch (Exception exception)
        {
            SystemSounds.Exclamation.Play();

            StatusText.Text =
                "Không thể sao chép: " +
                exception
                    .GetBaseException()
                    .Message;
        }
    }

    private void OnPrintSlipClick(
        object sender,
        RoutedEventArgs e)
    {
        PrintPaymentSlip();
    }

    private void PrintPaymentSlip()
    {
        if (_isPrinting)
        {
            return;
        }

        _isPrinting =
            true;

        PrintSlipButton.IsEnabled =
            false;

        ConfirmButton.IsEnabled =
            false;

        StatusText.Text =
            "Đang chuẩn bị phiếu thanh toán VietQR...";

        try
        {
            var printDialog =
                new PrintDialog();

            var accepted =
                printDialog.ShowDialog() ==
                true;

            if (!accepted)
            {
                StatusText.Text =
                    "Đã hủy in phiếu VietQR.";

                return;
            }

            var document =
                BuildPaymentSlipDocument();

            ConfigureDocumentForPrinter(
                document,
                printDialog);

            var paginator =
                ((IDocumentPaginatorSource)
                    document)
                .DocumentPaginator;

            if (IsValidDimension(
                    document.PageWidth) &&
                IsValidDimension(
                    document.PageHeight))
            {
                paginator.PageSize =
                    new Size(
                        document.PageWidth,
                        document.PageHeight);
            }

            var customerPaymentCode =
                CreateCustomerPaymentCode(
                    _presentation.TransferContent);

            printDialog.PrintDocument(
                paginator,
                $"Phiếu VietQR " +
                $"{customerPaymentCode}");

            StatusText.Text =
                "Đã gửi phiếu QR đến máy in. " +
                "Việc in phiếu không đồng nghĩa cửa hàng " +
                "đã nhận tiền.";

            SystemSounds.Asterisk.Play();
        }
        catch (Exception exception)
        {
            SystemSounds.Exclamation.Play();

            StatusText.Text =
                "Không thể in phiếu VietQR: " +
                exception
                    .GetBaseException()
                    .Message;
        }
        finally
        {
            _isPrinting =
                false;

            PrintSlipButton.IsEnabled =
                true;

            ConfirmButton.IsEnabled =
                ConfirmationCheckBox.IsChecked ==
                true;
        }
    }

    private FlowDocument
        BuildPaymentSlipDocument()
    {
        var customerPaymentCode =
            CreateCustomerPaymentCode(
                _presentation.TransferContent);

        var document =
            new FlowDocument
            {
                PageWidth =
                    K80PageWidth,

                PagePadding =
                    new Thickness(
                        K80HorizontalMargin,
                        K80VerticalMargin,
                        K80HorizontalMargin,
                        K80VerticalMargin),

                ColumnWidth =
                    double.PositiveInfinity,

                IsColumnWidthFlexible =
                    true,

                FontFamily =
                    SlipFontFamily,

                FontSize =
                    9.5,

                Foreground =
                    Brushes.Black,

                Background =
                    Brushes.White,

                LineStackingStrategy =
                    LineStackingStrategy
                        .BlockLineHeight,

                LineHeight =
                    13,

                TextAlignment =
                    TextAlignment.Left
            };

        document.Blocks.Add(
            CreateSlipParagraph(
                "POS ENTERPRISE",
                14,
                FontWeights.Bold,
                TextAlignment.Center,
                new Thickness(
                    0,
                    0,
                    0,
                    2)));

        document.Blocks.Add(
            CreateSlipParagraph(
                "PHIẾU THANH TOÁN VIETQR",
                13,
                FontWeights.Bold,
                TextAlignment.Center,
                new Thickness(
                    0,
                    0,
                    0,
                    2)));

        document.Blocks.Add(
            CreateSlipParagraph(
                "CHƯA XÁC NHẬN THANH TOÁN",
                8.5,
                FontWeights.Bold,
                TextAlignment.Center,
                new Thickness(
                    0,
                    0,
                    0,
                    7)));

        AddSlipRule(
            document);

        var qrImage =
            new Image
            {
                Source =
                    CreateBitmapImage(
                        _presentation.QrPngBytes),

                Width =
                    210,

                Height =
                    210,

                Stretch =
                    Stretch.Uniform,

                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        RenderOptions.SetBitmapScalingMode(
            qrImage,
            BitmapScalingMode
                .NearestNeighbor);

        document.Blocks.Add(
            new BlockUIContainer(
                qrImage)
            {
                Margin =
                    new Thickness(
                        0,
                        7,
                        0,
                        7)
            });

        document.Blocks.Add(
            CreateSlipParagraph(
                "SỐ TIỀN CẦN THANH TOÁN",
                8.5,
                FontWeights.SemiBold,
                TextAlignment.Center,
                new Thickness(
                    0,
                    0,
                    0,
                    2)));

        document.Blocks.Add(
            CreateSlipParagraph(
                FormatMoney(
                    _presentation.Amount),
                20,
                FontWeights.Bold,
                TextAlignment.Center,
                new Thickness(
                    0,
                    0,
                    0,
                    8)));

        AddSlipRule(
            document);

        document.Blocks.Add(
            CreateLabelValueParagraph(
                "Nội dung CK:",
                _presentation.TransferContent));

        /*
         * Chỉ in mã ngắn dành cho người dùng.
         * Không in PaymentReference nội bộ dạng QR2026...
         */
        document.Blocks.Add(
            CreateLabelValueParagraph(
                "Mã thanh toán:",
                customerPaymentCode));

        document.Blocks.Add(
            CreateLabelValueParagraph(
                "Thời gian tạo:",
                DateTimeOffset.Now.ToString(
                    "dd/MM/yyyy HH:mm:ss",
                    VietnameseCulture)));

        AddSlipRule(
            document);

        document.Blocks.Add(
            CreateSlipParagraph(
                "HƯỚNG DẪN",
                9,
                FontWeights.Bold,
                TextAlignment.Center,
                new Thickness(
                    0,
                    0,
                    0,
                    4)));

        document.Blocks.Add(
            CreateSlipParagraph(
                "1. Mở ứng dụng ngân hàng và quét mã QR.",
                8.5,
                FontWeights.Normal,
                TextAlignment.Left,
                new Thickness(
                    2,
                    0,
                    2,
                    2)));

        document.Blocks.Add(
            CreateSlipParagraph(
                "2. Kiểm tra đúng người nhận, số tiền " +
                "và nội dung chuyển khoản.",
                8.5,
                FontWeights.Normal,
                TextAlignment.Left,
                new Thickness(
                    2,
                    0,
                    2,
                    2)));

        document.Blocks.Add(
            CreateSlipParagraph(
                "3. Chỉ hoàn tất khi thu ngân xác nhận " +
                "cửa hàng đã nhận tiền.",
                8.5,
                FontWeights.Normal,
                TextAlignment.Left,
                new Thickness(
                    2,
                    0,
                    2,
                    5)));

        document.Blocks.Add(
            CreateSlipParagraph(
                "Phiếu này chỉ dùng để thanh toán. " +
                "Đây chưa phải hóa đơn bán hàng và chưa " +
                "chứng minh tiền đã vào tài khoản.",
                8,
                FontWeights.SemiBold,
                TextAlignment.Center,
                new Thickness(
                    3,
                    3,
                    3,
                    4)));

        document.Blocks.Add(
            CreateSlipParagraph(
                "────────  ◇  ────────",
                8,
                FontWeights.Normal,
                TextAlignment.Center,
                new Thickness(0)));

        return document;
    }

    private static void
        ConfigureDocumentForPrinter(
            FlowDocument document,
            PrintDialog printDialog)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentNullException.ThrowIfNull(
            printDialog);

        var printableWidth =
            printDialog.PrintableAreaWidth;

        var printableHeight =
            printDialog.PrintableAreaHeight;

        if (IsValidDimension(
                printableWidth) &&
            printableWidth <
            document.PageWidth)
        {
            document.PageWidth =
                printableWidth;
        }

        if (IsValidDimension(
                printableHeight))
        {
            document.PageHeight =
                printableHeight;
        }

        document.ColumnWidth =
            double.PositiveInfinity;
    }

    private static Paragraph
        CreateLabelValueParagraph(
            string label,
            string value)
    {
        var paragraph =
            new Paragraph
            {
                Margin =
                    new Thickness(
                        2,
                        1,
                        2,
                        2),

                FontSize =
                    8.8,

                TextAlignment =
                    TextAlignment.Left
            };

        paragraph.Inlines.Add(
            new Bold(
                new Run(
                    label + " ")));

        paragraph.Inlines.Add(
            new Run(
                value));

        return paragraph;
    }

    private static Paragraph
        CreateSlipParagraph(
            string text,
            double fontSize,
            FontWeight fontWeight,
            TextAlignment textAlignment,
            Thickness margin)
    {
        return new Paragraph(
            new Run(
                text))
        {
            Margin =
                margin,

            FontFamily =
                SlipFontFamily,

            FontSize =
                fontSize,

            FontWeight =
                fontWeight,

            TextAlignment =
                textAlignment,

            Foreground =
                Brushes.Black
        };
    }

    private static void AddSlipRule(
        FlowDocument document)
    {
        document.Blocks.Add(
            new Paragraph(
                new Run(" "))
            {
                Margin =
                    new Thickness(
                        0,
                        3,
                        0,
                        4),

                BorderBrush =
                    Brushes.Gray,

                BorderThickness =
                    new Thickness(
                        0,
                        0,
                        0,
                        0.7),

                FontSize =
                    1,

                LineHeight =
                    1
            });
    }

    private void OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key ==
            Key.Escape)
        {
            if (!_isPrinting)
            {
                DialogResult =
                    false;

                Close();
            }

            e.Handled =
                true;

            return;
        }

        if (e.Key ==
                Key.P &&
            Keyboard.Modifiers.HasFlag(
                ModifierKeys.Control))
        {
            PrintPaymentSlip();

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
        if (_isPrinting)
        {
            e.Cancel =
                true;

            StatusText.Text =
                "Hãy chờ tác vụ in hoàn tất.";

            return;
        }

        if (!_isConfirmed &&
            DialogResult is null)
        {
            DialogResult =
                false;
        }
    }

    /// <summary>
    /// Tạo mã ngắn chỉ dùng để hiển thị cho người dùng.
    ///
    /// Ví dụ:
    /// POS 2607 184812 000001
    /// →
    /// 2607 184812 000001
    ///
    /// Không thay đổi payload hoặc mã tham chiếu kỹ thuật.
    /// </summary>
    private static string
        CreateCustomerPaymentCode(
            string transferContent)
    {
        if (string.IsNullOrWhiteSpace(
                transferContent))
        {
            return "—";
        }

        var normalized =
            transferContent.Trim();

        var parts =
            normalized.Split(
                ' ',
                StringSplitOptions
                    .RemoveEmptyEntries |
                StringSplitOptions
                    .TrimEntries);

        /*
         * Luồng mới tạo nội dung theo dạng:
         * PREFIX DDMM HHMMSS SEQUENCE.
         *
         * Lấy ba nhóm cuối để không phụ thuộc tiền tố
         * đang cấu hình là POS, SHOP, CAFE...
         */
        if (parts.Length >= 3)
        {
            return
                $"{parts[^3]} " +
                $"{parts[^2]} " +
                $"{parts[^1]}";
        }

        return normalized;
    }

    private static BitmapImage
        CreateBitmapImage(
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

        bitmap.CacheOption =
            BitmapCacheOption.OnLoad;

        bitmap.StreamSource =
            stream;

        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }

    private static bool IsValidDimension(
        double value)
    {
        return double.IsFinite(
                   value) &&
               value > 0;
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