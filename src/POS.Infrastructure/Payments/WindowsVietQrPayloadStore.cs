using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace POS.Infrastructure.Payments;

/// <summary>
/// Lưu payload VietQR bằng Windows DPAPI.
///
/// File chỉ giải mã được bởi tài khoản Windows đã tạo nó.
/// Payload không được ghi vào appsettings, database hoặc log.
/// </summary>
public sealed class WindowsVietQrPayloadStore :
    IVietQrPayloadStore
{
    private const string
        PayloadFileName =
            "vietqr-payload.bin";

    private const int
        MaximumPayloadByteLength =
            4_096;

    private const string
        PayloadFormatPrefix =
            "000201";

    private const string
        VietQrIdentifier =
            "A000000727";

    private static readonly byte[]
        AdditionalEntropy =
            Encoding.UTF8.GetBytes(
                "POS.Enterprise.VietQrPayload.v1");

    private static readonly UTF8Encoding
        StrictUtf8 =
            new(
                encoderShouldEmitUTF8Identifier:
                    false,

                throwOnInvalidBytes:
                    true);

    private readonly object
        _syncRoot =
            new();

    private readonly string
        _payloadFilePath;

    public WindowsVietQrPayloadStore()
        : this(
            CreateDefaultPayloadFilePath())
    {
    }

    /// <summary>
    /// Constructor phục vụ test bằng đường dẫn tạm.
    /// </summary>
    public WindowsVietQrPayloadStore(
        string payloadFilePath)
    {
        if (string.IsNullOrWhiteSpace(
                payloadFilePath))
        {
            throw new ArgumentException(
                "Đường dẫn payload VietQR không được để trống.",
                nameof(payloadFilePath));
        }

        _payloadFilePath =
            Path.GetFullPath(
                payloadFilePath);
    }

    public bool IsConfigured =>
        Load().IsSuccess;

    public Result<string> Load()
    {
        lock (_syncRoot)
        {
            if (!File.Exists(
                    _payloadFilePath))
            {
                return NotConfigured();
            }

            byte[]? plaintextBytes =
                null;

            try
            {
                var protectedBytes =
                    File.ReadAllBytes(
                        _payloadFilePath);

                if (protectedBytes.Length == 0)
                {
                    TryDeleteFile(
                        _payloadFilePath);

                    return NotConfigured();
                }

                plaintextBytes =
                    ProtectedData.Unprotect(
                        protectedBytes,
                        AdditionalEntropy,
                        DataProtectionScope.CurrentUser);

                var payload =
                    StrictUtf8.GetString(
                        plaintextBytes);

                var validation =
                    ValidatePayload(
                        payload);

                if (validation.IsFailure)
                {
                    TryDeleteFile(
                        _payloadFilePath);

                    return Result.Failure<string>(
                        validation.AppError);
                }

                return validation;
            }
            catch (Exception exception)
                when (IsExpectedStorageException(
                    exception))
            {
                TryDeleteFile(
                    _payloadFilePath);

                return Result.Failure<string>(
                    new AppError(
                        ErrorCodes.Payments
                            .VietQrNotConfigured,

                        "Cấu hình VietQR trên máy bị hỏng " +
                        "hoặc không thể giải mã. " +
                        "Hãy tải lại ảnh QR ngân hàng."));
            }
            finally
            {
                if (plaintextBytes is not null)
                {
                    CryptographicOperations
                        .ZeroMemory(
                            plaintextBytes);
                }
            }
        }
    }

    public Result Save(
        string payload)
    {
        var validation =
            ValidatePayload(
                payload);

        if (validation.IsFailure)
        {
            return Result.Failure(
                validation.AppError);
        }

        lock (_syncRoot)
        {
            byte[]? plaintextBytes =
                null;

            var temporaryPath =
                _payloadFilePath +
                ".tmp";

            try
            {
                var directory =
                    Path.GetDirectoryName(
                        _payloadFilePath);

                if (string.IsNullOrWhiteSpace(
                        directory))
                {
                    return StorageFailure(
                        "Không xác định được thư mục lưu VietQR.");
                }

                Directory.CreateDirectory(
                    directory);

                plaintextBytes =
                    StrictUtf8.GetBytes(
                        validation.Value);

                var protectedBytes =
                    ProtectedData.Protect(
                        plaintextBytes,
                        AdditionalEntropy,
                        DataProtectionScope.CurrentUser);

                File.WriteAllBytes(
                    temporaryPath,
                    protectedBytes);

                File.Move(
                    temporaryPath,
                    _payloadFilePath,
                    overwrite:
                        true);

                return Result.Success();
            }
            catch (Exception exception)
                when (IsExpectedStorageException(
                    exception))
            {
                TryDeleteFile(
                    temporaryPath);

                return StorageFailure(
                    "Không thể lưu cấu hình VietQR trên máy.");
            }
            finally
            {
                if (plaintextBytes is not null)
                {
                    CryptographicOperations
                        .ZeroMemory(
                            plaintextBytes);
                }
            }
        }
    }

    public Result Delete()
    {
        lock (_syncRoot)
        {
            var mainDeleted =
                TryDeleteFile(
                    _payloadFilePath);

            var temporaryDeleted =
                TryDeleteFile(
                    _payloadFilePath +
                    ".tmp");

            return
                mainDeleted &&
                temporaryDeleted
                    ? Result.Success()
                    : StorageFailure(
                        "Không thể xóa cấu hình VietQR trên máy.");
        }
    }

    private static Result<string>
        ValidatePayload(
            string? payload)
    {
        if (string.IsNullOrWhiteSpace(
                payload))
        {
            return InvalidPayload(
                "Payload VietQR không được để trống.");
        }

        var normalized =
            payload.Trim();

        byte[] payloadBytes;

        try
        {
            payloadBytes =
                StrictUtf8.GetBytes(
                    normalized);
        }
        catch (EncoderFallbackException)
        {
            return InvalidPayload(
                "Payload VietQR chứa ký tự không hợp lệ.");
        }

        if (payloadBytes.Length >
            MaximumPayloadByteLength)
        {
            return InvalidPayload(
                $"Payload VietQR không được vượt quá " +
                $"{MaximumPayloadByteLength:N0} byte.");
        }

        if (!normalized.StartsWith(
                PayloadFormatPrefix,
                StringComparison.Ordinal))
        {
            return InvalidPayload(
                "Ảnh không chứa payload VietQR chuẩn.");
        }

        if (!normalized.Contains(
                VietQrIdentifier,
                StringComparison.Ordinal))
        {
            return InvalidPayload(
                "Payload không chứa định danh VietQR.");
        }

        var fieldsResult =
            TryReadTlvCollection(
                payloadBytes);

        if (fieldsResult.IsFailure)
        {
            return Result.Failure<string>(
                fieldsResult.AppError);
        }

        var fields =
            fieldsResult.Value;

        if (fields.Count == 0)
        {
            return InvalidPayload(
                "Payload VietQR không có dữ liệu TLV.");
        }

        var crcField =
            fields[^1];

        if (!string.Equals(
                crcField.Tag,
                "63",
                StringComparison.Ordinal) ||
            crcField.RawValue.Length != 4)
        {
            return InvalidPayload(
                "Payload VietQR không có CRC hợp lệ ở cuối.");
        }

        if (!ContainsOnlyHexCharacters(
                crcField.Value))
        {
            return InvalidPayload(
                "CRC VietQR không hợp lệ.");
        }

        var crcSourceLength =
            payloadBytes.Length -
            crcField.RawValue.Length;

        var actualCrc =
            ComputeCrc16CcittFalse(
                payloadBytes.AsSpan(
                    0,
                    crcSourceLength));

        if (!string.Equals(
                actualCrc,
                crcField.Value,
                StringComparison.OrdinalIgnoreCase))
        {
            return InvalidPayload(
                "CRC của ảnh VietQR không hợp lệ. " +
                "Ảnh có thể bị hỏng hoặc đã bị chỉnh sửa.");
        }

        return Result.Success(
            normalized);
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
            if (bytes.Length - index <
                4)
            {
                return InvalidFields(
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
                return InvalidFields(
                    "Payload VietQR có tag hoặc độ dài TLV " +
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
                return InvalidFields(
                    $"Payload VietQR bị thiếu dữ liệu " +
                    $"ở tag {tag}.");
            }

            var rawValue =
                bytes
                    .AsSpan(
                        valueStart,
                        valueLength)
                    .ToArray();

            string value;

            try
            {
                value =
                    StrictUtf8.GetString(
                        rawValue);
            }
            catch (DecoderFallbackException)
            {
                return InvalidFields(
                    $"Tag {tag} chứa UTF-8 không hợp lệ.");
            }

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

    private static bool ContainsOnlyHexCharacters(
        string value)
    {
        return value.Length == 4 &&
               value.All(
                   Uri.IsHexDigit);
    }

    private static bool IsAsciiDigit(
        byte value)
    {
        return value is
            >= (byte)'0' and <= (byte)'9';
    }

    private static bool TryDeleteFile(
        string filePath)
    {
        try
        {
            if (File.Exists(
                    filePath))
            {
                File.Delete(
                    filePath);
            }

            return true;
        }
        catch (Exception exception)
            when (IsExpectedStorageException(
                exception))
        {
            return false;
        }
    }

    private static bool
        IsExpectedStorageException(
            Exception exception)
    {
        return exception is
            IOException or
            UnauthorizedAccessException or
            CryptographicException or
            DecoderFallbackException or
            EncoderFallbackException or
            NotSupportedException or
            ArgumentException;
    }

    private static string
        CreateDefaultPayloadFilePath()
    {
        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .LocalApplicationData);

        return Path.Combine(
            localApplicationData,
            "POS Enterprise",
            "Payments",
            PayloadFileName);
    }

    private static Result<string>
        NotConfigured()
    {
        return Result.Failure<string>(
            new AppError(
                ErrorCodes.Payments
                    .VietQrNotConfigured,

                "Cửa hàng chưa lưu ảnh QR ngân hàng."));
    }

    private static Result<string>
        InvalidPayload(
            string message)
    {
        return Result.Failure<string>(
            new AppError(
                ErrorCodes.Payments
                    .VietQrInvalidPayload,

                message));
    }

    private static Result<IReadOnlyList<TlvField>>
        InvalidFields(
            string message)
    {
        return Result.Failure<
            IReadOnlyList<TlvField>>(
                new AppError(
                    ErrorCodes.Payments
                        .VietQrInvalidPayload,

                    message));
    }

    private static Result StorageFailure(
        string message)
    {
        return Result.Failure(
            new AppError(
                ErrorCodes.Payments
                    .VietQrGenerationFailed,

                message));
    }

    private sealed record TlvField(
        string Tag,
        string Value,
        byte[] RawValue);
}