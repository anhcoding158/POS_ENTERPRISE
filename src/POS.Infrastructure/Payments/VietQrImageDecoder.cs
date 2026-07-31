using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using ZXing.Common;
using BarcodeFormat =
    ZXing.BarcodeFormat;
using BarcodeReaderGeneric =
    ZXing.BarcodeReaderGeneric;
using RgbLuminanceSource =
    ZXing.RGBLuminanceSource;

namespace POS.Infrastructure.Payments;

/// <summary>
/// Giải mã payload VietQR từ dữ liệu ảnh.
///
/// Luồng xử lý:
/// - đọc ảnh bằng WPF BitmapDecoder;
/// - chuyển pixel về BGRA32;
/// - tạo RGBLuminanceSource;
/// - chỉ cho ZXing tìm QR Code;
/// - kiểm tra QR có dấu hiệu nhận diện VietQR/NAPAS.
///
/// Service không kết nối mạng và không thay đổi cấu hình.
/// </summary>
public sealed class VietQrImageDecoder :
    IVietQrImageDecoder
{
    private const int
        MaximumImageBytes =
            15 * 1024 * 1024;

    private const int
        MaximumPixelDimension =
            12_000;

    private const long
        MaximumPixelCount =
            64_000_000;

    private const string
        EmvPayloadPrefix =
            "000201";

    private const string
        VietQrGloballyUniqueIdentifier =
            "A000000727";

    public Result<string> DecodePayload(
        byte[] imageBytes)
    {
        if (imageBytes is null ||
            imageBytes.Length == 0)
        {
            return Failure(
                "Ảnh QR không được để trống.");
        }

        if (imageBytes.Length >
            MaximumImageBytes)
        {
            return Failure(
                $"Ảnh QR không được lớn hơn " +
                $"{MaximumImageBytes / 1024 / 1024} MB.");
        }

        try
        {
            using var stream =
                new MemoryStream(
                    imageBytes,
                    writable:
                        false);

            var decoder =
                BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions
                        .PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
            {
                return Failure(
                    "File ảnh không có khung hình hợp lệ.");
            }

            var frame =
                decoder.Frames[0];

            var dimensionValidation =
                ValidateDimensions(
                    frame.PixelWidth,
                    frame.PixelHeight);

            if (dimensionValidation.IsFailure)
            {
                return Result.Failure<string>(
                    dimensionValidation.AppError);
            }

            var bitmap =
                ConvertToBgra32(
                    frame);

            var stride =
                checked(
                    bitmap.PixelWidth *
                    4);

            var pixelBuffer =
                new byte[
                    checked(
                        stride *
                        bitmap.PixelHeight)];

            bitmap.CopyPixels(
                pixelBuffer,
                stride,
                offset:
                    0);

            var luminanceSource =
                new RgbLuminanceSource(
                    pixelBuffer,
                    bitmap.PixelWidth,
                    bitmap.PixelHeight,
                    RgbLuminanceSource
                        .BitmapFormat
                        .BGRA32);

            var reader =
                new BarcodeReaderGeneric
                {
                    AutoRotate =
                        true,

                    Options =
                        new DecodingOptions
                        {
                            TryInverted =
                                true,

                            TryHarder =
                                true,

                            PossibleFormats =
                            [
                                BarcodeFormat
                                    .QR_CODE
                            ]
                        }
                };

            var decodedResult =
                reader.Decode(
                    luminanceSource);

            if (decodedResult is null ||
                string.IsNullOrWhiteSpace(
                    decodedResult.Text))
            {
                return Failure(
                    "Không tìm thấy mã QR có thể đọc trong ảnh.");
            }

            if (decodedResult.BarcodeFormat !=
                BarcodeFormat.QR_CODE)
            {
                return Failure(
                    "Ảnh không chứa mã QR hợp lệ.");
            }

            var payload =
                decodedResult.Text.Trim();

            var vietQrValidation =
                ValidateVietQrIdentity(
                    payload);

            if (vietQrValidation.IsFailure)
            {
                return Result.Failure<string>(
                    vietQrValidation.AppError);
            }

            return Result.Success(
                payload);
        }
        catch (OverflowException)
        {
            return Failure(
                "Kích thước ảnh QR vượt quá giới hạn xử lý.");
        }
        catch (Exception)
        {
            /*
             * Không đưa payload, tài khoản hoặc dữ liệu ảnh
             * vào thông báo lỗi vì đây có thể là dữ liệu
             * riêng của cửa hàng.
             */
            return Failure(
                "Không thể đọc file ảnh QR. " +
                "Hãy chọn ảnh PNG, JPG, JPEG hoặc BMP rõ nét.");
        }
    }

    private static Result ValidateDimensions(
        int width,
        int height)
    {
        if (width <= 0 ||
            height <= 0)
        {
            return Result.Failure(
                new AppError(
                    ErrorCodes.Payments
                        .VietQrInvalidPayload,

                    "Ảnh QR có kích thước không hợp lệ."));
        }

        if (width >
                MaximumPixelDimension ||
            height >
                MaximumPixelDimension)
        {
            return Result.Failure(
                new AppError(
                    ErrorCodes.Payments
                        .VietQrInvalidPayload,

                    $"Chiều rộng và chiều cao ảnh QR " +
                    $"không được vượt quá " +
                    $"{MaximumPixelDimension:N0} pixel."));
        }

        var pixelCount =
            checked(
                (long)width *
                height);

        if (pixelCount >
            MaximumPixelCount)
        {
            return Result.Failure(
                new AppError(
                    ErrorCodes.Payments
                        .VietQrInvalidPayload,

                    "Ảnh QR có tổng số pixel vượt quá " +
                    "giới hạn xử lý an toàn."));
        }

        return Result.Success();
    }

    private static BitmapSource ConvertToBgra32(
        BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        if (source.Format ==
            PixelFormats.Bgra32)
        {
            if (source.CanFreeze &&
                !source.IsFrozen)
            {
                source.Freeze();
            }

            return source;
        }

        var converted =
            new FormatConvertedBitmap(
                source,
                PixelFormats.Bgra32,
                destinationPalette:
                    null,
                alphaThreshold:
                    0);

        if (converted.CanFreeze &&
            !converted.IsFrozen)
        {
            converted.Freeze();
        }

        return converted;
    }

    private static Result
        ValidateVietQrIdentity(
            string payload)
    {
        if (!payload.StartsWith(
                EmvPayloadPrefix,
                StringComparison.Ordinal))
        {
            return Result.Failure(
                new AppError(
                    ErrorCodes.Payments
                        .VietQrInvalidPayload,

                    "Ảnh có mã QR nhưng không phải payload " +
                    "EMVCo/VietQR được hỗ trợ."));
        }

        if (!payload.Contains(
                VietQrGloballyUniqueIdentifier,
                StringComparison.Ordinal))
        {
            return Result.Failure(
                new AppError(
                    ErrorCodes.Payments
                        .VietQrInvalidPayload,

                    "Ảnh có mã QR nhưng không chứa định danh VietQR."));
        }

        return Result.Success();
    }

    private static Result<string> Failure(
        string message)
    {
        return Result.Failure<string>(
            new AppError(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                message));
    }
}
