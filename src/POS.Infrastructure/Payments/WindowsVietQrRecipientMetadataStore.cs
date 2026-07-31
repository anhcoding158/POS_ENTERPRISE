using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace POS.Infrastructure.Payments;

public sealed class WindowsVietQrRecipientMetadataStore :
    IVietQrRecipientMetadataStore
{
    private const string
        MetadataFileName =
            "vietqr-recipient-metadata.bin";

    private const int
        CurrentVersion =
            1;

    private const string
        InvalidMetadataCode =
            "Payments.VietQrRecipientMetadataInvalid";

    private const string
        StorageFailedCode =
            "Payments.VietQrRecipientMetadataStorageFailed";

    private static readonly byte[]
        AdditionalEntropy =
            Encoding.UTF8.GetBytes(
                "POS.Enterprise.VietQrRecipientMetadata.v1");

    private readonly object
        _syncRoot =
            new();

    private readonly string
        _metadataFilePath;

    public WindowsVietQrRecipientMetadataStore()
        : this(
            CreateDefaultMetadataFilePath())
    {
    }

    public WindowsVietQrRecipientMetadataStore(
        string filePath)
    {
        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "Đường dẫn metadata VietQR không được để trống.",
                nameof(filePath));
        }

        _metadataFilePath =
            Path.GetFullPath(
                filePath);
    }

    public bool IsConfigured =>
        Load().IsSuccess;

    public Result<VietQrRecipientMetadata> Load()
    {
        lock (_syncRoot)
        {
            if (!File.Exists(
                    _metadataFilePath))
            {
                return NotConfigured();
            }

            byte[]? plaintextBytes =
                null;

            try
            {
                var protectedBytes =
                    File.ReadAllBytes(
                        _metadataFilePath);

                if (protectedBytes.Length == 0)
                {
                    return InvalidMetadata(
                        "Metadata người nhận VietQR bị trống.");
                }

                plaintextBytes =
                    ProtectedData.Unprotect(
                        protectedBytes,
                        AdditionalEntropy,
                        DataProtectionScope.CurrentUser);

                var envelope =
                    JsonSerializer.Deserialize<MetadataEnvelope>(
                        plaintextBytes);

                if (envelope is null ||
                    envelope.Version !=
                    CurrentVersion)
                {
                    return InvalidMetadata(
                        "Metadata người nhận VietQR không đúng phiên bản.");
                }

                return Result.Success(
                    new VietQrRecipientMetadata(
                        envelope.BankName,
                        envelope.AccountName));
            }
            catch (Exception exception)
                when (IsExpectedLoadException(
                    exception))
            {
                return InvalidMetadata(
                    "Metadata người nhận VietQR bị hỏng hoặc không thể giải mã.");
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
        VietQrRecipientMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(
            metadata);

        lock (_syncRoot)
        {
            byte[]? plaintextBytes =
                null;

            byte[]? protectedBytes =
                null;

            var temporaryPath =
                _metadataFilePath +
                ".tmp";

            try
            {
                var directory =
                    Path.GetDirectoryName(
                        _metadataFilePath);

                if (string.IsNullOrWhiteSpace(
                        directory))
                {
                    return StorageFailure(
                        "Không xác định được thư mục lưu metadata VietQR.");
                }

                Directory.CreateDirectory(
                    directory);

                plaintextBytes =
                    JsonSerializer.SerializeToUtf8Bytes(
                        new MetadataEnvelope(
                            CurrentVersion,
                            metadata.BankName,
                            metadata.AccountName));

                protectedBytes =
                    ProtectedData.Protect(
                        plaintextBytes,
                        AdditionalEntropy,
                        DataProtectionScope.CurrentUser);

                File.WriteAllBytes(
                    temporaryPath,
                    protectedBytes);

                File.Move(
                    temporaryPath,
                    _metadataFilePath,
                    overwrite:
                        true);

                return Result.Success();
            }
            catch (Exception exception)
                when (IsExpectedSaveException(
                    exception))
            {
                return StorageFailure(
                    "Không thể lưu metadata người nhận VietQR trên máy.");
            }
            finally
            {
                TryDeleteTemporaryFile(
                    temporaryPath);

                if (plaintextBytes is not null)
                {
                    CryptographicOperations
                        .ZeroMemory(
                            plaintextBytes);
                }

                if (protectedBytes is not null)
                {
                    CryptographicOperations
                        .ZeroMemory(
                            protectedBytes);
                }
            }
        }
    }

    public Result Delete()
    {
        lock (_syncRoot)
        {
            try
            {
                if (File.Exists(
                        _metadataFilePath))
                {
                    File.Delete(
                        _metadataFilePath);
                }

                return Result.Success();
            }
            catch (IOException)
            {
                return StorageFailure(
                    "Không thể xóa metadata người nhận VietQR trên máy.");
            }
            catch (UnauthorizedAccessException)
            {
                return StorageFailure(
                    "Không thể xóa metadata người nhận VietQR trên máy.");
            }
        }
    }

    private static string
        CreateDefaultMetadataFilePath()
    {
        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .LocalApplicationData);

        return Path.Combine(
            localApplicationData,
            "POS Enterprise",
            "Payments",
            MetadataFileName);
    }

    private static bool
        IsExpectedLoadException(
            Exception exception)
    {
        return exception is
            IOException or
            UnauthorizedAccessException or
            CryptographicException or
            JsonException or
            NotSupportedException or
            ArgumentException;
    }

    private static bool
        IsExpectedSaveException(
            Exception exception)
    {
        return exception is
            IOException or
            UnauthorizedAccessException or
            CryptographicException or
            NotSupportedException or
            ArgumentException;
    }

    private static void TryDeleteTemporaryFile(
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
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static Result<VietQrRecipientMetadata>
        NotConfigured()
    {
        return Result.Failure<
            VietQrRecipientMetadata>(
                new AppError(
                    ErrorCodes.Payments
                        .VietQrNotConfigured,

                    "Cửa hàng chưa lưu metadata người nhận VietQR."));
    }

    private static Result<VietQrRecipientMetadata>
        InvalidMetadata(
            string message)
    {
        return Result.Failure<
            VietQrRecipientMetadata>(
                new AppError(
                    InvalidMetadataCode,
                    message));
    }

    private static Result StorageFailure(
        string message)
    {
        return Result.Failure(
            new AppError(
                StorageFailedCode,
                message));
    }

    private sealed record MetadataEnvelope(
        int Version,
        string BankName,
        string AccountName);
}
