using System.ComponentModel;
using System.Globalization;
using System.Media;
using POS.Wpf.Commands;
using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

/// <summary>
/// Màn hình quầy bán hàng.
///
/// Phím tắt:
/// - F2: chuyển tới ô quét barcode/mã sản phẩm;
/// - Ctrl+F: chuyển tới ô tìm sản phẩm;
/// - F4: đặt tiền khách đưa bằng đúng tổng đơn;
/// - F6: chuyển tới ô tiền khách đưa;
/// - F8: thanh toán;
/// - Enter:
///     + tại ô tìm kiếm: thực hiện tìm kiếm;
///     + tại ô tiền khách đưa: thực hiện thanh toán;
/// - Esc: đóng quầy qua close guard hiện hữu.
///
/// Nguyên tắc an toàn:
/// - không cho đóng cửa sổ khi checkout đang chạy;
/// - không cho đóng cửa sổ khi đã xác nhận nhận tiền VietQR
///   nhưng Order chưa được lưu;
/// - F4 và F6 chỉ hoạt động với thanh toán tiền mặt;
/// - F8 dùng phương thức thanh toán đang chọn;
/// - không cho gửi lệnh bàn phím lần hai trong transaction;
/// - chỉ cho nhập và dán chữ số vào ô tiền;
/// - định dạng tiền theo vi-VN ngay trong lúc nhập;
/// - giải phóng timer và tài nguyên ViewModel khi cửa sổ đóng.
/// </summary>
public partial class SalesWindow :
    global::System.Windows.Window
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private readonly SalesViewModel
        _viewModel;

    private bool _closeConfirmed;
    private bool _isFormattingCashText;
    private bool _isWindowClosed;

    private string _lastValidCashText =
        string.Empty;

    public SalesWindow(
        SalesViewModel viewModel)
    {
        _viewModel =
            viewModel ??
            throw new ArgumentNullException(
                nameof(viewModel));

        InitializeComponent();

        DataContext =
            _viewModel;

        /*
         * Pasting event xử lý cả:
         * - Ctrl + V;
         * - Shift + Insert;
         * - menu chuột phải → Paste.
         */
        global::System.Windows.DataObject
            .AddPastingHandler(
                CashReceivedTextBox,
                OnCashPaste);

        Loaded +=
            OnWindowLoaded;

        PreviewKeyDown +=
            OnPreviewKeyDown;

        Closing +=
            OnWindowClosing;

        Closed +=
            OnWindowClosed;

        _viewModel.ScanFocusRequested +=
            OnScanFocusRequested;
    }

    private void OnScanFocusRequested(
        object? sender,
        EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(
            () => FocusAndSelectAll(ProductScanBox),
            global::System.Windows.Threading.DispatcherPriority.Input);
    }

    private async void OnWindowLoaded(
        object sender,
        global::System.Windows
            .RoutedEventArgs e)
    {
        Loaded -=
            OnWindowLoaded;

        try
        {
            await _viewModel
                .InitializeAsync();

            /*
             * Chờ WPF hoàn thành layout rồi mới focus.
             * Cách này ổn định hơn khi cửa sổ mở Maximized.
             */
            await Dispatcher.InvokeAsync(
                () =>
                {
                    FocusAndSelectAll(
                        ProductScanBox);
                },
                global::System.Windows.Threading
                    .DispatcherPriority.Input);
        }
        catch (Exception exception)
        {
            _closeConfirmed =
                true;

            global::System.Windows.MessageBox.Show(
                this,
                "Không thể khởi tạo quầy bán hàng.\n\n" +
                exception
                    .GetBaseException()
                    .Message,
                "POS Enterprise",
                global::System.Windows
                    .MessageBoxButton.OK,
                global::System.Windows
                    .MessageBoxImage.Error);

            Close();
        }
    }

    private async void OnPreviewKeyDown(
        object sender,
        global::System.Windows.Input
            .KeyEventArgs e)
    {
        /*
         * Khi Checkout đang chạy, khóa toàn bộ thao tác
         * bàn phím trên cửa sổ.
         *
         * OnWindowClosing vẫn là lớp bảo vệ thứ hai
         * cho Alt + F4 hoặc nút X.
         */
        if (_viewModel.IsCheckingOut)
        {
            e.Handled =
                true;

            return;
        }

        if (_viewModel.HasCheckoutRecovery ||
            _viewModel.IsPaymentIntentRecoveryOpen ||
            _viewModel.IsRecoveryBusy)
        {
            e.Handled = true;
            SystemSounds.Beep.Play();
            return;
        }

        /*
         * Sau khi thu ngân đã xác nhận nhận tiền VietQR:
         * - F8 được phép thử lưu lại bằng authorization cũ;
         * - mọi phím tắt sửa đơn hoặc rời cửa sổ đều bị khóa;
         * - không mở QR mới và không làm mất dữ liệu đối soát.
         */
        if (_viewModel
            .HasPendingVietQrAuthorization)
        {
            if (e.Key ==
                global::System.Windows
                    .Input.Key.F8)
            {
                ExecuteCommand(
                    _viewModel
                        .CheckoutCommand);
            }
            else if (e.Key ==
                     global::System.Windows
                         .Input.Key.Escape)
            {
                Close();
            }
            else
            {
                SystemSounds.Beep
                    .Play();
            }

            e.Handled =
                true;

            return;
        }

        switch (e.Key)
        {
            case global::System.Windows
                .Input.Key.F2:

                FocusAndSelectAll(
                    ProductScanBox);

                e.Handled =
                    true;

                break;

            case global::System.Windows
                .Input.Key.F4:

                if (!_viewModel
                    .IsCashPaymentSelected)
                {
                    ShowCashShortcutUnavailable();

                    e.Handled =
                        true;

                    break;
                }

                ExecuteCommand(
                    _viewModel
                        .ExactCashCommand);

                FocusAndSelectAll(
                    CashReceivedTextBox);

                e.Handled =
                    true;

                break;

            case global::System.Windows.Input.Key.F7:
                ShowDiscountDialog();
                e.Handled = true;
                break;

            case global::System.Windows
                .Input.Key.F6:

                if (!_viewModel
                    .IsCashPaymentSelected)
                {
                    ShowCashShortcutUnavailable();

                    e.Handled =
                        true;

                    break;
                }

                FocusAndSelectAll(
                    CashReceivedTextBox);

                e.Handled =
                    true;

                break;

            case global::System.Windows
                .Input.Key.F8:

                ExecuteCommand(
                    _viewModel
                        .CheckoutCommand);

                e.Handled =
                    true;

                break;

            case global::System.Windows
                .Input.Key.Enter:

                await HandleEnterKeyAsync(e);

                break;

            case global::System.Windows
                .Input.Key.Delete:

                if (!ProductSearchBox.IsKeyboardFocusWithin &&
                    !ProductScanBox.IsKeyboardFocusWithin &&
                    !CashReceivedTextBox.IsKeyboardFocusWithin)
                {
                    _viewModel.RemoveSelectedCartLine();
                    e.Handled = true;
                }

                break;

            case global::System.Windows
                .Input.Key.Escape:

                Close();

                e.Handled =
                    true;

                break;
        }

        if ((global::System.Windows.Input.Keyboard.Modifiers &
             global::System.Windows.Input.ModifierKeys.Control) != 0 &&
            e.Key == global::System.Windows.Input.Key.F)
        {
            FocusAndSelectAll(ProductSearchBox);
            e.Handled = true;
            return;
        }

        if ((global::System.Windows.Input.Keyboard.Modifiers &
             global::System.Windows.Input.ModifierKeys.Control) != 0 &&
            e.Key == global::System.Windows.Input.Key.L)
        {
            ExecuteCommand(_viewModel.ClearCartCommand);
            e.Handled = true;
        }
    }

    private void OnDiscountClick(
        object sender,
        global::System.Windows.RoutedEventArgs e) =>
        ShowDiscountDialog();

    private void ShowDiscountDialog()
    {
        if (!_viewModel.CanApplySalesDiscount ||
            _viewModel.EstimatedSubtotal <= 0 ||
            _viewModel.EstimatedSubtotal > long.MaxValue)
        {
            SystemSounds.Beep.Play();
            return;
        }
        var currentDiscount = _viewModel.CurrentSalesDiscount;
        var dialog = new SalesDiscountWindow(
            checked((long)_viewModel.EstimatedSubtotal),
            currentDiscount.Type,
            currentDiscount.Value,
            currentDiscount.Reason)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
            return;
        if (!_viewModel.TryApplySalesDiscount(
                dialog.DiscountType, dialog.DiscountValue,
                dialog.DiscountReason, out var error))
            global::System.Windows.MessageBox.Show(
                this, error, "Giảm giá",
                global::System.Windows.MessageBoxButton.OK,
                global::System.Windows.MessageBoxImage.Warning);
    }

    private void OnClearDiscountClick(
        object sender,
        global::System.Windows.RoutedEventArgs e) =>
        _viewModel.ClearSalesDiscount();

    private async Task HandleEnterKeyAsync(
        global::System.Windows.Input
            .KeyEventArgs e)
    {
        if (ProductSearchBox
            .IsKeyboardFocusWithin)
        {
            ExecuteCommand(_viewModel.SearchCommand);
            e.Handled = true;
            return;
        }

        if (ProductScanBox
            .IsKeyboardFocusWithin)
        {
            var input = ProductScanBox.Text;
            await _viewModel.ProcessScanOrSearchAsync(input);
            FocusAndSelectAll(ProductScanBox);

            e.Handled =
                true;

            return;
        }

        // Enter tại ô tiền không được phép bypass guard/xác nhận checkout.
    }

    /// <summary>
    /// Chỉ nhận chữ số ASCII từ bàn phím.
    ///
    /// Dấu phân cách hàng nghìn không cần nhập thủ công;
    /// hệ thống tự thêm theo định dạng Việt Nam.
    /// </summary>
    private void OnCashPreviewTextInput(
        object sender,
        global::System.Windows.Input
            .TextCompositionEventArgs e)
    {
        if (!CanAcceptCashInput())
        {
            e.Handled =
                true;

            SystemSounds.Beep
                .Play();

            return;
        }

        foreach (var character in
                 e.Text)
        {
            if (IsAsciiDigit(
                    character))
            {
                continue;
            }

            e.Handled =
                true;

            SystemSounds.Beep
                .Play();

            return;
        }
    }

    /// <summary>
    /// Xử lý dữ liệu dán vào ô tiền.
    ///
    /// Ví dụ:
    /// - "500000"        → 500.000;
    /// - "500.000 đ"     → 500.000;
    /// - "Khách đưa 1tr" → chỉ lấy các chữ số có trong chuỗi;
    /// - số vượt giới hạn long → từ chối.
    ///
    /// Việc tự xử lý paste giúp tránh trạng thái TextBox
    /// chứa dữ liệu lỗi trong một khoảng thời gian ngắn.
    /// </summary>
    private void OnCashPaste(
        object sender,
        global::System.Windows
            .DataObjectPastingEventArgs e)
    {
        if (!CanAcceptCashInput() ||
            sender is not
                global::System.Windows.Controls
                    .TextBox textBox)
        {
            e.CancelCommand();

            return;
        }

        if (!e.SourceDataObject
            .GetDataPresent(
                global::System.Windows
                    .DataFormats.UnicodeText,
                autoConvert:
                    true))
        {
            e.CancelCommand();

            SystemSounds.Beep
                .Play();

            return;
        }

        var pastedText =
            e.SourceDataObject
                .GetData(
                    global::System.Windows
                        .DataFormats.UnicodeText,
                    autoConvert:
                        true)
                as string;

        var pastedDigits =
            ExtractDigits(
                pastedText ??
                string.Empty);

        if (pastedDigits.Length == 0)
        {
            e.CancelCommand();

            SystemSounds.Beep
                .Play();

            return;
        }

        var currentText =
            textBox.Text ??
            string.Empty;

        var selectionStart =
            Math.Clamp(
                textBox.SelectionStart,
                0,
                currentText.Length);

        var selectionLength =
            Math.Clamp(
                textBox.SelectionLength,
                0,
                currentText.Length -
                selectionStart);

        var candidateText =
            currentText
                .Remove(
                    selectionStart,
                    selectionLength)
                .Insert(
                    selectionStart,
                    pastedDigits);

        var candidateDigits =
            ExtractDigits(
                candidateText);

        if (!long.TryParse(
                candidateDigits,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            e.CancelCommand();

            SystemSounds.Beep
                .Play();

            return;
        }

        var insertionEnd =
            selectionStart +
            pastedDigits.Length;

        var digitsToRight =
            CountDigits(
                candidateText,
                insertionEnd,
                candidateText.Length);

        var formattedText =
            FormatCashAmount(
                amount);

        var caretIndex =
            FindCaretIndex(
                formattedText,
                digitsToRight);

        /*
         * Hủy paste mặc định rồi tự áp dụng dữ liệu
         * đã được kiểm tra và chuẩn hóa.
         */
        e.CancelCommand();

        SetFormattedCashText(
            textBox,
            formattedText,
            caretIndex);
    }

    /// <summary>
    /// Định dạng tiền ngay khi người dùng nhập:
    ///
    /// 5       → 5
    /// 5000    → 5.000
    /// 500000  → 500.000
    ///
    /// Vị trí con trỏ được giữ theo số chữ số nằm
    /// bên phải caret, nên vẫn sửa được ở giữa chuỗi.
    /// </summary>
    private void OnCashTextChanged(
        object sender,
        global::System.Windows.Controls
            .TextChangedEventArgs e)
    {
        if (_isFormattingCashText ||
            sender is not
                global::System.Windows.Controls
                    .TextBox textBox)
        {
            return;
        }

        var originalText =
            textBox.Text ??
            string.Empty;

        var originalCaretIndex =
            Math.Clamp(
                textBox.CaretIndex,
                0,
                originalText.Length);

        var digitsToRight =
            CountDigits(
                originalText,
                originalCaretIndex,
                originalText.Length);

        var digits =
            ExtractDigits(
                originalText);

        if (digits.Length == 0)
        {
            _lastValidCashText =
                string.Empty;

            if (originalText.Length > 0)
            {
                SetFormattedCashText(
                    textBox,
                    string.Empty,
                    caretIndex:
                        0);
            }

            return;
        }

        if (!long.TryParse(
                digits,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            /*
             * Không để chuỗi quá lớn nằm lại trong TextBox.
             * Khôi phục giá trị hợp lệ gần nhất.
             */
            SystemSounds.Beep
                .Play();

            SetFormattedCashText(
                textBox,
                _lastValidCashText,
                _lastValidCashText.Length);

            return;
        }

        var formattedText =
            FormatCashAmount(
                amount);

        _lastValidCashText =
            formattedText;

        if (string.Equals(
                originalText,
                formattedText,
                StringComparison.Ordinal))
        {
            return;
        }

        var formattedCaretIndex =
            FindCaretIndex(
                formattedText,
                digitsToRight);

        SetFormattedCashText(
            textBox,
            formattedText,
            formattedCaretIndex);
    }

    private void SetFormattedCashText(
        global::System.Windows.Controls
            .TextBox textBox,
        string formattedText,
        int caretIndex)
    {
        try
        {
            _isFormattingCashText =
                true;

            textBox.Text =
                formattedText;

            textBox.CaretIndex =
                Math.Clamp(
                    caretIndex,
                    0,
                    formattedText.Length);

            _lastValidCashText =
                formattedText;
        }
        finally
        {
            _isFormattingCashText =
                false;
        }
    }

    private static string FormatCashAmount(
        long amount)
    {
        return amount.ToString(
            "N0",
            VietnameseCulture);
    }

    private static string ExtractDigits(
        string value)
    {
        if (string.IsNullOrEmpty(
                value))
        {
            return string.Empty;
        }

        var characters =
            new char[
                value.Length];

        var writeIndex =
            0;

        foreach (var character in
                 value)
        {
            if (!IsAsciiDigit(
                    character))
            {
                continue;
            }

            characters[writeIndex] =
                character;

            writeIndex++;
        }

        return new string(
            characters,
            startIndex:
                0,
            length:
                writeIndex);
    }

    private static bool IsAsciiDigit(
        char character)
    {
        return character is
            >= '0' and <= '9';
    }

    private static int CountDigits(
        string value,
        int startIndex,
        int endIndex)
    {
        var count =
            0;

        var normalizedStart =
            Math.Clamp(
                startIndex,
                0,
                value.Length);

        var normalizedEnd =
            Math.Clamp(
                endIndex,
                normalizedStart,
                value.Length);

        for (var index =
                 normalizedStart;
             index <
             normalizedEnd;
             index++)
        {
            if (IsAsciiDigit(
                    value[index]))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Khôi phục vị trí caret theo số chữ số nằm bên phải.
    ///
    /// Ví dụ:
    /// 12345 với caret sau số 2
    /// → định dạng thành 12.345
    /// → caret vẫn nằm trước số 3.
    /// </summary>
    private static int FindCaretIndex(
        string formattedText,
        int digitsToRight)
    {
        if (digitsToRight <= 0)
        {
            return formattedText.Length;
        }

        var foundDigits =
            0;

        for (var index =
                 formattedText.Length - 1;
             index >= 0;
             index--)
        {
            if (!IsAsciiDigit(
                    formattedText[index]))
            {
                continue;
            }

            foundDigits++;

            if (foundDigits ==
                digitsToRight)
            {
                return index;
            }
        }

        return 0;
    }

    private void OnWindowClosing(
        object? sender,
        CancelEventArgs e)
    {
        /*
         * Không bao giờ đóng Window khi Checkout đang chạy.
         *
         * Transaction phải hoàn tất trước khi:
         * - ViewModel bị dispose;
         * - DI scope bị đóng;
         * - DbContext và service được giải phóng.
         */
        if (_viewModel.IsCheckingOut || _viewModel.IsProcessingRecovery)
        {
            e.Cancel =
                true;

            global::System.Windows
                .MessageBox.Show(
                    this,
                    "Giao dịch đang được xác nhận và lưu dữ liệu.\n\n" +
                    "Vui lòng chờ thanh toán hoàn tất trước khi đóng quầy.",
                    "Đang xử lý thanh toán",
                    global::System.Windows
                        .MessageBoxButton.OK,
                    global::System.Windows
                        .MessageBoxImage.Information);

            return;
        }

        var hasConfirmedRecovery =
            _viewModel.PendingPaymentIntents.Any(
                value => value.Status == POS.Domain.Enums.PaymentIntentStatus.Confirmed);

        if (_viewModel.HasPendingVietQrAuthorization || hasConfirmedRecovery)
        {
            var closeResult =
                global::System.Windows.MessageBox.Show(
                    this,
                    "Giao dịch VietQR đã được xác nhận nhận tiền nhưng đơn chưa lưu.\n\n" +
                    "Bạn có thể ở lại để thử lưu lại đơn, hoặc đóng cửa sổ và tiếp tục xử lý khi mở lại phần mềm.\n\n" +
                    "Không yêu cầu khách chuyển thêm.",
                    "Giao dịch VietQR đã nhận tiền",
                    global::System.Windows.MessageBoxButton.YesNo,
                    global::System.Windows.MessageBoxImage.Warning,
                    global::System.Windows.MessageBoxResult.No);

            if (closeResult !=
                global::System.Windows.MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _closeConfirmed = true;
        }

        if (_closeConfirmed ||
            _viewModel.CartLines.Count == 0)
        {
            return;
        }

        var result =
            global::System.Windows
                .MessageBox.Show(
                    this,
                    "Đơn hàng hiện tại chưa được thanh toán.\n\n" +
                    "Đóng màn hình sẽ làm mất toàn bộ món " +
                    "và thông tin thanh toán trong giỏ.",
                    "Đóng quầy bán hàng",
                    global::System.Windows
                        .MessageBoxButton.YesNo,
                    global::System.Windows
                        .MessageBoxImage.Warning,
                    global::System.Windows
                        .MessageBoxResult.No);

        if (result !=
            global::System.Windows
                .MessageBoxResult.Yes)
        {
            e.Cancel =
                true;

            return;
        }

        _closeConfirmed =
            true;
    }

    private void OnRecoveryCloseClick(
        object sender,
        global::System.Windows.RoutedEventArgs e) => Close();

    private void OnContinueSalesClick(
        object sender,
        global::System.Windows.RoutedEventArgs e) =>
        _viewModel.ContinueSalesAfterManualReviewWarning();

    private async void OnManualResolutionClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        if (_viewModel.SelectedPaymentIntentRecovery is not { CanResolveManually: true } pending)
            return;
        var dialog = new PaymentIntentManualResolutionWindow(pending.Id)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.Request is not null)
            await _viewModel.ResolveSelectedPaymentIntentManuallyAsync(dialog.Request);
    }

    private async void OnManualResolutionHistoryClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        var history = await _viewModel.LoadPaymentIntentManualResolutionHistoryAsync();
        new PaymentIntentManualResolutionHistoryWindow(history)
        {
            Owner = this
        }.ShowDialog();
    }

    private void OnCloseRecoveryForLaterClick(
        object sender,
        global::System.Windows.RoutedEventArgs e) =>
        _viewModel.ClosePaymentIntentRecoveryForLater();

    private void OnOpenPaymentIntentRecoveryClick(
        object sender,
        global::System.Windows.RoutedEventArgs e) =>
        _viewModel.OpenPaymentIntentRecovery();

    private void OnRecoveryDetailsClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        var item = _viewModel.SelectedPaymentIntentRecovery;
        if (item?.CanViewDetails != true)
            return;

        global::System.Windows.MessageBox.Show(
            this,
            $"Mã tham chiếu: {item.DisplayCode}\n" +
            $"Số tiền: {item.AmountText}\n" +
            $"Thời gian tạo: {item.CreatedAtText}\n" +
            $"Thời gian xác nhận: {item.ConfirmedAtText}\n" +
            $"Xác nhận bởi: {item.ConfirmedByText}\n" +
            (item.HasLineSummary ? $"Sản phẩm: {item.LineSummary}\n" : string.Empty) +
            $"\nLý do phục hồi:\n{item.Warning}\n\n" +
            "Metadata an toàn: snapshot legacy/không đọc được; không hiển thị raw JSON.",
            "Chi tiết giao dịch VietQR",
            global::System.Windows.MessageBoxButton.OK,
            global::System.Windows.MessageBoxImage.Warning);
    }

    private void OnWindowClosed(
        object? sender,
        EventArgs e)
    {
        if (_isWindowClosed)
        {
            return;
        }

        _isWindowClosed =
            true;

        Loaded -=
            OnWindowLoaded;

        PreviewKeyDown -=
            OnPreviewKeyDown;

        Closing -=
            OnWindowClosing;

        Closed -=
            OnWindowClosed;

        _viewModel.ScanFocusRequested -=
            OnScanFocusRequested;

        global::System.Windows.DataObject
            .RemovePastingHandler(
                CashReceivedTextBox,
                OnCashPaste);

        /*
         * SalesViewModel.Dispose():
         * - hủy CancellationTokenSource của banner hóa đơn;
         * - ngăn continuation của timer cập nhật UI sau khi đóng;
         * - giải phóng tài nguyên do ViewModel sở hữu.
         *
         * Dispose được thiết kế idempotent nên DI scope gọi lại
         * cũng không gây lỗi.
         */
        _viewModel.Dispose();

        /*
         * Cắt liên kết binding để Visual Tree không giữ
         * ViewModel lâu hơn vòng đời cửa sổ.
         */
        DataContext =
            null;
    }

    private bool CanAcceptCashInput()
    {
        return
            !_viewModel.IsCheckingOut &&
            !_viewModel
                .HasPendingVietQrAuthorization &&
            _viewModel
                .IsCashPaymentSelected &&
            _viewModel
                .IsCashInputEnabled;
    }

    private void ShowCashShortcutUnavailable()
    {
        SystemSounds.Beep
            .Play();

        global::System.Windows
            .MessageBox.Show(
                this,
                "F4 và F6 chỉ dùng cho thanh toán tiền mặt.\n\n" +
                "Với VietQR, hãy dùng F8 để mở hoặc thử lưu " +
                "giao dịch theo trạng thái hiện tại.",
                "Phím tắt tiền mặt",
                global::System.Windows
                    .MessageBoxButton.OK,
                global::System.Windows
                    .MessageBoxImage.Information);
    }

    private void ShowPendingVietQrCloseBlockedMessage()
    {
        SystemSounds.Beep
            .Play();

        var paymentReference =
            string.IsNullOrWhiteSpace(
                _viewModel
                    .PendingVietQrReferenceText)
                ? "Không xác định"
                : _viewModel
                    .PendingVietQrReferenceText;

        var amount =
            string.IsNullOrWhiteSpace(
                _viewModel
                    .PendingVietQrAmountText)
                ? "Không xác định"
                : _viewModel
                    .PendingVietQrAmountText;

        global::System.Windows
            .MessageBox.Show(
                this,
                "Không thể đóng quầy vì thu ngân đã xác nhận " +
                "cửa hàng nhận tiền VietQR nhưng đơn chưa được lưu.\n\n" +
                $"Mã tham chiếu: {paymentReference}\n" +
                $"Số tiền đã nhận: {amount}\n\n" +
                "Không tạo QR mới và không yêu cầu khách chuyển thêm. " +
                "Hãy xử lý nguyên nhân rồi bấm F8 để thử lưu lại, " +
                "hoặc báo quản lý.",
                "Đơn VietQR chưa được lưu",
                global::System.Windows
                    .MessageBoxButton.OK,
                global::System.Windows
                    .MessageBoxImage.Warning);
    }

    private static void FocusAndSelectAll(
        global::System.Windows.Controls
            .TextBox textBox)
    {
        ArgumentNullException.ThrowIfNull(
            textBox);

        if (!textBox.IsVisible ||
            !textBox.IsEnabled)
        {
            return;
        }

        textBox.Focus();
        textBox.SelectAll();
    }

    private static void ExecuteCommand(
        AsyncRelayCommand command)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        if (!command.CanExecute(
                parameter:
                    null))
        {
            return;
        }

        command.Execute(
            parameter:
                null);
    }
}
