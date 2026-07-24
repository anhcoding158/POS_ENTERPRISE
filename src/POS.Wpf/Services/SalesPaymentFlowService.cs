using POS.Application.Abstractions.DateTime;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;
using System.Globalization;
using System.Threading;

namespace POS.Wpf.Services;

/// <summary>
/// Yêu cầu xác thực một phương thức thanh toán
/// trước khi SalesViewModel gọi CheckoutService.
/// </summary>
public sealed record SalesPaymentAuthorizationRequest
{
    public SalesPaymentAuthorizationRequest(
        PaymentMethod paymentMethod,
        long totalAmount,
        long cashReceived,
        SalesPaymentAuthorization?
            existingAuthorization = null)
    {
        PaymentMethod =
            paymentMethod;

        TotalAmount =
            totalAmount;

        CashReceived =
            cashReceived;

        ExistingAuthorization =
            existingAuthorization;
    }

    public PaymentMethod PaymentMethod
    {
        get;
    }

    /// <summary>
    /// Tổng tiền đang được Presentation dùng để:
    ///
    /// - kiểm tra tiền mặt;
    /// - tạo mã VietQR;
    /// - khóa số tiền khách đã xác nhận;
    /// - kiểm tra authorization cũ khi thử lưu lại.
    ///
    /// CheckoutService vẫn phải tính lại tổng thật
    /// từ Product trong database.
    /// </summary>
    public long TotalAmount
    {
        get;
    }

    public long CashReceived
    {
        get;
    }

    /// <summary>
    /// Xác nhận VietQR đã có từ một lần trước.
    ///
    /// Khi Checkout bị lỗi sau lúc khách đã chuyển tiền,
    /// SalesViewModel truyền lại authorization này để thử
    /// lưu đơn mà không mở hoặc tạo QR lần hai.
    /// </summary>
    public SalesPaymentAuthorization?
        ExistingAuthorization
    {
        get;
    }
}

/// <summary>
/// Bằng chứng Presentation đã hoàn thành bước
/// xác thực phương thức thanh toán.
///
/// Đây không phải xác nhận tự động từ ngân hàng.
/// </summary>
public sealed record SalesPaymentAuthorization
{
    public SalesPaymentAuthorization(
        PaymentMethod paymentMethod,
        long cashReceived,
        long confirmedPaymentAmount = 0,
        string? paymentReference = null,
        string? transferContent = null)
    {
        if (!Enum.IsDefined(
                paymentMethod))
        {
            throw new ArgumentOutOfRangeException(
                nameof(paymentMethod),
                paymentMethod,
                "Phương thức thanh toán không hợp lệ.");
        }

        if (cashReceived < 0 ||
            cashReceived >
            BusinessRules.Orders
                .MaximumOrderAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cashReceived),
                "Tiền khách đưa không hợp lệ.");
        }

        if (confirmedPaymentAmount < 0 ||
            confirmedPaymentAmount >
            BusinessRules.Orders
                .MaximumOrderAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confirmedPaymentAmount),
                "Số tiền đã xác nhận không hợp lệ.");
        }

        switch (paymentMethod)
        {
            case PaymentMethod.Cash:

                if (confirmedPaymentAmount !=
                    0)
                {
                    throw new ArgumentException(
                        "Thanh toán tiền mặt không được có " +
                        "số tiền xác nhận không dùng tiền mặt.",
                        nameof(confirmedPaymentAmount));
                }

                if (!string.IsNullOrWhiteSpace(
                        paymentReference) ||
                    !string.IsNullOrWhiteSpace(
                        transferContent))
                {
                    throw new ArgumentException(
                        "Thanh toán tiền mặt không được có " +
                        "thông tin đối soát VietQR.");
                }

                PaymentMethod =
                    PaymentMethod.Cash;

                CashReceived =
                    cashReceived;

                ConfirmedPaymentAmount =
                    0;

                return;

            case PaymentMethod.VietQr:

                if (cashReceived !=
                    0)
                {
                    throw new ArgumentException(
                        "Thanh toán VietQR phải có " +
                        "CashReceived bằng 0.",
                        nameof(cashReceived));
                }

                if (confirmedPaymentAmount <=
                    0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(confirmedPaymentAmount),
                        "Thanh toán VietQR phải có số tiền " +
                        "đã xác nhận lớn hơn 0.");
                }

                if (string.IsNullOrWhiteSpace(
                        paymentReference))
                {
                    throw new ArgumentException(
                        "Thanh toán VietQR phải có " +
                        "mã tham chiếu.",
                        nameof(paymentReference));
                }

                if (string.IsNullOrWhiteSpace(
                        transferContent))
                {
                    throw new ArgumentException(
                        "Thanh toán VietQR phải có " +
                        "nội dung chuyển khoản.",
                        nameof(transferContent));
                }

                PaymentMethod =
                    PaymentMethod.VietQr;

                CashReceived =
                    0;

                ConfirmedPaymentAmount =
                    confirmedPaymentAmount;

                PaymentReference =
                    paymentReference.Trim();

                TransferContent =
                    transferContent.Trim();

                return;

            case PaymentMethod.BankTransfer:
            case PaymentMethod.Card:

                throw new ArgumentException(
                    "Authorization hiện chỉ hỗ trợ " +
                    "tiền mặt và VietQR.",
                    nameof(paymentMethod));

            default:

                throw new ArgumentOutOfRangeException(
                    nameof(paymentMethod),
                    paymentMethod,
                    "Phương thức thanh toán không hợp lệ.");
        }
    }

    public PaymentMethod PaymentMethod
    {
        get;
    }

    /// <summary>
    /// Tiền khách giao trực tiếp.
    ///
    /// VietQR bắt buộc bằng 0.
    /// </summary>
    public long CashReceived
    {
        get;
    }

    /// <summary>
    /// Số tiền không dùng tiền mặt mà thu ngân
    /// đã xác nhận cửa hàng nhận được.
    ///
    /// Cash:
    /// bằng 0.
    ///
    /// VietQR:
    /// bằng đúng số tiền đã hiển thị trên QR và sẽ được
    /// gửi vào CheckoutRequest.ConfirmedPaymentAmount.
    /// </summary>
    public long ConfirmedPaymentAmount
    {
        get;
    }

    public string? PaymentReference
    {
        get;
    }

    public string? TransferContent
    {
        get;
    }

    public bool IsVietQr =>
        PaymentMethod ==
        PaymentMethod.VietQr;
}

