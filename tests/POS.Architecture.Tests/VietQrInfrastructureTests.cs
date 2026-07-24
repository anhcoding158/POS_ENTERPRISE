using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using POS.Application.DTOs.Payments;
using POS.Infrastructure;
using POS.Infrastructure.Payments;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử việc đăng ký VietQR trong Infrastructure
/// và validation của typed Payment options.
///
/// Các test:
/// - không gọi mạng;
/// - không kết nối ngân hàng;
/// - không sử dụng tài khoản thật;
/// - không thay đổi Order;
/// - không gửi thanh toán.
/// </summary>
public sealed class VietQrInfrastructureTests
{
    [Fact]
    public void
        Disabled_options_must_allow_blank_bank_information()
    {
        var options =
            new VietQrOptions
            {
                EnableVietQr =
                    false,

                BankBin =
                    string.Empty,

                AccountNumber =
                    string.Empty,

                AccountName =
                    string.Empty,

                TransferContentPrefix =
                    "POS",

                DisplayQrOnReceipt =
                    true,

                QrPixelsPerModule =
                    8
            };

        options.Validate();

        Assert.False(
            options.EnableVietQr);

        Assert.Equal(
            string.Empty,
            options.GetNormalizedBankBin());

        Assert.Equal(
            string.Empty,
            options.GetNormalizedAccountNumber());

        Assert.Equal(
            string.Empty,
            options.GetNormalizedAccountName());

        Assert.Equal(
            "POS",
            options
                .GetNormalizedTransferContentPrefix());
    }

    [Fact]
    public void
        Enabled_options_must_accept_and_normalize_valid_information()
    {
        var options =
            new VietQrOptions
            {
                EnableVietQr =
                    true,

                BankBin =
                    " 970 422 ",

                AccountNumber =
                    " 123 456 789 ",

                AccountName =
                    "  Nguyen Van A  ",

                TransferContentPrefix =
                    "  pos  ",

                DisplayQrOnReceipt =
                    true,

                QrPixelsPerModule =
                    8
            };

        options.Validate();

        Assert.True(
            options.EnableVietQr);

        Assert.Equal(
            "970422",
            options.GetNormalizedBankBin());

        Assert.Equal(
            "123456789",
            options.GetNormalizedAccountNumber());

        Assert.Equal(
            "NGUYEN VAN A",
            options.GetNormalizedAccountName());

        Assert.Equal(
            "POS",
            options
                .GetNormalizedTransferContentPrefix());
    }

    [Theory]
    [InlineData("")]
    [InlineData("97042")]
    [InlineData("9704221")]
    [InlineData("970A22")]
    public void
        Enabled_options_must_reject_invalid_bank_bin(
            string bankBin)
    {
        var options =
            CreateValidOptions();

        options.BankBin =
            bankBin;

        var exception =
            Assert.Throws<
                InvalidOperationException>(
                    options.Validate);

        Assert.Contains(
            "BankBin",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12345678901234567890")]
    [InlineData("1234A")]
    public void
        Enabled_options_must_reject_invalid_account_number(
            string accountNumber)
    {
        var options =
            CreateValidOptions();

        options.AccountNumber =
            accountNumber;

        var exception =
            Assert.Throws<
                InvalidOperationException>(
                    options.Validate);

        Assert.Contains(
            "AccountNumber",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(21)]
    public void
        Options_must_reject_invalid_pixels_per_module(
            int pixelsPerModule)
    {
        var options =
            CreateValidOptions();

        options.QrPixelsPerModule =
            pixelsPerModule;

        var exception =
            Assert.Throws<
                InvalidOperationException>(
                    options.Validate);

        Assert.Contains(
            "QrPixelsPerModule",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void
        Enabled_options_must_reject_control_character_in_account_name()
    {
        var options =
            CreateValidOptions();

        options.AccountName =
            "NGUYEN\u0001VAN A";

        var exception =
            Assert.Throws<
                InvalidOperationException>(
                    options.Validate);

        Assert.Contains(
            "AccountName",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void
        Infrastructure_must_register_vietqr_service_as_singleton()
    {
        var services =
            CreateServiceCollection(
                enableVietQr:
                    false);

        var descriptor =
            Assert.Single(
                services.Where(
                    service =>
                        service.ServiceType ==
                        typeof(IVietQrService)));

        Assert.Equal(
            ServiceLifetime.Singleton,
            descriptor.Lifetime);

        Assert.Equal(
            typeof(VietQrService),
            descriptor.ImplementationType);

        using var serviceProvider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild =
                        true,

                    ValidateScopes =
                        true
                });

        var firstService =
            serviceProvider
                .GetRequiredService<
                    IVietQrService>();

        var secondService =
            serviceProvider
                .GetRequiredService<
                    IVietQrService>();

        Assert.Same(
            firstService,
            secondService);

        Assert.IsType<VietQrService>(
            firstService);
    }

    [Fact]
    public void
        Disabled_vietqr_service_must_return_not_configured_error()
    {
        var services =
            CreateServiceCollection(
                enableVietQr:
                    false);

        using var serviceProvider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild =
                        true,

                    ValidateScopes =
                        true
                });

        var service =
            serviceProvider
                .GetRequiredService<
                    IVietQrService>();

        var result =
            service.BuildPayload(
                new VietQrRequest(
                    amount:
                        50_000,

                    orderCode:
                        "HD-DI-DISABLED"));

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrNotConfigured,
            result.Error.Code);
    }

    [Fact]
    public void
        Enabled_vietqr_service_must_create_payload_and_png()
    {
        var services =
            CreateServiceCollection(
                enableVietQr:
                    true);

        using var serviceProvider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild =
                        true,

                    ValidateScopes =
                        true
                });

        var service =
            serviceProvider
                .GetRequiredService<
                    IVietQrService>();

        var request =
            new VietQrRequest(
                amount:
                    135_000,

                orderCode:
                    "HD-DI-ENABLED");

        var payloadResult =
            service.BuildPayload(
                request);

        Assert.True(
            payloadResult.IsSuccess,
            payloadResult.Error.Message);

        Assert.StartsWith(
            "000201",
            payloadResult.Value,
            StringComparison.Ordinal);

        Assert.Contains(
            "A000000727",
            payloadResult.Value,
            StringComparison.Ordinal);

        Assert.Contains(
            "QRIBFTTA",
            payloadResult.Value,
            StringComparison.Ordinal);

        Assert.Contains(
            "5303704",
            payloadResult.Value,
            StringComparison.Ordinal);

        Assert.Contains(
            "5406135000",
            payloadResult.Value,
            StringComparison.Ordinal);

        Assert.EndsWith(
            payloadResult.Value[
                ^8..],
            payloadResult.Value,
            StringComparison.Ordinal);

        var pngResult =
            service.GeneratePng(
                request);

        Assert.True(
            pngResult.IsSuccess,
            pngResult.Error.Message);

        Assert.True(
            pngResult.Value.Length >
            8);

        Assert.Equal(
            0x89,
            pngResult.Value[0]);

        Assert.Equal(
            0x50,
            pngResult.Value[1]);

        Assert.Equal(
            0x4E,
            pngResult.Value[2]);

        Assert.Equal(
            0x47,
            pngResult.Value[3]);
    }

