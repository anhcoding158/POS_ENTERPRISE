using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using POS.Application.DTOs.Payments;
using POS.Infrastructure.Payments;
using POS.Wpf.Services;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử lớp điều phối dialog VietQR mà không mở cửa sổ WPF.
///
/// Các test khóa những nguyên tắc:
/// - request phải hợp lệ và được chuẩn hóa;
/// - lỗi payload phải dừng trước khi tạo PNG;
/// - lỗi PNG phải được giữ nguyên mã lỗi;
/// - payload sai cấu trúc phải bị từ chối;
/// - VietQR bị tắt phải trả lỗi cấu hình;
/// - cancellation phải được tôn trọng;
/// - không có test nào tạo Order hoặc gọi ngân hàng.
/// </summary>
public sealed class VietQrPaymentDialogServiceTests
{
    [Fact]
    public void
        Dialog_request_must_trim_reference_and_transfer_content()
    {
        var request =
            new VietQrPaymentDialogRequest(
                amount:
                    125_000,

                paymentReference:
                    "  QR-20260724-0001  ",

                transferContent:
                    "  POS QR 20260724 0001  ");

        Assert.Equal(
            125_000,
            request.Amount);

        Assert.Equal(
            "QR-20260724-0001",
            request.PaymentReference);

        Assert.Equal(
            "POS QR 20260724 0001",
            request.TransferContent);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void
        Dialog_request_must_reject_non_positive_amount(
            long amount)
    {
        var exception =
            Assert.Throws<
                ArgumentOutOfRangeException>(
                    () =>
                        new VietQrPaymentDialogRequest(
                            amount:
                                amount,

                            paymentReference:
                                "QR-INVALID-AMOUNT"));

        Assert.Equal(
            "amount",
            exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void
        Dialog_request_must_reject_blank_payment_reference(
            string paymentReference)
    {
        var exception =
            Assert.Throws<
                ArgumentException>(
                    () =>
                        new VietQrPaymentDialogRequest(
                            amount:
                                10_000,

                            paymentReference:
                                paymentReference));

        Assert.Equal(
            "paymentReference",
            exception.ParamName);
    }

    [Fact]
    public async Task
        Payload_failure_must_be_returned_without_generating_png()
    {
        var expectedError =
            new Error(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Payload test không hợp lệ.");

        var fakeService =
            new FakeVietQrService(
                buildPayload:
                    _ =>
                        Result.Failure<string>(
                            expectedError),

                generatePng:
                    _ =>
                        throw new InvalidOperationException(
                            "GeneratePng không được gọi khi " +
                            "BuildPayload thất bại."));

        var dialogService =
            CreateDialogService(
                fakeService);

        var result =
            await dialogService.ShowAsync(
                new VietQrPaymentDialogRequest(
                    amount:
                        50_000,

                    paymentReference:
                        "QR-PAYLOAD-FAILURE"),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            expectedError.Code,
            result.Error.Code);

        Assert.Equal(
            expectedError.Message,
            result.Error.Message);

        Assert.Equal(
            1,
            fakeService.BuildPayloadCallCount);

        Assert.Equal(
            0,
            fakeService.GeneratePngCallCount);
    }

    [Fact]
    public async Task
        Png_failure_must_be_returned_after_successful_payload()
    {
        var expectedError =
            new Error(
                ErrorCodes.Payments
                    .VietQrGenerationFailed,

                "Không tạo được PNG trong test.");

        var fakeService =
            new FakeVietQrService(
                buildPayload:
                    _ =>
                        Result.Success(
                            "0002016304ABCD"),

                generatePng:
                    _ =>
                        Result.Failure<byte[]>(
                            expectedError));

        var dialogService =
            CreateDialogService(
                fakeService);

        var result =
            await dialogService.ShowAsync(
                new VietQrPaymentDialogRequest(
                    amount:
                        75_000,

                    paymentReference:
                        "QR-PNG-FAILURE"),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            expectedError.Code,
            result.Error.Code);

        Assert.Equal(
            expectedError.Message,
            result.Error.Message);

        Assert.Equal(
            1,
            fakeService.BuildPayloadCallCount);

        Assert.Equal(
            1,
            fakeService.GeneratePngCallCount);
    }

    [Fact]
    public async Task
        Malformed_payload_must_be_rejected_before_window_is_opened()
    {
        var fakeService =
            new FakeVietQrService(
                buildPayload:
                    _ =>
                        Result.Success(
                            "INVALID-PAYLOAD"),

                generatePng:
                    _ =>
                        Result.Success(
                            CreateMinimalPngSignature()));

        var dialogService =
            CreateDialogService(
                fakeService);

        var result =
            await dialogService.ShowAsync(
                new VietQrPaymentDialogRequest(
                    amount:
                        80_000,

                    paymentReference:
                        "QR-MALFORMED-PAYLOAD"),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrInvalidPayload,
            result.Error.Code);

        Assert.Contains(
            "TLV",
            result.Error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task
        Disabled_real_vietqr_service_must_return_not_configured()
    {
        var options =
            CreateOptions(
                enableVietQr:
                    false);

        var realVietQrService =
            new VietQrService(
                Options.Create(
                    options),

                NullLogger<VietQrService>
                    .Instance);

        var dialogService =
            new VietQrPaymentDialogService(
                realVietQrService,

                Options.Create(
                    options));

        var result =
            await dialogService.ShowAsync(
                new VietQrPaymentDialogRequest(
                    amount:
                        90_000,

                    paymentReference:
                        "QR-DISABLED"),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrNotConfigured,
            result.Error.Code);
    }

    [Fact]
    public async Task
        Pre_cancelled_token_must_stop_before_payload_generation()
    {
        var fakeService =
            new FakeVietQrService(
                buildPayload:
                    _ =>
                        throw new InvalidOperationException(
                            "BuildPayload không được gọi khi token " +
                            "đã bị hủy."),

                generatePng:
                    _ =>
                        throw new InvalidOperationException(
                            "GeneratePng không được gọi khi token " +
                            "đã bị hủy."));

        var dialogService =
            CreateDialogService(
                fakeService);

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAsync<
            OperationCanceledException>(
                () =>
                    dialogService.ShowAsync(
                        new VietQrPaymentDialogRequest(
                            amount:
                                100_000,

                            paymentReference:
                                "QR-CANCELLED"),

                        cancellationSource.Token));

        Assert.Equal(
            0,
            fakeService.BuildPayloadCallCount);

        Assert.Equal(
            0,
            fakeService.GeneratePngCallCount);
    }

    [Fact]
    public void
        Dialog_result_must_preserve_confirmation_reference_and_content()
    {
        var result =
            new VietQrPaymentDialogResult(
                Confirmed:
                    true,

                PaymentReference:
                    "QR-RESULT-001",

                TransferContent:
                    "POS QR RESULT 001");

        Assert.True(
            result.Confirmed);

        Assert.Equal(
            "QR-RESULT-001",
            result.PaymentReference);

        Assert.Equal(
            "POS QR RESULT 001",
            result.TransferContent);
    }

    private static VietQrPaymentDialogService
        CreateDialogService(
            IVietQrService vietQrService)
    {
        var options =
            CreateOptions(
                enableVietQr:
                    true);

        return new VietQrPaymentDialogService(
            vietQrService,

            Options.Create(
                options));
    }

    private static VietQrOptions CreateOptions(
        bool enableVietQr)
    {
        return new VietQrOptions
        {
            EnableVietQr =
                enableVietQr,

            BankBin =
                enableVietQr
                    ? "970422"
                    : string.Empty,

            AccountNumber =
                enableVietQr
                    ? "123456789"
                    : string.Empty,

            AccountName =
                enableVietQr
                    ? "NGUYEN VAN A"
                    : string.Empty,

            TransferContentPrefix =
                "POS",

            DisplayQrOnReceipt =
                true,

            QrPixelsPerModule =
                8
        };
    }

    private static byte[]
        CreateMinimalPngSignature()
    {
        return
        [
            0x89,
            0x50,
            0x4E,
            0x47,
            0x0D,
            0x0A,
            0x1A,
            0x0A
        ];
    }

    private sealed class FakeVietQrService :
        IVietQrService
    {
        private readonly Func<
            VietQrRequest,
            Result<string>>
            _buildPayload;

        private readonly Func<
            VietQrRequest,
            Result<byte[]>>
            _generatePng;

        public FakeVietQrService(
            Func<VietQrRequest, Result<string>>
                buildPayload,

            Func<VietQrRequest, Result<byte[]>>
                generatePng)
        {
            _buildPayload =
                buildPayload ??
                throw new ArgumentNullException(
                    nameof(buildPayload));

            _generatePng =
                generatePng ??
                throw new ArgumentNullException(
                    nameof(generatePng));
        }

        public int BuildPayloadCallCount
        {
            get;
            private set;
        }

        public int GeneratePngCallCount
        {
            get;
            private set;
        }

        public Result<string> BuildPayload(
            VietQrRequest request)
        {
            BuildPayloadCallCount++;

            return _buildPayload(
                request);
        }

        public Result<byte[]> GeneratePng(
            VietQrRequest request)
        {
            GeneratePngCallCount++;

            return _generatePng(
                request);
        }
    }
}