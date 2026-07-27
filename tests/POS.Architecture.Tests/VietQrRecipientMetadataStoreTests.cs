using POS.Application.Abstractions.Payments;
using POS.Infrastructure.Payments;
using System.Text;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class VietQrRecipientMetadataStoreTests
{
    [Fact]
    public void
        Store_must_save_load_and_delete_metadata()
    {
        var testLocation =
            CreateTestLocation();

        try
        {
            var store =
                new WindowsVietQrRecipientMetadataStore(
                    testLocation.FilePath);

            var saveResult =
                store.Save(
                    new VietQrRecipientMetadata(
                        "Ngân hàng Quân đội",
                        "Nguyễn Văn An"));

            Assert.True(
                saveResult.IsSuccess,
                saveResult.Error.Message);

            Assert.True(
                store.IsConfigured);

            var loadResult =
                store.Load();

            Assert.True(
                loadResult.IsSuccess,
                loadResult.Error.Message);

            Assert.Equal(
                "Ngân hàng Quân đội",
                loadResult.Value.BankName);

            Assert.Equal(
                "Nguyễn Văn An",
                loadResult.Value.AccountName);

            var deleteResult =
                store.Delete();

            Assert.True(
                deleteResult.IsSuccess,
                deleteResult.Error.Message);

            Assert.False(
                store.IsConfigured);
        }
        finally
        {
            DeleteTestDirectory(
                testLocation.Directory);
        }
    }

    [Fact]
    public void
        Store_must_trim_metadata_values()
    {
        var testLocation =
            CreateTestLocation();

        try
        {
            var store =
                new WindowsVietQrRecipientMetadataStore(
                    testLocation.FilePath);

            var saveResult =
                store.Save(
                    new VietQrRecipientMetadata(
                        "  Vietcombank  ",
                        "  Trần Thị Bình  "));

            Assert.True(
                saveResult.IsSuccess,
                saveResult.Error.Message);

            var loadResult =
                store.Load();

            Assert.True(
                loadResult.IsSuccess,
                loadResult.Error.Message);

            Assert.Equal(
                "Vietcombank",
                loadResult.Value.BankName);

            Assert.Equal(
                "Trần Thị Bình",
                loadResult.Value.AccountName);
        }
        finally
        {
            DeleteTestDirectory(
                testLocation.Directory);
        }
    }

    [Fact]
    public void
        Store_file_must_not_contain_plaintext_metadata()
    {
        var testLocation =
            CreateTestLocation();

        try
        {
            const string bankName =
                "Ngân hàng Bảo mật";

            const string accountName =
                "Lê Minh Khôi";

            var store =
                new WindowsVietQrRecipientMetadataStore(
                    testLocation.FilePath);

            var saveResult =
                store.Save(
                    new VietQrRecipientMetadata(
                        bankName,
                        accountName));

            Assert.True(
                saveResult.IsSuccess,
                saveResult.Error.Message);

            var protectedBytes =
                File.ReadAllBytes(
                    testLocation.FilePath);

            Assert.Equal(
                -1,
                protectedBytes
                    .AsSpan()
                    .IndexOf(
                        Encoding.UTF8.GetBytes(
                            bankName)));

            Assert.Equal(
                -1,
                protectedBytes
                    .AsSpan()
                    .IndexOf(
                        Encoding.UTF8.GetBytes(
                            accountName)));
        }
        finally
        {
            DeleteTestDirectory(
                testLocation.Directory);
        }
    }

    [Fact]
    public void
        Store_must_reject_corrupt_file_without_throwing()
    {
        var testLocation =
            CreateTestLocation();

        try
        {
            Directory.CreateDirectory(
                testLocation.Directory);

            File.WriteAllBytes(
                testLocation.FilePath,
                new byte[]
                {
                    0x01,
                    0x02,
                    0x03,
                    0x04
                });

            var store =
                new WindowsVietQrRecipientMetadataStore(
                    testLocation.FilePath);

            var loadResult =
                store.Load();

            Assert.True(
                loadResult.IsFailure);

            Assert.False(
                store.IsConfigured);
        }
        finally
        {
            DeleteTestDirectory(
                testLocation.Directory);
        }
    }

    [Fact]
    public void
        Metadata_must_reject_empty_or_invalid_values()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new VietQrRecipientMetadata(
                    string.Empty,
                    "Nguyễn Văn An"));

        Assert.Throws<ArgumentException>(
            () =>
                new VietQrRecipientMetadata(
                    "Vietcombank",
                    string.Empty));

        Assert.Throws<ArgumentException>(
            () =>
                new VietQrRecipientMetadata(
                    new string(
                        'B',
                        121),
                    "Nguyễn Văn An"));

        Assert.Throws<ArgumentException>(
            () =>
                new VietQrRecipientMetadata(
                    "Vietcombank",
                    new string(
                        'A',
                        161)));

        Assert.Throws<ArgumentException>(
            () =>
                new VietQrRecipientMetadata(
                    "Vietcombank\nChi nhánh",
                    "Nguyễn Văn An"));

        Assert.Throws<ArgumentException>(
            () =>
                new VietQrRecipientMetadata(
                    "Vietcombank",
                    "Nguyễn Văn\nAn"));
    }

    private static TestLocation CreateTestLocation()
    {
        var directory =
            Path.Combine(
                Path.GetTempPath(),
                "POS-VietQR-Metadata-Tests",
                Guid.NewGuid()
                    .ToString("N"));

        return new TestLocation(
            directory,
            Path.Combine(
                directory,
                "metadata.bin"));
    }

    private static void DeleteTestDirectory(
        string directory)
    {
        if (Directory.Exists(
                directory))
        {
            Directory.Delete(
                directory,
                recursive:
                    true);
        }
    }

    private sealed record TestLocation(
        string Directory,
        string FilePath);
}
