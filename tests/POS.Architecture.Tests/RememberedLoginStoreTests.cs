using System.Text;
using POS.Application.DTOs.Authentication;
using POS.Infrastructure.Authentication;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm tra credential ghi nhớ được:
/// - lưu và đọc đúng;
/// - không để fingerprint dạng rõ trong file;
/// - tự loại bỏ file hỏng;
/// - xóa thành công khi đăng xuất.
/// </summary>
public sealed class RememberedLoginStoreTests :
    IDisposable
{
    private readonly string
        _temporaryDirectory;

    private readonly string
        _credentialPath;

    public RememberedLoginStoreTests()
    {
        _temporaryDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "POS-Enterprise-Tests",
                Guid.NewGuid()
                    .ToString("N"));

        _credentialPath =
            Path.Combine(
                _temporaryDirectory,
                "remembered-login.bin");
    }

    [Fact]
    public void Save_load_and_delete_must_roundtrip()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store =
            new WindowsRememberedLoginStore(
                _credentialPath);

        var credential =
            CreateCredential();

        Assert.True(
            store.TrySave(
                credential));

        Assert.True(
            File.Exists(
                _credentialPath));

        var loaded =
            store.Load();

        Assert.NotNull(
            loaded);

        Assert.Equal(
            credential,
            loaded);

        Assert.True(
            store.TryDelete());

        Assert.False(
            File.Exists(
                _credentialPath));

        Assert.Null(
            store.Load());
    }

    [Fact]
    public void Protected_file_must_not_contain_plain_fingerprint()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store =
            new WindowsRememberedLoginStore(
                _credentialPath);

        var credential =
            CreateCredential();

        Assert.True(
            store.TrySave(
                credential));

        var protectedBytes =
            File.ReadAllBytes(
                _credentialPath);

        var interpretedAsText =
            Encoding.UTF8.GetString(
                protectedBytes);

        Assert.DoesNotContain(
            credential.PasswordHashFingerprint,
            interpretedAsText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Corrupted_file_must_not_restore_session()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(
            _temporaryDirectory);

        File.WriteAllText(
            _credentialPath,
            "not-a-valid-protected-credential");

        var store =
            new WindowsRememberedLoginStore(
                _credentialPath);

        var loaded =
            store.Load();

        Assert.Null(
            loaded);

        Assert.False(
            File.Exists(
                _credentialPath));
    }

    private static RememberedLoginCredential
        CreateCredential()
    {
        return new RememberedLoginCredential(
            Version:
                RememberedLoginCredential
                    .CurrentVersion,

            UserId:
                15,

            PasswordHashFingerprint:
                new string(
                    'A',
                    64),

            ExpiresAtUtc:
                new DateTimeOffset(
                    2026,
                    8,
                    20,
                    0,
                    0,
                    0,
                    TimeSpan.Zero));
    }

    public void Dispose()
    {
        if (Directory.Exists(
                _temporaryDirectory))
        {
            Directory.Delete(
                _temporaryDirectory,
                recursive:
                    true);
        }

        GC.SuppressFinalize(
            this);
    }
}