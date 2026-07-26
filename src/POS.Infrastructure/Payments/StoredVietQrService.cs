using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using POS.Application.DTOs.Payments;
using POS.Domain.Constants;
using QRCoder;
using System.Globalization;
using System.Text;

namespace POS.Infrastructure.Payments;

/// <summary>
/// Tạo QR thanh toán từ payload nền đã đọc từ ảnh ngân hàng.
///
/// Service:
/// - không yêu cầu nhập thủ công thông tin tài khoản;
/// - giữ nguyên Merchant Account Information của QR gốc;
/// - thay Point of Initiation Method thành QR động;
/// - bỏ số tiền cũ;
/// - bỏ nội dung chuyển khoản cũ;
/// - thêm số tiền và nội dung của đơn hiện tại;
/// - tính lại CRC;
/// - tạo PNG bằng QRCoder.
/// </summary>
public sealed class StoredVietQrService
{
    private const int
        MaximumPayloadByteLength =
            4_096;

    private const int
        MaximumTlvValueByteLength =
            99;

    private const int
        MaximumTransferContentLength =
            50;

    private const string
        DynamicPointOfInitiationMethod =
            "12";

    private const string
        VietQrIdentifier =
            "A000000727";

    private const string
        VietnameseDongCurrencyCode =
            "704";

    private const string
        VietnamCountryCode =
            "VN";

    private const string
        FallbackTransferContentPrefix =
            "POS";

    private static readonly UTF8Encoding
        StrictUtf8 =
            new(
                encoderShouldEmitUTF8Identifier:
                    false,

                throwOnInvalidBytes:
                    true);

    private readonly IVietQrPayloadStore
        _payloadStore;

    private readonly VietQrOptions
        _options;

    private readonly ILogger<StoredVietQrService>
        _logger;

    public StoredVietQrService(
        IVietQrPayloadStore payloadStore,
        IOptions<VietQrOptions> options,
        ILogger<StoredVietQrService> logger)
    {
        _payloadStore =
            payloadStore ??
            throw new ArgumentNullException(
                nameof(payloadStore));

        ArgumentNullException.ThrowIfNull(
            options);

        _options =
            options.Value ??
            throw new ArgumentException(
                "Không đọc được cấu hình hiển thị VietQR.",
                nameof(options));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        _options.Validate();
    }

    public bool IsConfigured =>
        _payloadStore.IsConfigured;

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

