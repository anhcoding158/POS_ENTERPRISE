using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using POS.Application.DTOs.Payments;
using POS.Infrastructure.Payments;
using System.Globalization;
using System.Text;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class StoredVietQrPipelineTests
{
    [Fact]
    public void
        Store_must_save_load_and_delete_payload()
    {
        var directory =
            Path.Combine(
                Path.GetTempPath(),
                "POS-VietQR-Tests",
                Guid.NewGuid()
                    .ToString("N"));

        var filePath =
            Path.Combine(
                directory,
                "vietqr.bin");

        try
        {
            var store =
                new WindowsVietQrPayloadStore(
                    filePath);

            var payload =
                BuildBasePayload();

            var saveResult =
                store.Save(
                    payload);

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
                payload,
                loadResult.Value);

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
            if (Directory.Exists(
                    directory))
            {
                Directory.Delete(
                    directory,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public void
        Stored_service_must_replace_amount_content_and_crc()
    {
        var store =
            new FakePayloadStore(
                BuildBasePayload(
                    amount:
                        5_000,

                    transferContent:
                        "NOI DUNG CU"));

        var service =
            CreateService(
                store);

        var result =
            service.BuildPayload(
                new VietQrRequest(
                    amount:
                        135_000,

                    orderCode:
                        "HD-20260725-001"));

        Assert.True(
            result.IsSuccess,
            result.Error.Message);

        var topLevel =
            ReadFields(
                result.Value);

        Assert.Single(
            topLevel,
            field =>
                field.Tag == "01");

        Assert.Equal(
            "12",
            topLevel.Single(
                    field =>
                        field.Tag == "01")
                .Value);

        Assert.Single(
            topLevel,
            field =>
                field.Tag == "54");

        Assert.Equal(
            "135000",
            topLevel.Single(
                    field =>
                        field.Tag == "54")
                .Value);

        var additionalData =
            topLevel.Single(
                field =>
                    field.Tag == "62");

        var nested =
            ReadFields(
                additionalData.Value);

        Assert.Equal(
            "POS HD 20260725 001",
            nested.Single(
                    field =>
                        field.Tag == "08")
                .Value);

        Assert.DoesNotContain(
            "NOI DUNG CU",
            result.Value,
            StringComparison.Ordinal);

        Assert.True(
            HasValidCrc(
                result.Value));
    }

    [Fact]
    public void
        Stored_service_must_generate_png()
    {
        var service =
            CreateService(
                new FakePayloadStore(
                    BuildBasePayload()));

        var result =
            service.GeneratePng(
                new VietQrRequest(
                    amount:
                        95_000,

                    orderCode:
                        "HD-PNG-001"));

        Assert.True(
            result.IsSuccess,
            result.Error.Message);

        Assert.True(
            result.Value.Length > 8);

        Assert.Equal(
            new byte[]
            {
                0x89,
                0x50,
                0x4E,
                0x47,
                0x0D,
                0x0A,
                0x1A,
                0x0A
            },
            result.Value[..8]);
    }

    [Fact]
    public void
        Stored_service_must_fail_when_no_payload_exists()
    {
        var service =
            CreateService(
                new FakePayloadStore(
                    payload:
                        null));

        var result =
            service.BuildPayload(
                new VietQrRequest(
                    amount:
                        50_000,

                    orderCode:
                        "HD-NO-CONFIG"));

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrNotConfigured,
            result.Error.Code);
    }

    private static StoredVietQrService
        CreateService(
            IVietQrPayloadStore store)
    {
        var options =
            Options.Create(
                new VietQrOptions
                {
                    EnableVietQr =
                        false,

                    TransferContentPrefix =
                        "POS",

                    QrPixelsPerModule =
                        8
                });

        return new StoredVietQrService(
            store,
            options,
            NullLogger<StoredVietQrService>
                .Instance);
    }

    private static string BuildBasePayload(
        long? amount = null,
        string? transferContent = null)
    {
        var beneficiary =
            CreateTlv(
                "00",
                "970422") +
            CreateTlv(
                "01",
                "123456789");

        var merchantAccount =
            CreateTlv(
                "00",
                "A000000727") +
            CreateTlv(
                "01",
                beneficiary) +
            CreateTlv(
                "02",
                "QRIBFTTA");

        var payload =
            CreateTlv(
                "00",
                "01") +
            CreateTlv(
                "01",
                "11") +
            CreateTlv(
                "38",
                merchantAccount) +
            CreateTlv(
                "53",
                "704");

        if (amount.HasValue)
        {
            payload +=
                CreateTlv(
                    "54",
                    amount.Value.ToString(
                        CultureInfo.InvariantCulture));
        }

        payload +=
            CreateTlv(
                "58",
                "VN");

        if (!string.IsNullOrWhiteSpace(
                transferContent))
        {
            payload +=
                CreateTlv(
                    "62",
                    CreateTlv(
                        "08",
                        transferContent));
        }

        var crcSource =
            payload +
            "6304";

        return
            crcSource +
            ComputeCrc(
                crcSource);
    }

    private static string CreateTlv(
        string tag,
        string value)
    {
        return
            $"{tag}" +
            $"{Encoding.UTF8.GetByteCount(value)
                .ToString(
                    "D2",
                    CultureInfo.InvariantCulture)}" +
            $"{value}";
    }

    private static IReadOnlyList<TestField>
        ReadFields(
            string payload)
    {
        var bytes =
            Encoding.UTF8.GetBytes(
                payload);

        var fields =
            new List<TestField>();

        var index =
            0;

        while (index <
               bytes.Length)
        {
            var tag =
                Encoding.ASCII.GetString(
                    bytes,
                    index,
                    2);

            var length =
                ((bytes[index + 2] -
                  (byte)'0') * 10) +
                (bytes[index + 3] -
                 (byte)'0');

            var valueStart =
                index + 4;

            var value =
                Encoding.UTF8.GetString(
                    bytes,
                    valueStart,
                    length);

            fields.Add(
                new TestField(
                    tag,
                    value));

            index =
                valueStart +
                length;
        }

        return fields;
    }

    private static bool HasValidCrc(
        string payload)
    {
        if (payload.Length < 8 ||
            payload[^8..^4] !=
            "6304")
        {
            return false;
        }

        var expected =
            payload[^4..];

        var crcSource =
            payload[..^4];

        return string.Equals(
            expected,
            ComputeCrc(
                crcSource),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeCrc(
        string value)
    {
        var crc =
            0xFFFF;

        foreach (var currentByte in
                 Encoding.UTF8.GetBytes(
                     value))
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

    private sealed record TestField(
        string Tag,
        string Value);

    private sealed class FakePayloadStore :
        IVietQrPayloadStore
    {
        private string?
            _payload;

        public FakePayloadStore(
            string? payload)
        {
            _payload =
                payload;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(
                _payload);

        public Result<string> Load()
        {
            return IsConfigured
                ? Result.Success(
                    _payload!)
                : Result.Failure<string>(
                    new Error(
                        ErrorCodes.Payments
                            .VietQrNotConfigured,

                        "Chưa cấu hình."));
        }

        public Result Save(
            string payload)
        {
            _payload =
                payload;

            return Result.Success();
        }

        public Result Delete()
        {
            _payload =
                null;

            return Result.Success();
        }
    }
}