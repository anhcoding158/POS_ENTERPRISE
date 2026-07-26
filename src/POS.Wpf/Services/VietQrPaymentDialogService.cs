using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using POS.Application.DTOs.Payments;
using POS.Infrastructure.Payments;
using POS.Wpf.Views;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace POS.Wpf.Services;

public sealed record VietQrPaymentDialogRequest
{
    public VietQrPaymentDialogRequest(
        long amount,
        string paymentReference,
        string? transferContent = null)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Số tiền VietQR phải lớn hơn 0.");
        }

        if (string.IsNullOrWhiteSpace(
                paymentReference))
        {
            throw new ArgumentException(
                "Mã tham chiếu thanh toán không được để trống.",
                nameof(paymentReference));
        }

        Amount =
            amount;

        PaymentReference =
            paymentReference.Trim();

        TransferContent =
            string.IsNullOrWhiteSpace(
                transferContent)
                ? null
                : transferContent.Trim();
    }

    public long Amount
    {
        get;
    }

    public string PaymentReference
    {
        get;
    }

    public string? TransferContent
    {
        get;
    }
}

public sealed record VietQrPaymentDialogResult(
    bool Confirmed,
    string PaymentReference,
    string TransferContent);

/// <summary>
/// Dữ liệu phục vụ:
/// - màn thu ngân;
/// - màn khách hàng;
/// - phiếu in thanh toán VietQR.
///
/// PaymentReference là mã nội bộ và không được hiển thị
/// đầy đủ cho khách hàng.
/// </summary>
public sealed record VietQrPaymentPresentation(
    long Amount,
    string PaymentReference,
    string TransferContent,
    byte[] QrPngBytes);

public interface IVietQrPaymentDialogService
{
    bool IsEnabled
    {
        get;
    }

    Task<Result<VietQrPaymentDialogResult>>
        ShowAsync(
            VietQrPaymentDialogRequest request,
            CancellationToken cancellationToken = default);
}

