using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POS.Application.Common;
using POS.Application.DTOs.Payments;
using POS.Domain.Constants;
using POS.Infrastructure.Payments;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử VietQR payload và PNG.
///
/// Test không:
/// - gọi mạng;
/// - kết nối ngân hàng;
/// - thay đổi Order;
/// - yêu cầu tài khoản ngân hàng thật.
/// </summary>
public sealed class VietQrServiceTests
{
    private static readonly byte[]
        ExpectedPngSignature =
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

    /*
     * Golden vector cố định.
     *
     * Khi bất kỳ field, length hoặc CRC thay đổi ngoài ý muốn,
     * test này sẽ báo lỗi ngay.
     */
    private const string
        ExpectedGoldenPayload =
            "000201" +
            "010212" +
            "3853" +
            "0010A000000727" +
            "0123" +
            "0006970422" +
            "0109123456789" +
            "0208QRIBFTTA" +
            "5303704" +
            "5406135000" +
            "5802VN" +
            "6222" +
            "0818POS HD202607230001" +
            "630471CE";

    [Fact]
    public void
        BuildPayload_must_match_golden_vector()
    {
        var service =
            CreateService();

        var request =
            new VietQrRequest(
                amount:
                    135_000,

                orderCode:
                    "HD202607230001");

        var result =
            service.BuildPayload(
                request);

        Assert.True(
            result.IsSuccess,
            result.Error.Message);

        Assert.Equal(
            ExpectedGoldenPayload,
            result.Value);
    }

    [Fact]
    public void
        BuildPayload_must_normalize_vietnamese_transfer_content()
    {
        var service =
            CreateService();

        var request =
            new VietQrRequest(
                amount:
                    70_000,

                orderCode:
                    "HD-42",

                transferContent:
                    "Cà phê sữa đá - đơn 42");

        var result =
            service.BuildPayload(
                request);

        Assert.True(
            result.IsSuccess,
            result.Error.Message);

        Assert.Contains(
            "POS CA PHE SUA DA DON 42",
            result.Value);

        Assert.DoesNotContain(
            "CÀ",
            result.Value);

        Assert.DoesNotContain(
            "POS POS",
            result.Value);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void
        BuildPayload_must_reject_non_positive_amount(
            long amount)
    {
        var service =
            CreateService();

        var request =
            new VietQrRequest(
                amount:
                    amount,

                orderCode:
                    "HD-INVALID-AMOUNT");

        var result =
            service.BuildPayload(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments.InvalidAmount,
            result.Error.Code);
    }

    [Fact]
    public void
        BuildPayload_must_reject_amount_above_order_limit()
    {
        var service =
            CreateService();

        var invalidAmount =
            checked(
                BusinessRules.Orders
                    .MaximumOrderAmount +
                1);

        var request =
            new VietQrRequest(
                amount:
                    invalidAmount,

                orderCode:
                    "HD-AMOUNT-TOO-LARGE");

        var result =
            service.BuildPayload(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments.InvalidAmount,
            result.Error.Code);
    }

    [Fact]
    public void
        BuildPayload_must_fail_when_vietqr_is_disabled()
    {
        var options =
            CreateValidOptions();

        options.EnableVietQr =
            false;

        var service =
            CreateService(
                options);

        var request =
            new VietQrRequest(
                amount:
                    10_000,

                orderCode:
                    "HD-DISABLED");

        var result =
            service.BuildPayload(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrNotConfigured,
            result.Error.Code);
    }

    [Fact]
    public void
        BuildPayload_must_reject_blank_order_code()
    {
        var service =
            CreateService();

        var request =
            new VietQrRequest(
                amount:
                    10_000,

                orderCode:
                    "   ");

        var result =
            service.BuildPayload(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrInvalidPayload,
            result.Error.Code);
    }

    [Fact]
    public void
        BuildPayload_must_reject_transfer_content_over_limit()
    {
        var service =
            CreateService();

        var request =
            new VietQrRequest(
                amount:
                    10_000,

                orderCode:
                    "HD-LONG-CONTENT",

                transferContent:
                    new string(
                        'A',
                        51));

        var result =
            service.BuildPayload(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrInvalidPayload,
            result.Error.Code);

        Assert.Contains(
            "50",
            result.Error.Message);
    }

    [Fact]
    public void
        BuildPayload_must_not_duplicate_configured_prefix()
    {
        var service =
            CreateService();

        var request =
            new VietQrRequest(
                amount:
                    25_000,

                orderCode:
                    "HD-PREFIX",

                transferContent:
                    "pos hd-prefix");

        var result =
            service.BuildPayload(
                request);

        Assert.True(
            result.IsSuccess,
            result.Error.Message);

        Assert.Contains(
            "POS HD PREFIX",
            result.Value);

        Assert.DoesNotContain(
            "POS POS",
            result.Value);
    }

    [Fact]
    public void
        GeneratePng_must_return_valid_png_signature()
    {
        var service =
            CreateService();

        var request =
            new VietQrRequest(
                amount:
                    135_000,

                orderCode:
                    "HD-PNG");

        var result =
            service.GeneratePng(
                request);

        Assert.True(
            result.IsSuccess,
            result.Error.Message);

        Assert.True(
            result.Value.Length >
            ExpectedPngSignature.Length);

        var actualSignature =
            result.Value[
                ..ExpectedPngSignature.Length];

        Assert.Equal(
            ExpectedPngSignature,
            actualSignature);
    }

    [Fact]
    public void
        GeneratePng_must_be_deterministic_for_same_request()
    {
        var service =
            CreateService();

        var request =
            new VietQrRequest(
                amount:
                    88_000,

                orderCode:
                    "HD-DETERMINISTIC");

        var firstResult =
            service.GeneratePng(
                request);

        var secondResult =
            service.GeneratePng(
                request);

        Assert.True(
            firstResult.IsSuccess,
            firstResult.Error.Message);

        Assert.True(
            secondResult.IsSuccess,
            secondResult.Error.Message);

        Assert.Equal(
            firstResult.Value,
            secondResult.Value);
    }

    [Fact]
    public void
        Constructor_must_reject_invalid_enabled_configuration()
    {
        var options =
            CreateValidOptions();

        options.BankBin =
            "97042";

        Assert.Throws<
            InvalidOperationException>(
                () =>
                    CreateService(
                        options));
    }

    private static VietQrService CreateService(
        VietQrOptions? options = null)
    {
        return new VietQrService(
            Options.Create(
                options ??
                CreateValidOptions()),

            NullLogger<VietQrService>
                .Instance);
    }

    private static VietQrOptions
        CreateValidOptions()
    {
        return new VietQrOptions
        {
            EnableVietQr =
                true,

            BankBin =
                "970422",

            AccountNumber =
                "123456789",

            AccountName =
                "NGUYEN VAN A",

            TransferContentPrefix =
                "POS",

            DisplayQrOnReceipt =
                true,

            QrPixelsPerModule =
                8
        };
    }
}