/// <summary>
/// Kết quả của bước xác thực thanh toán.
///
/// Khi IsAuthorized = false:
/// - người dùng đã hủy dialog;
/// - chưa được phép gọi CheckoutService.
///
/// Khi IsAuthorized = true:
/// Authorization luôn khác null.
/// </summary>
public sealed record SalesPaymentAuthorizationOutcome
{
    private SalesPaymentAuthorizationOutcome(
        bool isAuthorized,
        SalesPaymentAuthorization?
            authorization)
    {
        if (isAuthorized &&
            authorization is null)
        {
            throw new ArgumentException(
                "Kết quả đã xác thực phải có authorization.",
                nameof(authorization));
        }

        if (!isAuthorized &&
            authorization is not null)
        {
            throw new ArgumentException(
                "Kết quả đã hủy không được có authorization.",
                nameof(authorization));
        }

        IsAuthorized =
            isAuthorized;

        Authorization =
            authorization;
    }

    public bool IsAuthorized
    {
        get;
    }

    public bool IsCancelled =>
        !IsAuthorized;

    public SalesPaymentAuthorization?
        Authorization
    {
        get;
    }

    public static SalesPaymentAuthorizationOutcome
        Authorized(
            SalesPaymentAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(
            authorization);

        return new SalesPaymentAuthorizationOutcome(
            isAuthorized:
                true,

            authorization:
                authorization);
    }

    public static SalesPaymentAuthorizationOutcome
        Cancelled()
    {
        return new SalesPaymentAuthorizationOutcome(
            isAuthorized:
                false,

            authorization:
                null);
    }
}

/// <summary>
/// Điều phối bước xác thực thanh toán trước Checkout.
///
/// Service không:
/// - tạo Order;
/// - thay đổi tồn kho;
/// - mở transaction;
/// - đánh dấu Order là Paid;
/// - xác nhận tự động từ ngân hàng.
/// </summary>
public interface ISalesPaymentFlowService
{
    bool IsVietQrEnabled
    {
        get;
    }

    Task<Result<SalesPaymentAuthorizationOutcome>>
        AuthorizeAsync(
            SalesPaymentAuthorizationRequest request,
            CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation điều phối tiền mặt và VietQR.
///
/// Singleton-safe:
/// - không giữ DbContext;
/// - sequence dùng Interlocked;
/// - không giữ authorization của từng đơn;
/// - authorization đang chờ do SalesViewModel sở hữu.
/// </summary>
public sealed class SalesPaymentFlowService :
    ISalesPaymentFlowService
{
    private const string
        VietQrReferencePrefix =
            "QR";

    private readonly IVietQrPaymentDialogService
        _vietQrDialogService;

    private readonly IClock
        _clock;

    private long
        _referenceSequence;

    public SalesPaymentFlowService(
        IVietQrPaymentDialogService
            vietQrDialogService,
        IClock clock)
    {
        _vietQrDialogService =
            vietQrDialogService ??
            throw new ArgumentNullException(
                nameof(vietQrDialogService));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));
    }

