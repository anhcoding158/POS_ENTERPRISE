using POS.Application.Abstractions.Authentication;
using POS.Application.DTOs.Authentication;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace POS.Infrastructure.Authentication;

/// <summary>
/// Lưu credential đăng nhập bằng Windows DPAPI.
///
/// Dữ liệu chỉ có thể được giải mã bởi chính tài khoản
/// Windows đã tạo file credential.
/// </summary>
public sealed class WindowsRememberedLoginStore :
    IRememberedLoginStore
{
    private const string
        CredentialFileName =
            "remembered-login.bin";

    private static readonly byte[]
        AdditionalEntropy =
            Encoding.UTF8.GetBytes(
                "POS.Enterprise.RememberedLogin.v1");

    private static readonly
        JsonSerializerOptions
        SerializerOptions =
            new()
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase
            };

    private readonly string
        _credentialFilePath;

    public WindowsRememberedLoginStore()
        : this(
            CreateDefaultCredentialFilePath())
    {
    }

    /// <summary>
    /// Constructor này cho phép test dùng một thư mục tạm,
    /// không can thiệp credential thật của ứng dụng.
    /// </summary>
    public WindowsRememberedLoginStore(
        string credentialFilePath)
    {
        if (string.IsNullOrWhiteSpace(
                credentialFilePath))
        {
            throw new ArgumentException(
                "Đường dẫn credential không được để trống.",
                nameof(credentialFilePath));
        }

        _credentialFilePath =
            Path.GetFullPath(
                credentialFilePath);
    }

    public RememberedLoginCredential?
        Load()
    {
        if (!File.Exists(
                _credentialFilePath))
        {
            return null;
        }

        byte[]? plaintextBytes =
            null;

        try
        {
            var protectedBytes =
                File.ReadAllBytes(
                    _credentialFilePath);

            if (protectedBytes.Length == 0)
            {
                TryDelete();

                return null;
            }

            plaintextBytes =
                ProtectedData.Unprotect(
                    protectedBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser);

            var json =
                Encoding.UTF8.GetString(
                    plaintextBytes);

            var credential =
                JsonSerializer.Deserialize<
                    RememberedLoginCredential>(
                        json,
                        SerializerOptions);

            if (!IsValidCredential(
                    credential))
            {
                TryDelete();

                return null;
            }

            return credential;
        }
        catch (Exception exception)
            when (IsExpectedStorageException(
                exception))
        {
            /*
             * File hỏng, bị sửa hoặc được sao chép từ
             * Windows account khác không được phép làm
             * ứng dụng ngừng khởi động.
             */
            TryDelete();

            return null;
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

    public bool TrySave(
        RememberedLoginCredential credential)
    {
        ArgumentNullException.ThrowIfNull(
            credential);

        if (!IsValidCredential(
                credential))
        {
            return false;
        }

        byte[]? plaintextBytes =
            null;

        var temporaryPath =
            _credentialFilePath +
            ".tmp";

        try
        {
            var directory =
                Path.GetDirectoryName(
                    _credentialFilePath);

            if (string.IsNullOrWhiteSpace(
                    directory))
            {
                return false;
            }

            Directory.CreateDirectory(
                directory);

            var json =
                JsonSerializer.Serialize(
                    credential,
                    SerializerOptions);

            plaintextBytes =
                Encoding.UTF8.GetBytes(
                    json);

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
                _credentialFilePath,
                overwrite:
                    true);

            return true;
        }
        catch (Exception exception)
            when (IsExpectedStorageException(
                exception))
        {
            TryDeleteFile(
                temporaryPath);

            return false;
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

    public bool TryDelete()
    {
        var mainDeleted =
            TryDeleteFile(
                _credentialFilePath);

        var temporaryDeleted =
            TryDeleteFile(
                _credentialFilePath +
                ".tmp");

        return
            mainDeleted &&
            temporaryDeleted;
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

    private static bool IsValidCredential(
        RememberedLoginCredential?
            credential)
    {
        if (credential is null)
        {
            return false;
        }

        if (credential.Version !=
            RememberedLoginCredential
                .CurrentVersion)
        {
            return false;
        }

        if (credential.UserId <= 0)
        {
            return false;
        }

        if (credential.ExpiresAtUtc ==
            default)
        {
            return false;
        }

        var fingerprint =
            credential
                .PasswordHashFingerprint;

        if (string.IsNullOrWhiteSpace(
                fingerprint) ||
            fingerprint.Length != 64)
        {
            return false;
        }

        return fingerprint.All(
            Uri.IsHexDigit);
    }

    private static bool
        IsExpectedStorageException(
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

    private static string
        CreateDefaultCredentialFilePath()
    {
        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .LocalApplicationData);

        return Path.Combine(
            localApplicationData,
            "POS Enterprise",
            "Security",
            CredentialFileName);
    }
}