using POS.Application.Abstractions.Printing;
using POS.Application.DTOs.Printing;
using POS.Domain.Enums;
using POS.Infrastructure.Printing;
using System.ComponentModel;
using System.Globalization;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace POS.Wpf.Views;

/// <summary>
/// Dialog xem trước và in hóa đơn K80.
///
/// Giao dịch đã hoàn tất trước khi dialog này được mở.
/// Mọi lỗi trong dialog chỉ ảnh hưởng thao tác preview/in,
/// không thay đổi trạng thái Order đã commit.
/// </summary>
public partial class ReceiptPreviewWindow :
    global::System.Windows.Window
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private readonly ReceiptRequest
        _request;

    private readonly IReceiptService
        _receiptService;

    private readonly string
        _printerName;

    private readonly string
        _paperSize;

    private bool _isPrinting;
    private bool _hasPrintedSuccessfully;
    private bool _isClosed;

    public ReceiptPreviewWindow(
        ReceiptRequest request,
        ReceiptDocumentBuilder documentBuilder,
        IReceiptService receiptService,
        string printerName,
        string paperSize)
    {
        _request =
            request ??
            throw new ArgumentNullException(
                nameof(request));

        ArgumentNullException.ThrowIfNull(
            documentBuilder);

        _receiptService =
            receiptService ??
            throw new ArgumentNullException(
                nameof(receiptService));

        if (string.IsNullOrWhiteSpace(
                printerName))
        {
            throw new ArgumentException(
                "Tên máy in không được để trống.",
                nameof(printerName));
        }

        if (string.IsNullOrWhiteSpace(
                paperSize))
        {
            throw new ArgumentException(
                "Khổ giấy không được để trống.",
                nameof(paperSize));
        }

        _printerName =
            printerName.Trim();

        _paperSize =
            paperSize.Trim();

        if (!_request.Store.IsConfigured)
        {
            throw new InvalidOperationException(
                "Không thể mở xem trước hóa đơn vì thông tin " +
                "cửa hàng chưa được cấu hình.");
        }

        InitializeComponent();

        PrinterNameText.Text =
            _printerName;

        PrinterNameText.ToolTip =
            _printerName;

        PrinterPaperSizeText.Text =
            _paperSize;

        ReceiptViewer.Document =
            documentBuilder.Build(
                _request);

        ApplyReceiptSummary();

        Loaded +=
            OnWindowLoaded;
    }

    private void ApplyReceiptSummary()
    {
        OrderCodeText.Text =
            _request.OrderCode;

        PaidAtText.Text =
            _request.PaidAtUtc
                .ToLocalTime()
                .ToString(
                    "dd/MM/yyyy HH:mm",
                    VietnameseCulture);

        CashierText.Text =
            _request.CashierName;

        var totalQuantity =
            _request.Lines.Sum(
                line =>
                    line.Quantity);

        ItemCountText.Text =
            $"{totalQuantity.ToString(
                "N0",
                VietnameseCulture)} món";

        PaymentMethodText.Text =
            FormatPaymentMethod(
                _request.PaymentMethod);

        TotalAmountText.Text =
            FormatMoney(
                _request.TotalAmount);

        CopyKindBadgeText.Text =
            _request.IsReprint
                ? $"BẢN IN LẠI • LẦN " +
                  $"{_request.CopyNumber:N0}"
                : "BẢN GỐC";

        Title =
            $"POS Enterprise - Hóa đơn " +
            $"{_request.OrderCode}";
    }

    private void OnWindowLoaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -=
            OnWindowLoaded;

        FitToAvailableArea();

        PrintButton.Focus();
    }

    private void FitToAvailableArea()
    {
        var owner =
            Owner;

        var hasUsableOwner =
            owner is not null &&
            owner.IsLoaded &&
            owner.ActualWidth > 0 &&
            owner.ActualHeight > 0;

        var availableWidth =
            hasUsableOwner
                ? owner!.ActualWidth
                : SystemParameters.WorkArea.Width;

        var availableHeight =
            hasUsableOwner
                ? owner!.ActualHeight
                : SystemParameters.WorkArea.Height;

        const double safetyMargin =
            24;

        var maximumWidth =
            Math.Max(
                MinWidth,
                availableWidth -
                safetyMargin);

        var maximumHeight =
            Math.Max(
                MinHeight,
                availableHeight -
                safetyMargin);

        MaxWidth =
            maximumWidth;

        MaxHeight =
            maximumHeight;

        Width =
            Math.Min(
                Width,
                maximumWidth);

        Height =
            Math.Min(
                Height,
                maximumHeight);

        if (!hasUsableOwner)
        {
            return;
        }

        Left =
            owner!.Left +
            Math.Max(
                0,
                (owner.ActualWidth -
                 Width) /
                2);

        Top =
            owner.Top +
            Math.Max(
                0,
                (owner.ActualHeight -
                 Height) /
                2);
    }

    private async void OnPrintClick(
        object sender,
        RoutedEventArgs e)
    {
        await PrintAsync();
    }

    private async Task PrintAsync()
    {
        if (_isPrinting ||
            _hasPrintedSuccessfully)
        {
            return;
        }

        SetPrintingState(
            true);

        ShowPrintingStatus();

        try
        {
            var result =
                await _receiptService.PrintAsync(
                    _request);

            if (result.IsFailure)
            {
                SystemSounds.Exclamation
                    .Play();

                ShowErrorStatus(
                    result.Error.Message);

                return;
            }

            _hasPrintedSuccessfully =
                true;

            PrintButtonTitleText.Text =
                "ĐÃ GỬI IN";

            ShowSuccessStatus(
                $"Đã gửi hóa đơn tới {_printerName}.");

            SystemSounds.Asterisk
                .Play();
        }
        catch (OperationCanceledException)
        {
            ShowNeutralStatus(
                "Thao tác in đã được hủy.");
        }
        catch (Exception exception)
        {
            SystemSounds.Hand
                .Play();

            ShowErrorStatus(
                "Không thể gửi hóa đơn tới máy in. " +
                exception
                    .GetBaseException()
                    .Message +
                $"\nMáy in cấu hình: {_printerName}.");
        }
        finally
        {
            SetPrintingState(
                false);
        }
    }

    private void SetPrintingState(
        bool isPrinting)
    {
        _isPrinting =
            isPrinting;

        PrintingOverlay.Visibility =
            isPrinting
                ? Visibility.Visible
                : Visibility.Collapsed;

        PrintingProgress.Visibility =
            isPrinting
                ? Visibility.Visible
                : Visibility.Collapsed;

        CloseButton.IsEnabled =
            !isPrinting;

        PrintButton.IsEnabled =
            !isPrinting &&
            !_hasPrintedSuccessfully;
    }

    private void ShowPrintingStatus()
    {
        SetStatus(
            icon:
                "\uE749",

            message:
                $"Đang gửi hóa đơn tới {_printerName}...",

            backgroundResource:
                "GoldSoftBrush",

            borderResource:
                "GoldBorderBrush",

            foregroundResource:
                "GoldBrush");
    }

    private void ShowNeutralStatus(
        string message)
    {
        SetStatus(
            icon:
                "\uE946",

            message:
                message,

            backgroundResource:
                "GoldSoftBrush",

            borderResource:
                "GoldBorderBrush",

            foregroundResource:
                "GoldBrush");
    }

    private void ShowSuccessStatus(
        string message)
    {
        SetStatus(
            icon:
                "\uE73E",

            message:
                message,

            backgroundResource:
                "SuccessSoftBrush",

            borderResource:
                "BorderBrush",

            foregroundResource:
                "SuccessBrush");
    }

    private void ShowErrorStatus(
        string message)
    {
        SetStatus(
            icon:
                "\uEA39",

            message:
                message,

            backgroundResource:
                "DangerSoftBrush",

            borderResource:
                "BorderStrongBrush",

            foregroundResource:
                "DangerBrush");
    }

    private void SetStatus(
        string icon,
        string message,
        string backgroundResource,
        string borderResource,
        string foregroundResource)
    {
        StatusIcon.Text =
            icon;

        StatusText.Text =
            message;

        StatusBorder.Background =
            FindBrush(
                backgroundResource,
                Brushes.FloralWhite);

        StatusBorder.BorderBrush =
            FindBrush(
                borderResource,
                Brushes.BurlyWood);

        var foreground =
            FindBrush(
                foregroundResource,
                Brushes.DarkGoldenrod);

        StatusIcon.Foreground =
            foreground;

        StatusText.Foreground =
            foreground;
    }

    private Brush FindBrush(
        string resourceKey,
        Brush fallback)
    {
        return TryFindResource(
                   resourceKey) as Brush
               ??
               fallback;
    }

    private void OnCloseClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_isPrinting)
        {
            return;
        }

        Close();
    }

    private void OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (_isPrinting)
        {
            e.Handled =
                true;

            return;
        }

        if (e.Key ==
                Key.P &&
            Keyboard.Modifiers.HasFlag(
                ModifierKeys.Control))
        {
            if (!_hasPrintedSuccessfully)
            {
                _ =
                    PrintAsync();
            }

            e.Handled =
                true;

            return;
        }

        if (e.Key ==
            Key.Escape)
        {
            Close();

            e.Handled =
                true;
        }
    }

    private void OnWindowClosing(
        object? sender,
        CancelEventArgs e)
    {
        if (!_isPrinting)
        {
            return;
        }

        e.Cancel =
            true;

        SystemSounds.Beep
            .Play();

        global::System.Windows.MessageBox.Show(
            this,
            "Hóa đơn đang được gửi tới máy in.\n\n" +
            "Vui lòng chờ thao tác hoàn tất trước khi đóng.",
            "Đang in hóa đơn",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnWindowClosed(
        object? sender,
        EventArgs e)
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed =
            true;

        Loaded -=
            OnWindowLoaded;

        PreviewKeyDown -=
            OnPreviewKeyDown;

        Closing -=
            OnWindowClosing;

        Closed -=
            OnWindowClosed;

        ReceiptViewer.Document =
            null;
    }

    private static string FormatMoney(
        long amount)
    {
        return
            $"{amount.ToString(
                "N0",
                VietnameseCulture)} ₫";
    }

    private static string FormatPaymentMethod(
        PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Cash =>
                "Tiền mặt",

            PaymentMethod.VietQr =>
                "VietQR",

            PaymentMethod.BankTransfer =>
                "Chuyển khoản",

            PaymentMethod.Card =>
                "Thẻ",

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(paymentMethod),
                    paymentMethod,
                    "Phương thức thanh toán không hợp lệ.")
        };
    }
}