                "Mã tham chiếu VietQR không được để trống.");
        }

        var loadResult =
            _payloadStore.Load();

        if (loadResult.IsFailure)
        {
            return Result.Failure<string>(
                loadResult.Error);
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
            var payloadBytes =
                StrictUtf8.GetBytes(
                    loadResult.Value);

            var fieldsResult =
                TryReadTlvCollection(
                    payloadBytes,
                    "payload VietQR nền");

            if (fieldsResult.IsFailure)
            {
                return Result.Failure<string>(
                    fieldsResult.Error);
            }

            var fields =
                fieldsResult.Value;

            var validation =
                ValidateBasePayload(
                    payloadBytes,
                    fields);

            if (validation.IsFailure)
            {
                return Result.Failure<string>(
                    validation.Error);
            }

            var additionalFieldResult =
                GetOptionalField(
                    fields,
                    "62",
                    "Additional Data");

            if (additionalFieldResult.IsFailure)
            {
                return Result.Failure<string>(
                    additionalFieldResult.Error);
            }

            var additionalDataResult =
                BuildAdditionalData(
                    additionalFieldResult
                        .Value
                        .Field,

                    transferContentResult.Value);

            if (additionalDataResult.IsFailure)
            {
                return Result.Failure<string>(
                    additionalDataResult.Error);
            }

            /*
             * Giữ nguyên toàn bộ field của QR ngân hàng,
             * ngoại trừ các field thay đổi theo từng đơn.
             */
            var outputFields =
                fields
                    .Where(
                        field =>
                            !string.Equals(
                                field.Tag,
                                "01",
                                StringComparison.Ordinal) &&
                            !string.Equals(
                                field.Tag,
                                "54",
                                StringComparison.Ordinal) &&
                            !string.Equals(
                                field.Tag,
                                "62",
                                StringComparison.Ordinal) &&
                            !string.Equals(
                                field.Tag,
                                "63",
                                StringComparison.Ordinal))
                    .ToList();

            outputFields.Add(
                CreateField(
                    "01",
                    DynamicPointOfInitiationMethod));

            outputFields.Add(
                CreateField(
                    "54",
                    request.Amount.ToString(
                        CultureInfo.InvariantCulture)));

            outputFields.Add(
                CreateField(
                    "62",
                    additionalDataResult.Value));

            var orderedFields =
                outputFields
                    .OrderBy(
                        field =>
                            int.Parse(
                                field.Tag,
                                NumberStyles.None,
                                CultureInfo.InvariantCulture))
                    .ToArray();

            var payloadWithoutCrc =
                string.Concat(
                    orderedFields.Select(
                        field =>
                            CreateTlv(
                                field.Tag,
                                field.Value)));

            var crcSource =
                payloadWithoutCrc +
                "6304";

            var completedPayload =
                crcSource +
                ComputeCrc16CcittFalse(
                    StrictUtf8.GetBytes(
                        crcSource));

            if (StrictUtf8.GetByteCount(
                    completedPayload) >
                MaximumPayloadByteLength)
            {
                return Failure<string>(
                    ErrorCodes.Payments
                        .VietQrInvalidPayload,

                    "Payload VietQR sau khi thêm thông tin đơn " +
                    "vượt quá giới hạn cho phép.");
            }

            return Result.Success(
                completedPayload);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể dựng VietQR từ payload đã lưu " +
                "cho mã tham chiếu {OrderCode}.",
                request.OrderCode);

            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Không thể tạo VietQR từ ảnh ngân hàng đã lưu.");
        }
    }

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
            _logger.LogError(
                exception,
                "Không thể tạo PNG VietQR cho mã " +
                "tham chiếu {OrderCode}.",
                request.OrderCode);

            return Failure<byte[]>(
                ErrorCodes.Payments
                    .VietQrGenerationFailed,

                "Không thể tạo ảnh VietQR.");
        }
    }

    private Result<string> BuildTransferContent(
        VietQrRequest request)
    {
        var configuredPrefix =
            _options
                .TransferContentPrefix;

        var normalizedPrefix =
            NormalizeBankText(
                string.IsNullOrWhiteSpace(
                    configuredPrefix)
                    ? FallbackTransferContentPrefix
                    : configuredPrefix);

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

        var alreadyContainsPrefix =
            string.Equals(
                normalizedContent,
                normalizedPrefix,
                StringComparison.Ordinal) ||

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
            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                $"Nội dung chuyển khoản không được vượt quá " +
                $"{MaximumTransferContentLength} ký tự.");
        }

        return Result.Success(
            finalContent);
    }

    private static Result<string>
        BuildAdditionalData(
            TlvField? existingField,
            string transferContent)
    {
        var nestedFields =
            new List<TlvField>();

        if (existingField is not null)
        {
            var nestedResult =
                TryReadTlvCollection(
                    existingField.RawValue,
                    "Additional Data của QR nền");

            if (nestedResult.IsFailure)
            {
                return Result.Failure<string>(
                    nestedResult.Error);
            }

            nestedFields.AddRange(
                nestedResult.Value);
        }

        var duplicateTransferContent =
            nestedFields.Count(
                field =>
                    string.Equals(
                        field.Tag,
                        "08",
                        StringComparison.Ordinal));

        if (duplicateTransferContent > 1)
        {
            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "QR nền có nhiều nội dung chuyển khoản tag 08.");
        }

        nestedFields.RemoveAll(
            field =>
                string.Equals(
                    field.Tag,
                    "08",
                    StringComparison.Ordinal));

        nestedFields.Add(
            CreateField(
                "08",
                transferContent));

        var additionalData =
            string.Concat(
                nestedFields
                    .OrderBy(
                        field =>
                            int.Parse(
                                field.Tag,
                                NumberStyles.None,
                                CultureInfo.InvariantCulture))
                    .Select(
                        field =>
                            CreateTlv(
                                field.Tag,
                                field.Value)));

        if (StrictUtf8.GetByteCount(
                additionalData) >
            MaximumTlvValueByteLength)
        {
            return Failure<string>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Additional Data của VietQR vượt quá 99 byte.");
        }

        return Result.Success(
            additionalData);
    }

    private static Result ValidateBasePayload(
        byte[] payloadBytes,
        IReadOnlyList<TlvField> fields)
    {
        var formatResult =
            GetRequiredField(
                fields,
                "00",
                "Payload Format Indicator");

        if (formatResult.IsFailure)
        {
            return Result.Failure(
                formatResult.Error);
        }

        if (!string.Equals(
                formatResult.Value.Value,
                "01",
                StringComparison.Ordinal))
        {
            return Failure(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Phiên bản payload VietQR không được hỗ trợ.");
        }

        var currencyResult =
            GetRequiredField(
                fields,
                "53",
                "Transaction Currency");

        if (currencyResult.IsFailure)
        {
            return Result.Failure(
                currencyResult.Error);
        }

        if (!string.Equals(
                currencyResult.Value.Value,
                VietnameseDongCurrencyCode,
                StringComparison.Ordinal))
        {
            return Failure(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Payload VietQR không sử dụng tiền tệ VND.");
        }

        var countryResult =
            GetRequiredField(
                fields,
                "58",
                "Country Code");

        if (countryResult.IsFailure)
        {
            return Result.Failure(
                countryResult.Error);
        }

        if (!string.Equals(
                countryResult.Value.Value,
                VietnamCountryCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Country Code của VietQR phải là VN.");
        }

        var hasVietQrMerchantAccount =
            false;

        foreach (var field in fields)
        {
            if (!IsMerchantAccountTag(
                    field.Tag))
            {
                continue;
            }

            var nestedResult =
                TryReadTlvCollection(
                    field.RawValue,
                    $"Merchant Account tag {field.Tag}");

            if (nestedResult.IsFailure)
            {
                continue;
            }

            hasVietQrMerchantAccount =
                nestedResult.Value.Any(
                    nestedField =>
                        nestedField.Tag == "00" &&
                        nestedField.Value ==
                        VietQrIdentifier);

            if (hasVietQrMerchantAccount)
            {
                break;
            }
        }

        if (!hasVietQrMerchantAccount)
        {
            return Failure(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "Không tìm thấy thông tin VietQR trong QR nền.");
        }

        var crcResult =
            GetRequiredField(
                fields,
                "63",
                "CRC");

        if (crcResult.IsFailure)
        {
            return Result.Failure(
                crcResult.Error);
        }

        if (!ReferenceEquals(
                fields[^1],
                crcResult.Value) ||
            crcResult.Value.RawValue.Length != 4)
        {
            return Failure(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "CRC tag 63 phải nằm ở cuối payload.");
        }

        var crcSourceLength =
            payloadBytes.Length -
            crcResult.Value.RawValue.Length;

        var actualCrc =
            ComputeCrc16CcittFalse(
                payloadBytes.AsSpan(
                    0,
                    crcSourceLength));

        if (!string.Equals(
                actualCrc,
                crcResult.Value.Value,
                StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                "CRC của QR nền không hợp lệ.");
        }

        return Result.Success();
    }

    private static Result<IReadOnlyList<TlvField>>
        TryReadTlvCollection(
            byte[] bytes,
            string context)
    {
        var fields =
            new List<TlvField>();

        var index =
            0;

        while (index <
               bytes.Length)
        {
            if (bytes.Length - index <
                4)
            {
                return Failure<
                    IReadOnlyList<TlvField>>(
                        ErrorCodes.Payments
                            .VietQrInvalidPayload,

                        $"{context} có TLV không hoàn chỉnh.");
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
                        ErrorCodes.Payments
                            .VietQrInvalidPayload,

                        $"{context} có tag hoặc độ dài " +
                        "không hợp lệ.");
            }

            var tag =
                Encoding.ASCII.GetString(
                    bytes,
                    index,
                    2);

            var valueLength =
                ((bytes[index + 2] -
                  (byte)'0') * 10) +
                (bytes[index + 3] -
                 (byte)'0');

            var valueStart =
                index + 4;

            if (valueStart + valueLength >
                bytes.Length)
            {
                return Failure<
                    IReadOnlyList<TlvField>>(
                        ErrorCodes.Payments
                            .VietQrInvalidPayload,

                        $"{context} bị thiếu dữ liệu tag {tag}.");
            }

            var rawValue =
                bytes
                    .AsSpan(
                        valueStart,
                        valueLength)
                    .ToArray();

            var value =
                StrictUtf8.GetString(
                    rawValue);

            fields.Add(
                new TlvField(
                    tag,
                    value,
                    rawValue));

            index =
                valueStart +
                valueLength;
        }

        return Result.Success<
            IReadOnlyList<TlvField>>(
                fields);
    }

    private static Result<TlvField>
        GetRequiredField(
            IReadOnlyList<TlvField> fields,
            string tag,
            string fieldName)
    {
        var matches =
            fields
                .Where(
                    field =>
                        field.Tag == tag)
                .ToArray();

        if (matches.Length == 0)
        {
            return Failure<TlvField>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                $"Không tìm thấy {fieldName} tag {tag}.");
        }

        if (matches.Length > 1)
        {
            return Failure<TlvField>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                $"{fieldName} tag {tag} bị lặp.");
        }

        return Result.Success(
            matches[0]);
    }

    private static Result<OptionalTlvField>
        GetOptionalField(
            IReadOnlyList<TlvField> fields,
            string tag,
            string fieldName)
    {
        var matches =
            fields
                .Where(
                    field =>
                        field.Tag == tag)
                .ToArray();

        if (matches.Length > 1)
        {
            return Failure<OptionalTlvField>(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                $"{fieldName} tag {tag} bị lặp.");
        }

        return Result.Success(
            new OptionalTlvField(
                matches.SingleOrDefault()));
    }

    private static TlvField CreateField(
        string tag,
        string value)
    {
        return new TlvField(
            tag,
            value,
            StrictUtf8.GetBytes(
                value));
    }

    private static string CreateTlv(
        string tag,
        string value)
    {
        if (tag.Length != 2 ||
            !tag.All(
                char.IsAsciiDigit))
        {
            throw new InvalidOperationException(
                "Tag TLV phải gồm hai chữ số.");
        }

        var byteLength =
            StrictUtf8.GetByteCount(
                value);

        if (byteLength >
            MaximumTlvValueByteLength)
        {
            throw new InvalidOperationException(
                $"Giá trị tag {tag} vượt quá 99 byte.");
        }

        return
            $"{tag}" +
            $"{byteLength.ToString(
                "D2",
                CultureInfo.InvariantCulture)}" +
            $"{value}";
    }

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

        var previousWasSpace =
            true;

        foreach (var character in prepared)
        {
            var category =
                CharUnicodeInfo
                    .GetUnicodeCategory(
                        character);

            if (category is
                UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            var upper =
                char.ToUpperInvariant(
                    character);

            if (upper is
                    >= 'A' and <= 'Z' ||
                upper is
                    >= '0' and <= '9')
            {
                builder.Append(
                    upper);

                previousWasSpace =
                    false;

                continue;
            }

            if (!previousWasSpace &&
                builder.Length > 0)
            {
                builder.Append(
                    ' ');

                previousWasSpace =
                    true;
            }
        }

        return builder
            .ToString()
            .Trim();
    }

    private static string
        ComputeCrc16CcittFalse(
            ReadOnlySpan<byte> bytes)
    {
        var crc =
            0xFFFF;

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

    private static bool IsMerchantAccountTag(
        string tag)
    {
        return int.TryParse(
                   tag,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out var number) &&
               number is
                   >= 26 and <= 51;
    }

    private static bool IsAsciiDigit(
        byte value)
    {
        return value is
            >= (byte)'0' and <= (byte)'9';
    }

    private static bool HasValidPngSignature(
        byte[]? bytes)
    {
        return bytes is
            [
                0x89,
                0x50,
                0x4E,
                0x47,
                0x0D,
                0x0A,
                0x1A,
                0x0A,
                ..
            ];
    }

    private static Result Failure(
        string code,
        string message)
    {
        return Result.Failure(
            new Error(
                code,
                message));
    }

    private static Result<TValue>
        Failure<TValue>(
            string code,
            string message)
    {
        return Result.Failure<TValue>(
            new Error(
                code,
                message));
    }

    private sealed record TlvField(
        string Tag,
        string Value,
        byte[] RawValue);

    private sealed record OptionalTlvField(
        TlvField? Field);
}