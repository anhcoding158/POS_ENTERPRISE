using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using POS.Application.DTOs.Payments;
using POS.Domain.Constants;
using QRCoder;

namespace POS.Infrastructure.Payments;

/// <summary>
/// Tạo payload VietQR chuyển tiền tới tài khoản ngân hàng
/// và kết xuất payload thành ảnh PNG.
///
/// Service này chỉ thực hiện:
/// - kiểm tra cấu hình VietQR;
/// - kiểm tra số tiền và nội dung chuyển khoản;
/// - dựng payload TLV;
/// - tính CRC16/CCITT-FALSE;
/// - tạo ảnh QR bằng QRCoder.
///
/// Service không:
/// - gọi API ngân hàng;
/// - kiểm tra tiền đã về tài khoản;
/// - thay đổi trạng thái Order;
/// - tự động đánh dấu giao dịch là Paid;
/// - ghi số tài khoản hoặc payload đầy đủ vào log.
/// </summary>
public sealed class VietQrService :
    IVietQrService
{
    /*
     * EMVCo payload format indicator.
     *
     * 01 = phiên bản payload hiện tại.
     */
    private const string
        PayloadFormatIndicator = "01";

    /*
     * Point of Initiation Method:
     *
     * 12 = dynamic QR.
     *
     * QR của một đơn bán hàng có số tiền và nội dung
     * riêng, vì vậy được xem là dynamic QR.
     */
    private const string
        DynamicPointOfInitiationMethod = "12";

    /*
     * Globally Unique Identifier được VietQR sử dụng
     * trong Merchant Account Information.
     */
    private const string
        VietQrGloballyUniqueIdentifier =
            "A000000727";

    /*
     * QRIBFTTA:
     * QR Inter-Bank Funds Transfer To Account.
     *
     * Payload chuyển tiền tới số tài khoản ngân hàng.
     */
    private const string
        TransferToAccountServiceCode =
            "QRIBFTTA";

    /*
     * ISO 4217 numeric currency code:
     * 704 = Vietnamese đồng.
     */
    private const string
        VietnameseDongCurrencyCode =
            "704";

    private const string
        VietnamCountryCode =
            "VN";

    /*
     * Field length của TLV chỉ có hai chữ số,
     * nên một value không được vượt quá 99 byte.
     */
    private const int
        MaximumTlvValueByteLength =
            99;

    /*
     * Giới hạn nghiệp vụ riêng cho nội dung chuyển khoản.
     *
     * Đặt thấp hơn giới hạn kỹ thuật 99 byte để:
     * - tương thích tốt hơn với ứng dụng ngân hàng;
     * - nội dung dễ đọc và dễ đối soát;
     * - tránh QR quá dày trên màn hình nhỏ.
     */
    private const int
        MaximumTransferContentLength =
            50;

    private readonly VietQrOptions
        _options;

    private readonly ILogger<VietQrService>
        _logger;

    public VietQrService(
        IOptions<VietQrOptions> options,
        ILogger<VietQrService> logger)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        _options =
            options.Value ??
            throw new ArgumentException(
                "Không đọc được cấu hình VietQR.",
                nameof(options));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        /*
         * Fail fast khi cấu hình được bật nhưng sai.
         *
         * Khi EnableVietQr = false, VietQrOptions cho phép
         * thông tin ngân hàng chưa được cấu hình.
         */
        _options.Validate();
    }

    /// <summary>
    /// Dựng payload VietQR hoàn chỉnh, bao gồm CRC.
    ///
    /// Thứ tự field chính:
    /// - 00: Payload Format Indicator;
    /// - 01: Point of Initiation Method;
    /// - 38: Merchant Account Information;
    /// - 53: Currency Code;
    /// - 54: Amount;
    /// - 58: Country Code;
    /// - 62: Additional Data;
    /// - 63: CRC.
    /// </summary>
    public Result<string> BuildPayload(
        VietQrRequest request)
    {
        if (request is null)
        {
            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Yêu cầu tạo VietQR không được để trống.");
        }

        if (!_options.EnableVietQr)
        {
            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrNotConfigured,

                "VietQR đang bị tắt trong cấu hình ứng dụng.");
        }

        if (request.Amount <= 0 ||
            request.Amount >
            BusinessRules.Orders
                .MaximumOrderAmount)
        {
            return Failure<string>(
                ErrorCodes.Payments.InvalidAmount,
                "Số tiền VietQR không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(
                request.OrderCode))
        {
            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Mã đơn hàng dùng cho VietQR không được để trống.");
        }

        var transferContentResult =
            BuildTransferContent(
                request);

        if (transferContentResult.IsFailure)
        {
            return Result.Failure<string>(
                transferContentResult.Error);
        }

        try
        {
            var beneficiaryAccountInformation =
                CreateTlv(
                    tag:
                        "00",

                    value:
                        _options
                            .GetNormalizedBankBin())

                +

                CreateTlv(
                    tag:
                        "01",

                    value:
                        _options
                            .GetNormalizedAccountNumber());

            /*
             * Merchant Account Information - Tag 38.
             *
             * 00:
             *   Globally Unique Identifier của VietQR.
             *
             * 01:
             *   Thông tin người thụ hưởng:
             *   - 00: BIN ngân hàng;
             *   - 01: số tài khoản.
             *
             * 02:
             *   Dịch vụ chuyển tiền tới tài khoản.
             */
            var merchantAccountInformation =
                CreateTlv(
                    tag:
                        "00",

                    value:
                        VietQrGloballyUniqueIdentifier)

                +

                CreateTlv(
                    tag:
                        "01",

                    value:
                        beneficiaryAccountInformation)

                +

                CreateTlv(
                    tag:
                        "02",

                    value:
                        TransferToAccountServiceCode);

            /*
             * Additional Data Field Template - Tag 62.
             *
             * Sub-tag 08 chứa nội dung chuyển khoản
             * phục vụ đối soát đơn hàng.
             */
            var additionalData =
                CreateTlv(
                    tag:
                        "08",

                    value:
                        transferContentResult.Value);

            var amountText =
                request.Amount.ToString(
                    CultureInfo.InvariantCulture);

            var payloadWithoutCrc =
                CreateTlv(
                    tag:
                        "00",

                    value:
                        PayloadFormatIndicator)

                +

                CreateTlv(
                    tag:
                        "01",

                    value:
                        DynamicPointOfInitiationMethod)

                +

                CreateTlv(
                    tag:
                        "38",

                    value:
                        merchantAccountInformation)

                +

                CreateTlv(
                    tag:
                        "53",

                    value:
                        VietnameseDongCurrencyCode)

                +

                CreateTlv(
                    tag:
                        "54",

                    value:
                        amountText)

                +

                CreateTlv(
                    tag:
                        "58",

                    value:
                        VietnamCountryCode)

                +

                CreateTlv(
                    tag:
                        "62",

                    value:
                        additionalData);

            /*
             * CRC phải được tính trên toàn bộ payload,
             * bao gồm chính tag "6304", nhưng chưa bao gồm
             * bốn ký tự CRC ở cuối.
             */
            var crcSource =
                payloadWithoutCrc +
                "6304";

            var crc =
                ComputeCrc16CcittFalse(
                    crcSource);

            var completedPayload =
                crcSource +
                crc;

            return Result.Success(
                completedPayload);
        }
        catch (Exception exception)
        {
            /*
             * Không log payload hoặc số tài khoản.
             *
             * OrderCode được phép log vì đây là mã nghiệp vụ
             * dùng để truy vết lỗi và không phải credential.
             */
            _logger.LogError(
                exception,
                "Không thể dựng payload VietQR cho đơn " +
                "{OrderCode}.",
                request.OrderCode);

            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Không thể tạo nội dung VietQR hợp lệ.");
        }
    }

    /// <summary>
    /// Tạo ảnh PNG từ payload VietQR.
    ///
    /// Payload được dựng lại qua BuildPayload để mọi đường
    /// tạo ảnh đều sử dụng cùng validation và cùng CRC.
    /// </summary>
    public Result<byte[]> GeneratePng(
        VietQrRequest request)
    {
        var payloadResult =
            BuildPayload(
                request);

        if (payloadResult.IsFailure)
        {
            return Result.Failure<byte[]>(
                payloadResult.Error);
        }

        try
        {
            /*
             * ECCLevel.Q ưu tiên khả năng phục hồi khi:
             * - QR được hiển thị trên màn hình;
             * - giấy in bị mờ nhẹ;
             * - camera quét ở góc không hoàn hảo.
             *
             * Quiet zone luôn được giữ để camera xác định
             * đúng ranh giới mã QR.
             */
            using var generator =
                new QRCodeGenerator();

            using var qrCodeData =
                generator.CreateQrCode(
                    payloadResult.Value,
                    QRCodeGenerator.ECCLevel.Q);

            using var qrCode =
                new PngByteQRCode(
                    qrCodeData);

            var pngBytes =
                qrCode.GetGraphic(
                    pixelsPerModule:
                        _options
                            .QrPixelsPerModule,

                    drawQuietZones:
                        true);

            if (!HasValidPngSignature(
                    pngBytes))
            {
                _logger.LogError(
                    "QRCoder trả về dữ liệu không có " +
                    "PNG signature cho đơn {OrderCode}.",
                    request.OrderCode);

                return Failure<byte[]>(
                    ErrorCodes.Payments
                        .VietQrGenerationFailed,

                    "Không thể tạo ảnh VietQR hợp lệ.");
            }

            return Result.Success(
                pngBytes);
        }
        catch (Exception exception)
        {
            /*
             * Không ghi payload hoặc số tài khoản vào log.
             */
            _logger.LogError(
                exception,
                "Không thể tạo ảnh VietQR cho đơn " +
                "{OrderCode}.",
                request.OrderCode);

            return Failure<byte[]>(
                ErrorCodes.Payments
                    .VietQrGenerationFailed,

                "Không thể tạo ảnh VietQR.");
        }
    }

    /// <summary>
    /// Tạo nội dung chuyển khoản dạng ASCII viết hoa.
    ///
    /// Ví dụ:
    /// "Cà phê sữa đá - đơn 42"
    /// thành:
    /// "POS CA PHE SUA DA DON 42"
    ///
    /// Việc loại dấu giúp payload ổn định giữa các ứng dụng
    /// ngân hàng và làm chiều dài TLV dễ kiểm soát.
    /// </summary>
    private Result<string> BuildTransferContent(
        VietQrRequest request)
    {
        var normalizedPrefix =
            NormalizeBankText(
                _options
                    .GetNormalizedTransferContentPrefix());

        if (string.IsNullOrWhiteSpace(
                normalizedPrefix))
        {
            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Tiền tố nội dung chuyển khoản không hợp lệ.");
        }

        var contentSource =
            string.IsNullOrWhiteSpace(
                request.TransferContent)
                ? request.OrderCode
                : request.TransferContent;

        var normalizedContent =
            NormalizeBankText(
                contentSource);

        if (string.IsNullOrWhiteSpace(
                normalizedContent))
        {
            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Nội dung chuyển khoản không hợp lệ.");
        }

        /*
         * Không thêm prefix lần thứ hai khi nội dung đã có:
         *
         * POS HD001
         *
         * hoặc đúng bằng:
         *
         * POS
         */
        var alreadyContainsPrefix =
            string.Equals(
                normalizedContent,
                normalizedPrefix,
                StringComparison.Ordinal)

            ||

            normalizedContent.StartsWith(
                normalizedPrefix + " ",
                StringComparison.Ordinal);

        var finalContent =
            alreadyContainsPrefix
                ? normalizedContent
                : $"{normalizedPrefix} " +
                  $"{normalizedContent}";

        if (finalContent.Length >
            MaximumTransferContentLength)
        {
            /*
             * Không tự cắt chuỗi.
             *
             * Cắt âm thầm có thể làm mất phần mã đơn hàng
             * và khiến cửa hàng không đối soát được giao dịch.
             */
            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                $"Nội dung chuyển khoản không được vượt quá " +
                $"{MaximumTransferContentLength} ký tự.");
        }

        return Result.Success(
            finalContent);
    }

    /// <summary>
    /// Chuẩn hóa nội dung thành tập ký tự an toàn:
    /// - A đến Z;
    /// - 0 đến 9;
    /// - khoảng trắng đơn.
    ///
    /// Dấu tiếng Việt được loại bỏ.
    /// Đ và đ được chuyển riêng thành D.
    /// </summary>
    private static string NormalizeBankText(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        var prepared =
            value
                .Trim()
                .Replace(
                    'Đ',
                    'D')
                .Replace(
                    'đ',
                    'd')
                .Normalize(
                    NormalizationForm.FormD);

        var builder =
            new StringBuilder(
                prepared.Length);

        var previousCharacterWasSpace =
            true;

        foreach (var character in prepared)
        {
            var unicodeCategory =
                CharUnicodeInfo
                    .GetUnicodeCategory(
                        character);

            /*
             * FormD tách:
             *
             * ế → e + dấu mũ + dấu sắc.
             *
             * Bỏ các combining mark sẽ giữ lại chữ e.
             */
            if (unicodeCategory is
                UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            var upperCharacter =
                char.ToUpperInvariant(
                    character);

            var isAsciiLetter =
                upperCharacter is
                    >= 'A' and <= 'Z';

            var isAsciiDigit =
                upperCharacter is
                    >= '0' and <= '9';

            if (isAsciiLetter ||
                isAsciiDigit)
            {
                builder.Append(
                    upperCharacter);

                previousCharacterWasSpace =
                    false;

                continue;
            }

            /*
             * Dấu gạch, dấu chấm, dấu gạch chéo,
             * control character và ký tự không hỗ trợ
             * đều được chuyển thành một khoảng trắng.
             */
            if (!previousCharacterWasSpace &&
                builder.Length > 0)
            {
                builder.Append(
                    ' ');

                previousCharacterWasSpace =
                    true;
            }
        }

        return builder
            .ToString()
            .Trim();
    }

    /// <summary>
    /// Dựng một field theo dạng:
    ///
    /// TAG + LENGTH + VALUE
    ///
    /// Ví dụ:
    /// tag 53, value 704:
    ///
    /// 5303704
    /// </summary>
    private static string CreateTlv(
        string tag,
        string value)
    {
        ArgumentNullException.ThrowIfNull(
            tag);

        ArgumentNullException.ThrowIfNull(
            value);

        if (tag.Length != 2 ||
            tag[0] is < '0' or > '9' ||
            tag[1] is < '0' or > '9')
        {
            throw new InvalidOperationException(
                "Tag TLV phải gồm đúng hai chữ số.");
        }

        var valueByteLength =
            Encoding.UTF8.GetByteCount(
                value);

        if (valueByteLength >
            MaximumTlvValueByteLength)
        {
            throw new InvalidOperationException(
                $"Giá trị của tag {tag} vượt quá " +
                $"{MaximumTlvValueByteLength} byte.");
        }

        return
            $"{tag}" +
            $"{valueByteLength.ToString(
                "D2",
                CultureInfo.InvariantCulture)}" +
            $"{value}";
    }

    /// <summary>
    /// Tính CRC16/CCITT-FALSE:
    ///
    /// - polynomial: 0x1021;
    /// - initial value: 0xFFFF;
    /// - không reflect input;
    /// - không reflect output;
    /// - xor output: 0x0000.
    /// </summary>
    private static string
        ComputeCrc16CcittFalse(
            string value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        var crc =
            0xFFFF;

        var bytes =
            Encoding.UTF8.GetBytes(
                value);

        foreach (var currentByte in bytes)
        {
            crc ^=
                currentByte << 8;

            for (var bitIndex = 0;
                 bitIndex < 8;
                 bitIndex++)
            {
                crc =
                    (crc & 0x8000) != 0
                        ? ((crc << 1) ^
                           0x1021) &
                          0xFFFF
                        : (crc << 1) &
                          0xFFFF;
            }
        }

        return crc.ToString(
            "X4",
            CultureInfo.InvariantCulture);
    }

    private static bool HasValidPngSignature(
        byte[]? bytes)
    {
        /*
         * PNG signature:
         *
         * 89 50 4E 47 0D 0A 1A 0A
         */
        return bytes is
               {
                   Length: >= 8
               }

               &&

               bytes[0] == 0x89 &&
               bytes[1] == 0x50 &&
               bytes[2] == 0x4E &&
               bytes[3] == 0x47 &&
               bytes[4] == 0x0D &&
               bytes[5] == 0x0A &&
               bytes[6] == 0x1A &&
               bytes[7] == 0x0A;
    }

    private static Result<TValue> Failure<TValue>(
        string code,
        string message)
    {
        return Result.Failure<TValue>(
            new Error(
                code,
                message));
    }
}