/// <summary>
/// Điều phối màn hình thanh toán VietQR.
///
/// Production sử dụng payload QR tải từ ảnh ngân hàng.
///
/// Chế độ hiển thị được lưu riêng trên từng máy POS:
/// - màn hình khách hàng;
/// - màn hình thu ngân;
/// - mở in phiếu QR;
/// - hỏi thu ngân trong từng giao dịch.
///
/// Dù dùng chế độ nào:
/// - màn thu ngân vẫn giữ bước xác nhận nhận tiền;
/// - hiển thị hoặc in QR không tự xác nhận thanh toán;
/// - Checkout chỉ bắt đầu sau khi dialog trả Confirmed = true.
///
/// Constructor compatibility được giữ cho bộ test cũ.
/// </summary>
public sealed class VietQrPaymentDialogService :
    IVietQrPaymentDialogService
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier:
                false,

            throwOnInvalidBytes:
                true);

    private static readonly TimeSpan
        CustomerDisplayCompletionDelay =
            TimeSpan.FromMilliseconds(
                1400);

    private readonly StoredVietQrService?
        _storedService;

    private readonly IVietQrPayloadStore?
        _payloadStore;

    private readonly ILogger<
        VietQrPaymentDialogService>?
        _logger;

    private readonly IVietQrService?
        _legacyService;

    private readonly VietQrOptions?
        _legacyOptions;

    private readonly VietQrDisplayPreferenceStore
        _displayPreferenceStore;

    private readonly VietQrDisplayModeDialogService
        _displayModeDialogService;

    /// <summary>
    /// Constructor production.
    /// </summary>
    public VietQrPaymentDialogService(
        StoredVietQrService storedService,
        IVietQrPayloadStore payloadStore,
        ILogger<VietQrPaymentDialogService> logger)
    {
        _storedService =
            storedService ??
            throw new ArgumentNullException(
                nameof(storedService));

        _payloadStore =
            payloadStore ??
            throw new ArgumentNullException(
                nameof(payloadStore));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        _displayPreferenceStore =
            new VietQrDisplayPreferenceStore();

        _displayModeDialogService =
            new VietQrDisplayModeDialogService(
                _displayPreferenceStore);
    }

    /// <summary>
    /// Constructor dành cho test và luồng tương thích cũ.
    /// </summary>
    public VietQrPaymentDialogService(
        IVietQrService vietQrService,
        IOptions<VietQrOptions> options)
    {
        _legacyService =
            vietQrService ??
            throw new ArgumentNullException(
                nameof(vietQrService));

        ArgumentNullException.ThrowIfNull(
            options);

        _legacyOptions =
            options.Value ??
            throw new ArgumentException(
                "Không đọc được cấu hình VietQR.",
                nameof(options));

        _legacyOptions.Validate();

        _displayPreferenceStore =
            new VietQrDisplayPreferenceStore();

        _displayModeDialogService =
            new VietQrDisplayModeDialogService(
                _displayPreferenceStore);
    }

    public bool IsEnabled =>
        _storedService is not null
            ? _payloadStore?
                .IsConfigured ==
              true
            : _legacyOptions?
                .EnableVietQr ==
              true;

    public async Task<Result<VietQrPaymentDialogResult>>
        ShowAsync(
            VietQrPaymentDialogRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        if (!IsEnabled)
        {
            return Result.Failure<
                VietQrPaymentDialogResult>(
                    new Error(
                        ErrorCodes.Payments
                            .VietQrNotConfigured,

                        "Cửa hàng chưa lưu ảnh QR ngân hàng."));
        }

        var qrRequest =
            new VietQrRequest(
                amount:
                    request.Amount,

                orderCode:
                    request.PaymentReference,

                transferContent:
                    request.TransferContent);

        var payloadResult =
            _storedService is not null
                ? _storedService
                    .BuildPayload(
                        qrRequest)
                : _legacyService!
                    .BuildPayload(
                        qrRequest);

        if (payloadResult.IsFailure)
        {
            return Result.Failure<
                VietQrPaymentDialogResult>(
                    payloadResult.Error);
        }

        var pngResult =
            _storedService is not null
                ? _storedService
                    .GeneratePng(
                        qrRequest)
                : _legacyService!
                    .GeneratePng(
                        qrRequest);

        if (pngResult.IsFailure)
        {
            return Result.Failure<
                VietQrPaymentDialogResult>(
                    pngResult.Error);
        }

        var transferContentResult =
            TryExtractTransferContent(
                payloadResult.Value);

        if (transferContentResult.IsFailure)
        {
            return Result.Failure<
                VietQrPaymentDialogResult>(
                    transferContentResult.Error);
        }

        var presentation =
            new VietQrPaymentPresentation(
                Amount:
                    request.Amount,

                PaymentReference:
                    request.PaymentReference,

                TransferContent:
                    transferContentResult.Value,

                QrPngBytes:
                    pngResult.Value);

        var application =
            global::System.Windows
                .Application.Current;

        if (application is null)
        {
            return Failure(
                "Không tìm thấy WPF Application hiện tại.");
        }

        var dispatcher =
            application.Dispatcher;

        try
        {
            if (dispatcher.CheckAccess())
            {
                return ShowCore(
                    application,
                    presentation,
                    cancellationToken);
            }

            var operation =
                dispatcher.InvokeAsync(
                    () =>
                        ShowCore(
                            application,
                            presentation,
                            cancellationToken),

                    DispatcherPriority.Normal,
                    cancellationToken);

            return await operation
                .Task
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                exception,
                "Không thể mở dialog VietQR.");

            return Failure(
                "Không thể mở màn hình VietQR.");
        }
    }

    private Result<VietQrPaymentDialogResult>
        ShowCore(
            global::System.Windows.Application application,
            VietQrPaymentPresentation presentation,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var owner =
            FindActiveOwner(
                application);

        var displayMode =
            ResolveDisplayMode(
                owner);

        if (displayMode is null)
        {
            return CreateCancelledResult(
                presentation);
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var paymentWindow =
            new VietQrPaymentWindow(
                presentation);

        if (owner is not null &&
            !ReferenceEquals(
                owner,
                paymentWindow))
        {
            paymentWindow.Owner =
                owner;
        }

        VietQrCustomerDisplayWindow?
            customerDisplay =
                null;

        void OnPaymentWindowLoaded(
            object sender,
            RoutedEventArgs eventArgs)
        {
            paymentWindow.Loaded -=
                OnPaymentWindowLoaded;

            switch (displayMode.Value)
            {
                case VietQrDisplayMode
                    .CustomerDisplay:

                    OpenCustomerDisplay();

                    break;

                case VietQrDisplayMode
                    .PrintSlip:

                    /*
                     * Phiếu in vẫn sử dụng chính nút và luồng
                     * hiện có của VietQrPaymentWindow.
                     *
                     * Hệ thống chỉ tự mở hộp thoại in.
                     * Thu ngân vẫn chọn hoặc xác nhận máy in,
                     * tránh gửi nhầm job tới máy không mong muốn.
                     */
                    _ =
                        paymentWindow.Dispatcher
                            .BeginInvoke(
                                () =>
                                    paymentWindow
                                        .PrintSlipButton
                                        .RaiseEvent(
                                            new RoutedEventArgs(
                                                Button.ClickEvent)),

                                DispatcherPriority
                                    .Background);

                    break;

                case VietQrDisplayMode
                    .CashierDisplay:

                    /*
                     * Chỉ dùng cửa sổ trên màn thu ngân.
                     */
                    break;

                default:

                    throw new InvalidOperationException(
                        "Chế độ hiển thị VietQR không hợp lệ.");
            }
        }

        void OpenCustomerDisplay()
        {
            /*
             * Không có màn thứ hai là trạng thái hợp lệ.
             * Cửa sổ thu ngân và nút in phiếu vẫn hoạt động.
             */
            try
            {
                var candidate =
                    new VietQrCustomerDisplayWindow(
                        presentation);

                if (candidate
                    .TryShowOnSecondaryMonitor(
                        paymentWindow))
                {
                    customerDisplay =
                        candidate;
                }
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    exception,
                    "Không thể mở màn hình khách VietQR. " +
                    "Tiếp tục dùng màn hình thu ngân.");
            }
        }

        paymentWindow.Loaded +=
            OnPaymentWindowLoaded;

        using var cancellationRegistration =
            cancellationToken.Register(
                () =>
                {
                    _ =
                        paymentWindow.Dispatcher
                            .BeginInvoke(
                                () =>
                                {
                                    customerDisplay?
                                        .CloseSafely();

                                    if (paymentWindow
                                        .IsVisible)
                                    {
                                        paymentWindow
                                            .Close();
                                    }
                                });
                });

        try
        {
            var dialogResult =
                paymentWindow.ShowDialog();

            cancellationToken
                .ThrowIfCancellationRequested();

            if (customerDisplay is not null)
            {
                if (dialogResult ==
                    true)
                {
                    /*
                     * Thu ngân đã xác nhận tiền thực tế vào
                     * tài khoản cửa hàng.
                     *
                     * Không tuyên bố Order đã được lưu, bởi
                     * Checkout bắt đầu sau khi dialog trả kết quả.
                     */
                    var completedDisplay =
                        customerDisplay;

                    customerDisplay =
                        null;

                    _ =
                        completedDisplay
                            .ShowPaymentReceivedAndCloseAsync(
                                CustomerDisplayCompletionDelay);
                }
                else
                {
                    customerDisplay
                        .CloseSafely();

                    customerDisplay =
                        null;
                }
            }

            return Result.Success(
                new VietQrPaymentDialogResult(
                    Confirmed:
                        dialogResult ==
                        true,

                    PaymentReference:
                        presentation
                            .PaymentReference,

                    TransferContent:
                        presentation
                            .TransferContent));
        }
        finally
        {
            paymentWindow.Loaded -=
                OnPaymentWindowLoaded;

            customerDisplay?
                .CloseSafely();
        }
    }

    private VietQrDisplayMode?
        ResolveDisplayMode(
            Window? owner)
    {
        var configuredMode =
            _displayPreferenceStore
                .Load();

        if (configuredMode !=
            VietQrDisplayMode.AskEveryTime)
        {
            return configuredMode;
        }

        return _displayModeDialogService
            .ChooseForCurrentPayment(
                owner);
    }

    private static Result<VietQrPaymentDialogResult>
        CreateCancelledResult(
            VietQrPaymentPresentation presentation)
    {
        return Result.Success(
            new VietQrPaymentDialogResult(
                Confirmed:
                    false,

                PaymentReference:
                    presentation
                        .PaymentReference,

                TransferContent:
                    presentation
                        .TransferContent));
    }

    private static Window? FindActiveOwner(
        global::System.Windows.Application application)
    {
        var activeWindow =
            application.Windows
                .OfType<Window>()
                .FirstOrDefault(
                    window =>
                        window.IsActive &&
                        window.IsVisible &&
                        window is not
                            VietQrPaymentWindow &&
                        window is not
                            VietQrCustomerDisplayWindow);

        if (activeWindow is not null)
        {
            return activeWindow;
        }

        return application.MainWindow is
        {
            IsVisible:
                true
        } mainWindow
            ? mainWindow
            : null;
    }

    private static Result<string>
        TryExtractTransferContent(
            string payload)
    {
        if (string.IsNullOrWhiteSpace(
                payload))
        {
            return Failure<string>(
                "Payload VietQR không hợp lệ.");
        }

        var topLevelResult =
            TryReadTlvCollection(
                StrictUtf8.GetBytes(
                    payload));

        if (topLevelResult.IsFailure)
        {
            return Result.Failure<string>(
                topLevelResult.Error);
        }

        var additionalData =
            topLevelResult.Value
                .SingleOrDefault(
                    field =>
                        string.Equals(
                            field.Tag,
                            "62",
                            StringComparison.Ordinal));

        if (additionalData is null)
        {
            return Failure<string>(
                "Payload VietQR không có nội dung chuyển khoản.");
        }

        var nestedResult =
            TryReadTlvCollection(
                additionalData.RawValue);

        if (nestedResult.IsFailure)
        {
            return Result.Failure<string>(
                nestedResult.Error);
        }

        var transferContent =
            nestedResult.Value
                .SingleOrDefault(
                    field =>
                        string.Equals(
                            field.Tag,
                            "08",
                            StringComparison.Ordinal));

        if (transferContent is null ||
            string.IsNullOrWhiteSpace(
                transferContent.Value))
        {
            return Failure<string>(
                "Payload VietQR không có nội dung chuyển khoản.");
        }

        return Result.Success(
            transferContent.Value);
    }

    private static Result<IReadOnlyList<TlvField>>
        TryReadTlvCollection(
            byte[] bytes)
    {
        var fields =
            new List<TlvField>();

        var index =
            0;

        while (index <
               bytes.Length)
        {
            if (bytes.Length -
                index <
                4)
            {
                return Failure<
                    IReadOnlyList<TlvField>>(
                        "Payload VietQR có TLV không hoàn chỉnh.");
            }

            if (!IsAsciiDigit(
                    bytes[index]) ||
                !IsAsciiDigit(
                    bytes[index + 1]) ||
                !IsAsciiDigit(
                    bytes[index + 2]) ||
                !IsAsciiDigit(
                    bytes[index + 3]))
            {
                return Failure<
                    IReadOnlyList<TlvField>>(
                        "Payload VietQR có TLV không hợp lệ.");
            }

            var tag =
                Encoding.ASCII
                    .GetString(
                        bytes,
                        index,
                        2);

            var length =
                ((bytes[index + 2] -
                  (byte)'0') *
                 10) +
                (bytes[index + 3] -
                 (byte)'0');

            var valueStart =
                index +
                4;

            if (valueStart +
                length >
                bytes.Length)
            {
                return Failure<
                    IReadOnlyList<TlvField>>(
                        $"Payload VietQR thiếu dữ liệu tag {tag}.");
            }

            var rawValue =
                bytes
                    .AsSpan(
                        valueStart,
                        length)
                    .ToArray();

            fields.Add(
                new TlvField(
                    Tag:
                        tag,

                    Value:
                        StrictUtf8
                            .GetString(
                                rawValue),

                    RawValue:
                        rawValue));

            index =
                valueStart +
                length;
        }

        return Result.Success<
            IReadOnlyList<TlvField>>(
                fields);
    }

    private static bool IsAsciiDigit(
        byte value)
    {
        return value is
            >= (byte)'0' and
            <= (byte)'9';
    }

    private static Result<VietQrPaymentDialogResult>
        Failure(
            string message)
    {
        return Result.Failure<
            VietQrPaymentDialogResult>(
                new Error(
                    ErrorCodes.Payments
                        .VietQrGenerationFailed,

                    message));
    }

    private static Result<TValue>
        Failure<TValue>(
            string message)
    {
        return Result.Failure<TValue>(
            new Error(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                message));
    }

    private sealed record TlvField(
        string Tag,
        string Value,
        byte[] RawValue);
}