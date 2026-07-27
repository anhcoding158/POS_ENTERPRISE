using POS.Wpf.Services;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace POS.Wpf.Views;

/// <summary>
/// Màn hình chỉ đọc dành cho khách hàng.
///
/// Cửa sổ:
/// - tự tìm màn hình khác màn thu ngân;
/// - không nhận focus;
/// - không có nút thao tác;
/// - không tự xác nhận thanh toán;
/// - không gọi CheckoutService;
/// - không tạo hoặc sửa Order;
/// - không hiển thị mã tham chiếu kỹ thuật.
/// </summary>
public partial class VietQrCustomerDisplayWindow :
    global::System.Windows.Window
{
    private const uint MonitorDefaultToNearest =
        0x00000002;

    private const uint MonitorInfoPrimary =
        0x00000001;

    private const uint SetWindowPositionNoActivate =
        0x0010;

    private const uint SetWindowPositionShowWindow =
        0x0040;

    private const uint SetWindowPositionNoOwnerZOrder =
        0x0200;

    private static readonly IntPtr TopMostWindow =
        new(-1);

    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private readonly VietQrPaymentPresentation
        _presentation;

    private bool _completionStateShown;

    public VietQrCustomerDisplayWindow(
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
                _presentation.TransferContent))
        {
            throw new ArgumentException(
                "Nội dung chuyển khoản không được để trống.",
                nameof(presentation));
        }

        InitializeComponent();

        ApplyPresentation();
    }

    /// <summary>
    /// Mở cửa sổ trên một màn hình khác màn thu ngân.
    ///
    /// Trả false là trạng thái bình thường khi máy chỉ có
    /// một màn hình. Luồng thanh toán trên màn thu ngân
    /// vẫn tiếp tục nguyên vẹn.
    /// </summary>
    public bool TryShowOnSecondaryMonitor(
        Window cashierWindow)
    {
        ArgumentNullException.ThrowIfNull(
            cashierWindow);

        if (IsVisible)
        {
            return true;
        }

        var targetMonitor =
            TryFindTargetMonitor(
                cashierWindow);

        if (targetMonitor is null)
        {
            return false;
        }

        try
        {
            /*
             * Giữ cửa sổ trong suốt trong vài mili giây đầu,
             * tránh hiện chớp trên màn thu ngân trước khi
             * SetWindowPos chuyển cửa sổ sang màn khách.
             */
            Opacity = 0;

            Show();

            if (!ApplyMonitorBounds(
                    targetMonitor.Bounds))
            {
                CloseSafely();

                return false;
            }

            Opacity = 1;

            /*
             * Áp dụng lại sau khi WPF xử lý DPI và layout.
             * Điều này ổn định hơn với hai màn hình có
             * mức Scale khác nhau.
             */
            _ =
                Dispatcher.BeginInvoke(
                    () =>
                    {
                        if (IsVisible)
                        {
                            ApplyMonitorBounds(
                                targetMonitor.Bounds);
                        }
                    },
                    DispatcherPriority.Loaded);

            return true;
        }
        catch
        {
            CloseSafely();

            return false;
        }
    }

    /// <summary>
    /// Hiển thị trạng thái thu ngân đã xác nhận tiền
    /// thực tế vào tài khoản, sau đó tự đóng.
    ///
    /// Đây không tuyên bố Order đã được lưu thành công.
    /// Checkout vẫn tiếp tục theo luồng hiện tại.
    /// </summary>
    public async Task
        ShowPaymentReceivedAndCloseAsync(
            TimeSpan visibleDuration)
    {
        if (visibleDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visibleDuration));
        }

        try
        {
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(
                    ShowPaymentReceived);

                await Task.Delay(
                        visibleDuration)
                    .ConfigureAwait(false);

                if (!Dispatcher.HasShutdownStarted)
                {
                    await Dispatcher.InvokeAsync(
                        CloseSafely);
                }

                return;
            }

            ShowPaymentReceived();

            await Task.Delay(
                    visibleDuration)
                .ConfigureAwait(true);

            CloseSafely();
        }
        catch
        {
            /*
             * Màn khách là Presentation bổ trợ.
             * Không được để lỗi đóng hoặc Dispatcher shutdown
             * làm hỏng authorization VietQR đã xác nhận.
             */
            CloseSafely();
        }
    }

    public void CloseSafely()
    {
        try
        {
            if (!Dispatcher.CheckAccess())
            {
                _ =
                    Dispatcher.BeginInvoke(
                        CloseSafely);

                return;
            }

            if (IsVisible)
            {
                Close();
            }
        }
        catch
        {
            /*
             * Đóng màn khách là best-effort.
             */
        }
    }

    private void ApplyPresentation()
    {
        var paymentCode =
            CreateCustomerPaymentCode(
                _presentation.TransferContent);

        QrImage.Source =
            CreateBitmapImage(
                _presentation.QrPngBytes);

        AmountText.Text =
            FormatMoney(
                _presentation.Amount);

        TransferContentText.Text =
            _presentation.TransferContent;

        PaymentCodeText.Text =
            $"Mã thanh toán: {paymentCode}";

        var hasRecipientDetails =
            !string.IsNullOrWhiteSpace(
                _presentation.BankName) &&
            !string.IsNullOrWhiteSpace(
                _presentation.AccountName);

        if (hasRecipientDetails)
        {
            CustomerBankNameText.Text =
                _presentation.BankName;

            CustomerAccountNameText.Text =
                _presentation.AccountName;

            CustomerRecipientDetailsPanel.Visibility =
                Visibility.Visible;

            CustomerRecipientMessagePanel.Visibility =
                Visibility.Collapsed;
        }
        else
        {
            CustomerRecipientDetailsPanel.Visibility =
                Visibility.Collapsed;

            CustomerRecipientMessageText.Text =
                string.IsNullOrWhiteSpace(
                    _presentation.RecipientInformationMessage)
                    ? "Thông tin người nhận chưa khả dụng."
                    : _presentation.RecipientInformationMessage;

            CustomerRecipientMessagePanel.Visibility =
                Visibility.Visible;
        }

        SuccessAmountText.Text =
            FormatMoney(
                _presentation.Amount);
    }

    private void ShowPaymentReceived()
    {
        if (_completionStateShown ||
            !IsVisible)
        {
            return;
        }

        _completionStateShown =
            true;

        SuccessOverlay.Visibility =
            Visibility.Visible;
    }

    private bool ApplyMonitorBounds(
        NativeRectangle bounds)
    {
        var width =
            bounds.Right -
            bounds.Left;

        var height =
            bounds.Bottom -
            bounds.Top;

        if (width <= 0 ||
            height <= 0)
        {
            return false;
        }

        var handle =
            new WindowInteropHelper(
                this)
            .Handle;

        if (handle ==
            IntPtr.Zero)
        {
            return false;
        }

        return SetWindowPos(
            handle,
            TopMostWindow,
            bounds.Left,
            bounds.Top,
            width,
            height,
            SetWindowPositionNoActivate |
            SetWindowPositionShowWindow |
            SetWindowPositionNoOwnerZOrder);
    }

    private static MonitorDescriptor?
        TryFindTargetMonitor(
            Window cashierWindow)
    {
        var monitors =
            EnumerateMonitors();

        if (monitors.Count < 2)
        {
            return null;
        }

        var cashierHandle =
            new WindowInteropHelper(
                cashierWindow)
            .EnsureHandle();

        var cashierMonitor =
            MonitorFromWindow(
                cashierHandle,
                MonitorDefaultToNearest);

        /*
         * Ưu tiên màn phụ không phải màn chính.
         *
         * Trường hợp thu ngân đang đặt trên màn phụ,
         * fallback sẽ chọn bất kỳ màn hình còn lại.
         */
        return monitors
                   .FirstOrDefault(
                       monitor =>
                           monitor.Handle !=
                           cashierMonitor &&
                           !monitor.IsPrimary)
               ??
               monitors
                   .FirstOrDefault(
                       monitor =>
                           monitor.Handle !=
                           cashierMonitor);
    }

    private static IReadOnlyList<MonitorDescriptor>
        EnumerateMonitors()
    {
        var monitors =
            new List<MonitorDescriptor>();

        MonitorEnumerationCallback callback =
            (
                monitorHandle,
                _,
                _,
                _) =>
            {
                var information =
                    new NativeMonitorInformation
                    {
                        Size =
                            (uint)
                            Marshal.SizeOf<
                                NativeMonitorInformation>()
                    };

                if (!GetMonitorInfo(
                        monitorHandle,
                        ref information))
                {
                    return true;
                }

                var width =
                    information.Monitor.Right -
                    information.Monitor.Left;

                var height =
                    information.Monitor.Bottom -
                    information.Monitor.Top;

                if (width <= 0 ||
                    height <= 0)
                {
                    return true;
                }

                monitors.Add(
                    new MonitorDescriptor(
                        monitorHandle,
                        IsPrimary:
                            (information.Flags &
                             MonitorInfoPrimary) !=
                            0,
                        information.Monitor));

                return true;
            };

        _ =
            EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                callback,
                IntPtr.Zero);

        GC.KeepAlive(
            callback);

        return monitors;
    }

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

        if (parts.Length >= 3)
        {
            return
                $"{parts[^3]} " +
                $"{parts[^2]} " +
                $"{parts[^1]}";
        }

        return normalized;
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

    private sealed record MonitorDescriptor(
        IntPtr Handle,
        bool IsPrimary,
        NativeRectangle Bounds);

    [StructLayout(
        LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;

        public int Top;

        public int Right;

        public int Bottom;
    }

    [StructLayout(
        LayoutKind.Sequential)]
    private struct NativeMonitorInformation
    {
        public uint Size;

        public NativeRectangle Monitor;

        public NativeRectangle WorkingArea;

        public uint Flags;
    }

    private delegate bool
        MonitorEnumerationCallback(
            IntPtr monitorHandle,
            IntPtr monitorDeviceContext,
            IntPtr monitorRectangle,
            IntPtr state);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [DefaultDllImportSearchPaths(
        DllImportSearchPath.System32)]
    [return:
        MarshalAs(
            UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clippingRectangle,
        MonitorEnumerationCallback callback,
        IntPtr state);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetMonitorInfoW",
        ExactSpelling = true,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(
        DllImportSearchPath.System32)]
    [return:
        MarshalAs(
            UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        IntPtr monitorHandle,
        ref NativeMonitorInformation monitorInformation);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [DefaultDllImportSearchPaths(
        DllImportSearchPath.System32)]
    private static extern IntPtr MonitorFromWindow(
        IntPtr windowHandle,
        uint flags);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [DefaultDllImportSearchPaths(
        DllImportSearchPath.System32)]
    [return:
        MarshalAs(
            UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfterWindow,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}