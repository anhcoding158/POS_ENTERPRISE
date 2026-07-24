using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using POS.Application.DTOs.Payments;
using POS.Infrastructure.Payments;
using POS.Wpf.Views;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;

namespace POS.Wpf.Services;

/// <summary>
/// Yêu cầu mở màn hình thanh toán VietQR.
///
/// PaymentReference là mã tham chiếu tạm thời được dùng
/// trong nội dung chuyển khoản.
///
/// Ở checkpoint hiện tại mã này chưa phải OrderCode,
/// vì Order chưa được tạo trước khi thu ngân xác nhận.
/// </summary>
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

/// <summary>
/// Kết quả người dùng thao tác trên dialog VietQR.
///
/// Confirmed chỉ thể hiện:
/// thu ngân đã chủ động xác nhận theo quy trình nội bộ.
///
/// Confirmed không phải xác nhận tự động từ ngân hàng.
/// </summary>
public sealed record VietQrPaymentDialogResult(
    bool Confirmed,
    string PaymentReference,
    string TransferContent);

/// <summary>
/// Dữ liệu bất biến dùng riêng cho cửa sổ VietQR.
/// </summary>
public sealed record VietQrPaymentPresentation(
    long Amount,
    string PaymentReference,
    string TransferContent,
    string BankBin,
    string AccountNumber,
    string AccountName,
    byte[] QrPngBytes);

/// <summary>
/// Presentation abstraction cho màn thanh toán VietQR.
/// </summary>
public interface IVietQrPaymentDialogService
{
    Task<Result<VietQrPaymentDialogResult>> ShowAsync(
        VietQrPaymentDialogRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Mở dialog VietQR trên WPF Dispatcher.
///
/// Service không giữ DbContext và không thay đổi Order.
/// </summary>
public sealed class VietQrPaymentDialogService :
    IVietQrPaymentDialogService
{
    private readonly IVietQrService
        _vietQrService;

    private readonly VietQrOptions
        _options;

    public VietQrPaymentDialogService(
        IVietQrService vietQrService,
        IOptions<VietQrOptions> options)
    {
        _vietQrService =
            vietQrService ??
            throw new ArgumentNullException(
                nameof(vietQrService));

        ArgumentNullException.ThrowIfNull(
            options);

        _options =
            options.Value ??
            throw new ArgumentException(
                "Không đọc được cấu hình VietQR.",
                nameof(options));

        _options.Validate();
    }

    public async Task<Result<VietQrPaymentDialogResult>>
        ShowAsync(
            VietQrPaymentDialogRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        var qrRequest =
            new VietQrRequest(
                amount:
                    request.Amount,

                orderCode:
                    request.PaymentReference,

                transferContent:
                    request.TransferContent);

        var payloadResult =
            _vietQrService.BuildPayload(
                qrRequest);

        if (payloadResult.IsFailure)
        {
            return Result.Failure<
                VietQrPaymentDialogResult>(
                    payloadResult.Error);
        }

        var pngResult =
            _vietQrService.GeneratePng(
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

                BankBin:
                    _options
                        .GetNormalizedBankBin(),

                AccountNumber:
                    _options
                        .GetNormalizedAccountNumber(),

                AccountName:
                    _options
                        .GetNormalizedAccountName(),

                QrPngBytes:
                    pngResult.Value);

        var application =
            global::System.Windows.Application
                .Current;

        if (application is null)
        {
            return Failure(
                "Không tìm thấy WPF Application hiện tại.");
        }

        var dispatcher =
            application.Dispatcher;

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
            .ConfigureAwait(
                false);
    }

    private static Result<VietQrPaymentDialogResult>
        ShowCore(
            global::System.Windows.Application application,
            VietQrPaymentPresentation presentation,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var window =
            new VietQrPaymentWindow(
                presentation);

        var owner =
            FindActiveOwner(
                application);

        if (owner is not null &&
            !ReferenceEquals(
                owner,
                window))
        {
            window.Owner =
                owner;
        }

        using var cancellationRegistration =
            cancellationToken.Register(
                () =>
                {
                    _ =
                        window.Dispatcher.BeginInvoke(
                            () =>
                            {
                                if (window.IsVisible)
                                {
                                    window.Close();
                                }
                            });
                });

        var dialogResult =
            window.ShowDialog();

        cancellationToken
            .ThrowIfCancellationRequested();

        var confirmed =
            dialogResult ==
            true;

        return Result.Success(
            new VietQrPaymentDialogResult(
                Confirmed:
                    confirmed,

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
                            VietQrPaymentWindow);

        if (activeWindow is not null)
        {
            return activeWindow;
        }

        var mainWindow =
            application.MainWindow;

        return mainWindow is not null &&
               mainWindow.IsVisible
            ? mainWindow
            : null;
    }

    /// <summary>
    /// Đọc Additional Data tag 62 và sub-tag 08
    /// từ payload vừa được chính VietQrService tạo ra.
    ///
    /// Việc đọc lại giúp giao diện hiển thị đúng chính xác
    /// nội dung đã mã hóa trong QR, không lặp lại thuật toán
    /// chuẩn hóa ở Presentation.
    /// </summary>
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
                payload);

        if (topLevelResult.IsFailure)
        {
            return Result.Failure<string>(
                topLevelResult.Error);
        }

        var additionalData =
            topLevelResult.Value
                .FirstOrDefault(
                    field =>
                        string.Equals(
                            field.Tag,
                            "62",
                            StringComparison.Ordinal));

        if (additionalData is null)
        {
            return Failure<string>(
                "Payload VietQR không có thông tin chuyển khoản.");
        }

        var additionalDataResult =
            TryReadTlvCollection(
                additionalData.Value);

        if (additionalDataResult.IsFailure)
        {
            return Result.Failure<string>(
                additionalDataResult.Error);
        }

        var transferContent =
            additionalDataResult.Value
                .FirstOrDefault(
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
            string value)
    {
        var fields =
            new List<TlvField>();

        var index =
            0;

        while (index <
               value.Length)
        {
            /*
             * CRC ở top level có dạng:
             * 63 04 XXXX
             *
             * Parser vẫn đọc bình thường như một TLV.
             */
            if (value.Length - index <
                4)
            {
                return Failure<
                    IReadOnlyList<TlvField>>(
                        "Payload VietQR có phần TLV không hoàn chỉnh.");
            }

            var tag =
                value.Substring(
                    index,
                    2);

            var lengthText =
                value.Substring(
                    index + 2,
                    2);

            if (!int.TryParse(
                    lengthText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var length))
            {
                return Failure<
                    IReadOnlyList<TlvField>>(
                        $"Độ dài TLV của tag {tag} không hợp lệ.");
            }

            var valueStart =
                index + 4;

            if (valueStart + length >
                value.Length)
            {
                return Failure<
                    IReadOnlyList<TlvField>>(
                        $"Giá trị TLV của tag {tag} bị thiếu.");
            }

            var fieldValue =
                value.Substring(
                    valueStart,
                    length);

            fields.Add(
                new TlvField(
                    tag,
                    fieldValue));

            index =
                valueStart +
                length;
        }

        return Result.Success<
            IReadOnlyList<TlvField>>(
                fields);
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
        string Value);
}