    [Fact]
    public void
        Invalid_enabled_configuration_must_fail_when_options_are_resolved()
    {
        var configuration =
            CreateConfiguration(
                enableVietQr:
                    true,

                bankBin:
                    "97042");

        var services =
            new ServiceCollection();

        services.AddLogging();

        services.AddInfrastructure(
            configuration);

        using var serviceProvider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild =
                        true,

                    ValidateScopes =
                        true
                });

        var exception =
            Assert.Throws<
                OptionsValidationException>(
                    () =>
                    {
                        _ =
                            serviceProvider
                                .GetRequiredService<
                                    IOptions<
                                        VietQrOptions>>()
                                .Value;
                    });

        Assert.Contains(
            "Payment/VietQR",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceCollection
        CreateServiceCollection(
            bool enableVietQr)
    {
        var configuration =
            CreateConfiguration(
                enableVietQr,
                bankBin:
                    enableVietQr
                        ? "970422"
                        : string.Empty);

        var services =
            new ServiceCollection();

        services.AddLogging();

        services.AddInfrastructure(
            configuration);

        return services;
    }

    private static IConfiguration
        CreateConfiguration(
            bool enableVietQr,
            string bankBin)
    {
        var configurationValues =
            new Dictionary<string, string?>
            {
                /*
                 * Infrastructure.
                 */
                ["Infrastructure:DatabasePath"] =
                    "data/vietqr-di-test.db",

                ["Infrastructure:DatabaseTimeoutSeconds"] =
                    "30",

                ["Infrastructure:ApplyMigrationsOnStartup"] =
                    "false",

                ["Infrastructure:SeedDemoProductCatalog"] =
                    "false",

                ["Infrastructure:SeedDefaultAdministrator"] =
                    "false",

                /*
                 * Store.
                 */
                ["Store:Name"] =
                    "Cửa hàng kiểm thử VietQR",

                ["Store:Address"] =
                    "Địa chỉ kiểm thử",

                ["Store:Phone"] =
                    "0900 000 000",

                ["Store:TaxCode"] =
                    "0100000000",

                ["Store:FooterMessage"] =
                    "Cảm ơn quý khách!",

                /*
                 * Printer.
                 */
                ["Hardware:PrinterName"] =
                    "Microsoft Print to PDF",

                ["Hardware:PaperSize"] =
                    "K80",

                /*
                 * VietQR.
                 */
                ["Payment:EnableVietQr"] =
                    enableVietQr
                        .ToString(
                            provider:
                                null),

                ["Payment:BankBin"] =
                    bankBin,

                ["Payment:AccountNumber"] =
                    enableVietQr
                        ? "123456789"
                        : string.Empty,

                ["Payment:AccountName"] =
                    enableVietQr
                        ? "NGUYEN VAN A"
                        : string.Empty,

                ["Payment:TransferContentPrefix"] =
                    "POS",

                ["Payment:DisplayQrOnReceipt"] =
                    "true",

                ["Payment:QrPixelsPerModule"] =
                    "8"
            };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                configurationValues)
            .Build();
    }

    private static VietQrOptions
        CreateValidOptions()
    {
        return new VietQrOptions
        {
            EnableVietQr =
                true,

            BankBin =
                "970422",

            AccountNumber =
                "123456789",

            AccountName =
                "NGUYEN VAN A",

            TransferContentPrefix =
                "POS",

            DisplayQrOnReceipt =
                true,

            QrPixelsPerModule =
                8
        };
    }
}