    public bool IsVietQrEnabled =>
        _vietQrDialogService.IsEnabled;

    public async Task<
        Result<SalesPaymentAuthorizationOutcome>>
        AuthorizeAsync(
            SalesPaymentAuthorizationRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        var validation =
            ValidateRequest(
                request);

        if (validation.IsFailure)
        {
            return Result.Failure<
                SalesPaymentAuthorizationOutcome>(
                    validation.Error);
        }

        return request.PaymentMethod switch
        {
            PaymentMethod.Cash =>
                AuthorizeCash(
                    request),

            PaymentMethod.VietQr =>
                await AuthorizeVietQrAsync(
                    request,
                    cancellationToken),

            PaymentMethod.BankTransfer or
            PaymentMethod.Card =>
                Failure(
                    ErrorCodes.Checkout
                        .PaymentMethodNotSupported,

                    "Luồng bán hàng hiện chỉ hỗ trợ " +
                    "tiền mặt và VietQR."),

            _ =>
                Failure(
                    ErrorCodes.Checkout
                        .InvalidPaymentMethod,

                    "Phương thức thanh toán không hợp lệ.")
        };
    }

    private static Result ValidateRequest(
        SalesPaymentAuthorizationRequest request)
    {
        if (!Enum.IsDefined(
                request.PaymentMethod))
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.Checkout
                        .InvalidPaymentMethod,

                    "Phương thức thanh toán không hợp lệ."));
        }

        if (request.TotalAmount <= 0 ||
            request.TotalAmount >
            BusinessRules.Orders
                .MaximumOrderAmount)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.Payments
                        .InvalidAmount,

                    "Tổng tiền thanh toán không hợp lệ."));
        }

        if (request.CashReceived < 0 ||
            request.CashReceived >
            BusinessRules.Orders
                .MaximumOrderAmount)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.Payments
                        .InvalidAmount,

                    "Tiền khách đưa không hợp lệ."));
        }

        return Result.Success();
    }

    private static Result<
        SalesPaymentAuthorizationOutcome>
        AuthorizeCash(
            SalesPaymentAuthorizationRequest request)
    {
        if (request.ExistingAuthorization is not null)
        {
            return Failure(
                ErrorCodes.General.Validation,

                "Không thể dùng xác nhận thanh toán cũ " +
                "cho phương thức tiền mặt.");
        }

        if (request.CashReceived <
            request.TotalAmount)
        {
            return Failure(
                ErrorCodes.Payments.InvalidAmount,

                "Tiền khách đưa chưa đủ thanh toán.");
        }

        var authorization =
            new SalesPaymentAuthorization(
                paymentMethod:
                    PaymentMethod.Cash,

                cashReceived:
                    request.CashReceived,

                confirmedPaymentAmount:
                    0);

        return Result.Success(
            SalesPaymentAuthorizationOutcome
                .Authorized(
                    authorization));
    }

    private async Task<
        Result<SalesPaymentAuthorizationOutcome>>
        AuthorizeVietQrAsync(
            SalesPaymentAuthorizationRequest request,
            CancellationToken cancellationToken)
    {
        if (request.CashReceived !=
            0)
        {
            return Failure(
                ErrorCodes.General.Validation,

                "Thanh toán VietQR không được nhập " +
                "tiền khách đưa.");
        }

        /*
         * Checkout trước đó có thể đã thất bại sau khi
         * thu ngân xác nhận tiền về.
         *
         * Trong trường hợp đó:
         * - kiểm tra authorization vẫn hợp lệ;
         * - kiểm tra tổng giỏ không bị thay đổi;
         * - sử dụng đúng số tiền khách đã chuyển;
         * - tuyệt đối không mở QR thứ hai.
         */
        if (request.ExistingAuthorization is not null)
        {
            var existingValidation =
                ValidateExistingVietQrAuthorization(
                    authorization:
                        request.ExistingAuthorization,

                    expectedTotalAmount:
                        request.TotalAmount);

            if (existingValidation.IsFailure)
            {
                return Result.Failure<
                    SalesPaymentAuthorizationOutcome>(
                        existingValidation.Error);
            }

            return Result.Success(
                SalesPaymentAuthorizationOutcome
                    .Authorized(
                        request.ExistingAuthorization));
        }

        if (!IsVietQrEnabled)
        {
            return Failure(
                ErrorCodes.Payments
                    .VietQrNotConfigured,

                "VietQR chưa được bật hoặc chưa được " +
                "cấu hình cho cửa hàng.");
        }

        var paymentReference =
            CreatePaymentReference();

        var dialogResult =
            await _vietQrDialogService
                .ShowAsync(
                    new VietQrPaymentDialogRequest(
                        amount:
                            request.TotalAmount,

                        paymentReference:
                            paymentReference),

                    cancellationToken);

        if (dialogResult.IsFailure)
        {
            return Result.Failure<
                SalesPaymentAuthorizationOutcome>(
                    dialogResult.Error);
        }

        if (!dialogResult.Value.Confirmed)
        {
            return Result.Success(
                SalesPaymentAuthorizationOutcome
                    .Cancelled());
        }

        if (string.IsNullOrWhiteSpace(
                dialogResult.Value.PaymentReference) ||
            string.IsNullOrWhiteSpace(
                dialogResult.Value.TransferContent))
        {
            return Failure(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Dialog VietQR đã xác nhận nhưng thiếu " +
                "thông tin đối soát.");
        }

        var authorization =
            new SalesPaymentAuthorization(
                paymentMethod:
                    PaymentMethod.VietQr,

                cashReceived:
                    0,

                confirmedPaymentAmount:
                    request.TotalAmount,

                paymentReference:
                    dialogResult.Value
                        .PaymentReference,

                transferContent:
                    dialogResult.Value
                        .TransferContent);

        return Result.Success(
            SalesPaymentAuthorizationOutcome
                .Authorized(
                    authorization));
    }

    private static Result
        ValidateExistingVietQrAuthorization(
            SalesPaymentAuthorization authorization,
            long expectedTotalAmount)
    {
        ArgumentNullException.ThrowIfNull(
            authorization);

        if (authorization.PaymentMethod !=
            PaymentMethod.VietQr)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.General.Validation,

                    "Authorization đang chờ không phải " +
                    "thanh toán VietQR."));
        }

        if (authorization.CashReceived !=
            0)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.General.Validation,

                    "Authorization VietQR không được có " +
                    "tiền mặt."));
        }

        if (authorization.ConfirmedPaymentAmount <=
            0)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.Payments.InvalidAmount,

                    "Authorization VietQR thiếu số tiền " +
                    "đã xác nhận."));
        }

        if (authorization.ConfirmedPaymentAmount !=
            expectedTotalAmount)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.Payments
                        .VietQrAmountMismatch,

                    "Tổng giỏ hàng đã thay đổi sau khi " +
                    "VietQR được xác nhận. " +
                    $"Đã nhận: " +
                    $"{authorization.ConfirmedPaymentAmount:N0} ₫; " +
                    $"tổng hiện tại: " +
                    $"{expectedTotalAmount:N0} ₫. " +
                    "Không được tạo hoặc yêu cầu khách " +
                    "thanh toán lại."));
        }

        if (string.IsNullOrWhiteSpace(
                authorization.PaymentReference) ||
            string.IsNullOrWhiteSpace(
                authorization.TransferContent))
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.Payments
                        .VietQrInvalidPayload,

                    "Authorization VietQR thiếu thông tin " +
                    "đối soát."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Tạo mã tham chiếu ngắn, chỉ gồm ASCII:
    ///
    /// QR + yyyyMMddHHmmssfff + sequence 6 chữ số.
    ///
    /// Sequence giúp tránh trùng khi hai yêu cầu được tạo
    /// trong cùng một millisecond.
    /// </summary>
    private string CreatePaymentReference()
    {
        var utcNow =
            _clock.UtcNow
                .ToUniversalTime();

        var sequence =
            Interlocked.Increment(
                ref _referenceSequence);

        return
            VietQrReferencePrefix +
            utcNow.ToString(
                "yyyyMMddHHmmssfff",
                CultureInfo.InvariantCulture) +
            sequence.ToString(
                "D6",
                CultureInfo.InvariantCulture);
    }

    private static Result<
        SalesPaymentAuthorizationOutcome>
        Failure(
            string code,
            string message)
    {
        return Result.Failure<
            SalesPaymentAuthorizationOutcome>(
                new Error(
                    code,
                    message));
    }
}