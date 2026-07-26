using POS.Application.Common;
using POS.Infrastructure.Payments;
using QRCoder;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử nền giải mã VietQR từ ảnh.
///
/// Test không:
/// - gọi mạng;
/// - dùng tài khoản ngân hàng thật;
/// - mở cửa sổ chọn file;
/// - thay đổi cấu hình;
/// - tạo hoặc thanh toán Order.
/// </summary>
public sealed class VietQrImageDecoderTests
{
    private const string
        ValidVietQrPayload =
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
        DecodePayload_must_decode_valid_vietqr_png()
    {
        var decoder =
            new VietQrImageDecoder();

        var pngBytes =
            CreateQrPng(
                ValidVietQrPayload);

        var result =
            decoder.DecodePayload(
                pngBytes);

        Assert.True(
            result.IsSuccess,
            result.Error.Message);

        Assert.Equal(
            ValidVietQrPayload,
            result.Value);
    }

    [Fact]
    public void
        DecodePayload_must_reject_blank_image()
    {
        var decoder =
            new VietQrImageDecoder();

        var result =
            decoder.DecodePayload(
                []);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrInvalidPayload,
            result.Error.Code);
    }

    [Fact]
    public void
        DecodePayload_must_reject_invalid_image_bytes()
    {
        var decoder =
            new VietQrImageDecoder();

        var result =
            decoder.DecodePayload(
            [
                0x01,
                0x02,
                0x03,
                0x04
            ]);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrInvalidPayload,
            result.Error.Code);

        Assert.Contains(
            "ảnh",
            result.Error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void
        DecodePayload_must_reject_normal_text_qr()
    {
        var decoder =
            new VietQrImageDecoder();

        var pngBytes =
            CreateQrPng(
                "https://example.com/not-vietqr");

        var result =
            decoder.DecodePayload(
                pngBytes);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrInvalidPayload,
            result.Error.Code);

        Assert.Contains(
            "không phải",
            result.Error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void
        DecodePayload_must_reject_qr_without_vietqr_identifier()
    {
        var decoder =
            new VietQrImageDecoder();

        var pngBytes =
            CreateQrPng(
                "00020101021253037045802VN6304FFFF");

        var result =
            decoder.DecodePayload(
                pngBytes);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrInvalidPayload,
            result.Error.Code);

        Assert.Contains(
            "định danh VietQR",
            result.Error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void
        DecodePayload_must_reject_image_above_byte_limit()
    {
        var decoder =
            new VietQrImageDecoder();

        var oversizedImage =
            new byte[
                15 * 1024 * 1024 +
                1];

        var result =
            decoder.DecodePayload(
                oversizedImage);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrInvalidPayload,
            result.Error.Code);

        Assert.Contains(
            "15 MB",
            result.Error.Message,
            StringComparison.Ordinal);
    }

    private static byte[] CreateQrPng(
        string content)
    {
        using var generator =
            new QRCodeGenerator();

        using var data =
            generator.CreateQrCode(
                content,
                QRCodeGenerator
                    .ECCLevel
                    .Q);

        using var qrCode =
            new PngByteQRCode(
                data);

        return qrCode.GetGraphic(
            pixelsPerModule:
                8,

            drawQuietZones:
                true);
    }
}