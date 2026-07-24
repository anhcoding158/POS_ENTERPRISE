using POS.Application.Abstractions.DateTime;
using POS.Application.Common;
using POS.Domain.Enums;
using POS.Wpf.Services;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử điều phối thanh toán trước Checkout.
///
/// Không test nào:
/// - mở WPF Window;
/// - tạo Order;
/// - kết nối database;
/// - gọi ngân hàng;
/// - thay đổi tồn kho.
/// </summary>
public sealed class SalesPaymentFlowServiceTests
{
    private static readonly DateTimeOffset
        UtcNow =
            new(
                2026,
                7,
                24,
                15,
                30,
                45,
                123,
                TimeSpan.Zero);

    [Fact]
    public async Task
        Cash_with_sufficient_amount_must_be_authorized()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    false);

        var service =
            CreateService(
                dialog);

        var result =
            await service.AuthorizeAsync(
                new SalesPaymentAuthorizationRequest(
                    paymentMethod:
                        PaymentMethod.Cash,

                    totalAmount:
                        75_000,

                    cashReceived:
                        100_000),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        Assert.True(
            result.Value.IsAuthorized);

        Assert.False(
            result.Value.IsCancelled);

        var authorization =
            Assert.IsType<
                SalesPaymentAuthorization>(
                    result.Value.Authorization);

        Assert.Equal(
            PaymentMethod.Cash,
            authorization.PaymentMethod);

        Assert.Equal(
            100_000,
            authorization.CashReceived);

        Assert.Equal(
            0,
            authorization
                .ConfirmedPaymentAmount);

        Assert.Null(
            authorization.PaymentReference);

        Assert.Null(
            authorization.TransferContent);

        Assert.Equal(
            0,
            dialog.ShowCallCount);
    }

    [Fact]
    public async Task
        Cash_with_insufficient_amount_must_fail()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    false);

        var service =
            CreateService(
                dialog);

        var result =
            await service.AuthorizeAsync(
                new SalesPaymentAuthorizationRequest(
                    paymentMethod:
                        PaymentMethod.Cash,

                    totalAmount:
                        100_000,

                    cashReceived:
                        99_999),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .InvalidAmount,
            result.Error.Code);

        Assert.Equal(
            0,
            dialog.ShowCallCount);
    }

    [Fact]
    public async Task
        Disabled_vietqr_must_fail_before_opening_dialog()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    false);

        var service =
            CreateService(
                dialog);

        var result =
            await service.AuthorizeAsync(
                new SalesPaymentAuthorizationRequest(
                    paymentMethod:
                        PaymentMethod.VietQr,

                    totalAmount:
                        125_000,

                    cashReceived:
                        0),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrNotConfigured,
            result.Error.Code);

        Assert.Equal(
            0,
            dialog.ShowCallCount);
    }

    [Fact]
    public async Task
        Cancelled_vietqr_dialog_must_not_authorize_checkout()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    true,

                resultFactory:
                    request =>
                        Result.Success(
                            new VietQrPaymentDialogResult(
                                Confirmed:
                                    false,

                                PaymentReference:
                                    request
                                        .PaymentReference,

                                TransferContent:
                                    string.Empty)));

        var service =
            CreateService(
                dialog);

        var result =
            await service.AuthorizeAsync(
                new SalesPaymentAuthorizationRequest(
                    paymentMethod:
                        PaymentMethod.VietQr,

                    totalAmount:
                        125_000,

                    cashReceived:
                        0),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        Assert.False(
            result.Value.IsAuthorized);

        Assert.True(
            result.Value.IsCancelled);

        Assert.Null(
            result.Value.Authorization);

        Assert.Equal(
            1,
            dialog.ShowCallCount);
    }

    [Fact]
    public async Task
        Confirmed_vietqr_must_create_authorization_with_exact_amount()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    true,

                resultFactory:
                    request =>
                        Result.Success(
                            new VietQrPaymentDialogResult(
                                Confirmed:
                                    true,

                                PaymentReference:
                                    request
                                        .PaymentReference,

                                TransferContent:
                                    "POS " +
                                    request
                                        .PaymentReference)));

        var service =
            CreateService(
                dialog);

        var result =
            await service.AuthorizeAsync(
                new SalesPaymentAuthorizationRequest(
                    paymentMethod:
                        PaymentMethod.VietQr,

                    totalAmount:
                        135_000,

                    cashReceived:
                        0),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        Assert.True(
            result.Value.IsAuthorized);

        var authorization =
            Assert.IsType<
                SalesPaymentAuthorization>(
                    result.Value.Authorization);

        Assert.Equal(
            PaymentMethod.VietQr,
            authorization.PaymentMethod);

        Assert.Equal(
            0,
            authorization.CashReceived);

        Assert.Equal(
            135_000,
            authorization
                .ConfirmedPaymentAmount);

        Assert.Equal(
            "QR20260724153045123000001",
            authorization.PaymentReference);

        Assert.Equal(
            "POS QR20260724153045123000001",
            authorization.TransferContent);

        Assert.Equal(
            1,
            dialog.ShowCallCount);
    }

    [Fact]
    public async Task
        Consecutive_vietqr_requests_must_have_unique_references()
    {
        var references =
            new List<string>();

        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    true,

                resultFactory:
                    request =>
                    {
                        references.Add(
                            request.PaymentReference);

                        return Result.Success(
                            new VietQrPaymentDialogResult(
                                Confirmed:
                                    false,

                                PaymentReference:
                                    request.PaymentReference,

                                TransferContent:
                                    string.Empty));
                    });

        var service =
            CreateService(
                dialog);

        for (var index = 0;
             index < 2;
             index++)
        {
            var result =
                await service.AuthorizeAsync(
                    new SalesPaymentAuthorizationRequest(
                        paymentMethod:
                            PaymentMethod.VietQr,

                        totalAmount:
                            10_000,

                        cashReceived:
                            0),

                    TestContext
                        .Current
                        .CancellationToken);

            Assert.True(
                result.IsSuccess,
                result.Error.ToString());
        }

        Assert.Equal(
            2,
            references.Count);

        Assert.NotEqual(
            references[0],
            references[1]);

        Assert.EndsWith(
            "000001",
            references[0],
            StringComparison.Ordinal);

        Assert.EndsWith(
            "000002",
            references[1],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task
        Existing_vietqr_authorization_must_be_reused_without_new_dialog()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    true,

                resultFactory:
                    _ =>
                        throw new InvalidOperationException(
                            "Dialog không được mở lại khi đã có " +
                            "authorization VietQR."));

        var service =
            CreateService(
                dialog);

        var existingAuthorization =
            new SalesPaymentAuthorization(
                paymentMethod:
                    PaymentMethod.VietQr,

                cashReceived:
                    0,

                confirmedPaymentAmount:
                    150_000,

                paymentReference:
                    "QR-EXISTING-001",

                transferContent:
                    "POS QR EXISTING 001");

        var result =
            await service.AuthorizeAsync(
                new SalesPaymentAuthorizationRequest(
                    paymentMethod:
                        PaymentMethod.VietQr,

                    totalAmount:
                        150_000,

                    cashReceived:
                        0,

                    existingAuthorization:
                        existingAuthorization),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        Assert.True(
            result.Value.IsAuthorized);

        Assert.Same(
            existingAuthorization,
            result.Value.Authorization);

        Assert.Equal(
            150_000,
            result.Value
                .Authorization!
                .ConfirmedPaymentAmount);

        Assert.Equal(
            0,
            dialog.ShowCallCount);
    }

    [Fact]
    public async Task
        Existing_vietqr_authorization_with_changed_total_must_fail()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    true,

                resultFactory:
                    _ =>
                        throw new InvalidOperationException(
                            "Dialog không được mở khi kiểm tra " +
                            "authorization cũ."));

        var service =
            CreateService(
                dialog);

        var existingAuthorization =
            new SalesPaymentAuthorization(
                paymentMethod:
                    PaymentMethod.VietQr,

                cashReceived:
                    0,

                confirmedPaymentAmount:
                    150_000,

                paymentReference:
                    "QR-EXISTING-MISMATCH",

                transferContent:
                    "POS QR EXISTING MISMATCH");

        var result =
            await service.AuthorizeAsync(
                new SalesPaymentAuthorizationRequest(
                    paymentMethod:
                        PaymentMethod.VietQr,

                    totalAmount:
                        160_000,

                    cashReceived:
                        0,

                    existingAuthorization:
                        existingAuthorization),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrAmountMismatch,
            result.Error.Code);

        Assert.Contains(
            "thay đổi",
            result.Error.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            0,
            dialog.ShowCallCount);
    }

    [Fact]
    public async Task
        Existing_vietqr_authorization_can_be_reused_after_configuration_is_disabled()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    false,

                resultFactory:
                    _ =>
                        throw new InvalidOperationException(
                            "Không được mở QR mới khi đã nhận tiền."));

        var service =
            CreateService(
                dialog);

        var existingAuthorization =
            new SalesPaymentAuthorization(
                paymentMethod:
                    PaymentMethod.VietQr,

                cashReceived:
                    0,

                confirmedPaymentAmount:
                    175_000,

                paymentReference:
                    "QR-EXISTING-DISABLED",

                transferContent:
                    "POS QR EXISTING DISABLED");

        var result =
            await service.AuthorizeAsync(
                new SalesPaymentAuthorizationRequest(
                    paymentMethod:
                        PaymentMethod.VietQr,

                    totalAmount:
                        175_000,

                    cashReceived:
                        0,

                    existingAuthorization:
                        existingAuthorization),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        Assert.Same(
            existingAuthorization,
            result.Value.Authorization);

        Assert.Equal(
            0,
            dialog.ShowCallCount);
    }

    [Fact]
    public async Task
        Vietqr_with_cash_received_must_fail()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    true);

        var service =
            CreateService(
                dialog);

        var result =
            await service.AuthorizeAsync(
                new SalesPaymentAuthorizationRequest(
                    paymentMethod:
                        PaymentMethod.VietQr,

                    totalAmount:
                        100_000,

                    cashReceived:
                        100_000),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Validation,
            result.Error.Code);

        Assert.Equal(
            0,
            dialog.ShowCallCount);
    }

    [Fact]
    public async Task
        Unsupported_payment_method_must_fail()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    true);

        var service =
            CreateService(
                dialog);

        var result =
            await service.AuthorizeAsync(
                new SalesPaymentAuthorizationRequest(
                    paymentMethod:
                        PaymentMethod.Card,

                    totalAmount:
                        100_000,

                    cashReceived:
                        0),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Checkout
                .PaymentMethodNotSupported,
            result.Error.Code);

        Assert.Equal(
            0,
            dialog.ShowCallCount);
    }

    [Fact]
    public async Task
        Pre_cancelled_token_must_stop_before_dialog()
    {
        var dialog =
            new FakeVietQrDialogService(
                isEnabled:
                    true);

        var service =
            CreateService(
                dialog);

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAsync<
            OperationCanceledException>(
                () =>
                    service.AuthorizeAsync(
                        new SalesPaymentAuthorizationRequest(
                            paymentMethod:
                                PaymentMethod.VietQr,

                            totalAmount:
                                100_000,

                            cashReceived:
                                0),

                        cancellationSource.Token));

        Assert.Equal(
            0,
            dialog.ShowCallCount);
    }

    [Fact]
    public void
        Cash_authorization_must_reject_confirmed_non_cash_amount()
    {
        var exception =
            Assert.Throws<
                ArgumentException>(
                    () =>
                        new SalesPaymentAuthorization(
                            paymentMethod:
                                PaymentMethod.Cash,

                            cashReceived:
                                100_000,

                            confirmedPaymentAmount:
                                100_000));

        Assert.Equal(
            "confirmedPaymentAmount",
            exception.ParamName);
    }

    [Fact]
    public void
        Vietqr_authorization_must_require_confirmed_amount()
    {
        var exception =
            Assert.Throws<
                ArgumentOutOfRangeException>(
                    () =>
                        new SalesPaymentAuthorization(
                            paymentMethod:
                                PaymentMethod.VietQr,

                            cashReceived:
                                0,

                            confirmedPaymentAmount:
                                0,

                            paymentReference:
                                "QR-NO-AMOUNT",

                            transferContent:
                                "POS QR NO AMOUNT"));

        Assert.Equal(
            "confirmedPaymentAmount",
            exception.ParamName);
    }

    private static SalesPaymentFlowService
        CreateService(
            IVietQrPaymentDialogService dialog)
    {
        return new SalesPaymentFlowService(
            dialog,

            new FixedClock(
                UtcNow));
    }

    private sealed class FixedClock :
        IClock
    {
        public FixedClock(
            DateTimeOffset utcNow)
        {
            UtcNow =
                utcNow.ToUniversalTime();
        }

        public DateTimeOffset UtcNow
        {
            get;
        }
    }

    private sealed class
        FakeVietQrDialogService :
        IVietQrPaymentDialogService
    {
        private readonly Func<
            VietQrPaymentDialogRequest,
            Result<VietQrPaymentDialogResult>>
            _resultFactory;

        public FakeVietQrDialogService(
            bool isEnabled,
            Func<
                VietQrPaymentDialogRequest,
                Result<VietQrPaymentDialogResult>>?
                resultFactory = null)
        {
            IsEnabled =
                isEnabled;

            _resultFactory =
                resultFactory ??
                (request =>
                    Result.Success(
                        new VietQrPaymentDialogResult(
                            Confirmed:
                                false,

                            PaymentReference:
                                request.PaymentReference,

                            TransferContent:
                                string.Empty)));
        }

        public bool IsEnabled
        {
            get;
        }

        public int ShowCallCount
        {
            get;
            private set;
        }

        public Task<Result<VietQrPaymentDialogResult>>
            ShowAsync(
                VietQrPaymentDialogRequest request,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken
                .ThrowIfCancellationRequested();

            ShowCallCount++;

            return Task.FromResult(
                _resultFactory(
                    request));
        }
    }
}