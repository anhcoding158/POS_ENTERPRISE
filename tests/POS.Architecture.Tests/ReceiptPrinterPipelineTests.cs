using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Printing;
using POS.Application.DTOs.Printing;
using POS.Domain.Enums;
using POS.Infrastructure;
using POS.Infrastructure.Printing;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử typed printer configuration và đăng ký
/// print pipeline trong Infrastructure.
///
/// Các test không gửi print job thật và không phụ thuộc
/// máy in đang cài trên máy chạy test.
/// </summary>
public sealed class ReceiptPrinterPipelineTests
{
    [Fact]
    public void
        Printer_options_must_accept_and_normalize_k80_configuration()
    {
        var options =
            new ReceiptPrinterOptions
            {
                PrinterName =
                    "  Microsoft Print to PDF  ",

                PaperSize =
                    " k80 "
            };

        options.Validate();

        Assert.Equal(
            "Microsoft Print to PDF",
            options.GetNormalizedPrinterName());

        Assert.Equal(
            ReceiptPrinterOptions.SupportedPaperSize,
            options.GetNormalizedPaperSize());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void
        Printer_options_must_reject_blank_printer_name(
            string printerName)
    {
        var options =
            new ReceiptPrinterOptions
            {
                PrinterName =
                    printerName,

                PaperSize =
                    ReceiptPrinterOptions
                        .SupportedPaperSize
            };

        var exception =
            Assert.Throws<
                InvalidOperationException>(
                    options.Validate);

        Assert.Contains(
            "PrinterName",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void
        Printer_options_must_reject_unsupported_paper_size()
    {
        var options =
            new ReceiptPrinterOptions
            {
                PrinterName =
                    "Microsoft Print to PDF",

                PaperSize =
                    "A4"
            };

        var exception =
            Assert.Throws<
                InvalidOperationException>(
                    options.Validate);

        Assert.Contains(
            "chỉ hỗ trợ K80",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void
        Wpf_receipt_service_constructor_must_fail_fast_for_invalid_options()
    {
        var options =
            Options.Create(
                new ReceiptPrinterOptions
                {
                    PrinterName =
                        string.Empty,

                    PaperSize =
                        ReceiptPrinterOptions
                            .SupportedPaperSize
                });

        Assert.Throws<
            InvalidOperationException>(
                () =>
                    new WpfReceiptService(
                        new ReceiptDocumentBuilder(),

                        options,

                        NullLogger<WpfReceiptService>
                            .Instance));
    }

    [Fact]
    public void
        Infrastructure_must_register_print_pipeline_as_singletons()
    {
        var configurationValues =
            new Dictionary<string, string?>
            {
                ["Infrastructure:DatabasePath"] =
                    "data/receipt-printer-di-test.db",

                ["Infrastructure:DatabaseTimeoutSeconds"] =
                    "30",

                ["Infrastructure:ApplyMigrationsOnStartup"] =
                    "false",

                ["Infrastructure:SeedDemoProductCatalog"] =
                    "false",

                ["Infrastructure:SeedDefaultAdministrator"] =
                    "false",

                ["Store:Name"] =
                    "Cửa hàng kiểm thử",

                ["Store:Address"] =
                    "Địa chỉ kiểm thử",

                ["Store:Phone"] =
                    "0900 000 000",

                ["Store:TaxCode"] =
                    "0100000000",

                ["Store:FooterMessage"] =
                    "Cảm ơn quý khách!",

                ["Hardware:PrinterName"] =
                    "Microsoft Print to PDF",

                ["Hardware:PaperSize"] =
                    "K80"
            };

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    configurationValues)
                .Build();

        var services =
            new ServiceCollection();

        services.AddLogging();

        services.AddInfrastructure(
            configuration);

        var receiptServiceDescriptor =
            Assert.Single(
                services.Where(
                    descriptor =>
                        descriptor.ServiceType ==
                        typeof(IReceiptService)));

        Assert.Equal(
            ServiceLifetime.Singleton,
            receiptServiceDescriptor.Lifetime);

        Assert.Equal(
            typeof(WpfReceiptService),
            receiptServiceDescriptor
                .ImplementationType);

        var documentBuilderDescriptor =
            Assert.Single(
                services.Where(
                    descriptor =>
                        descriptor.ServiceType ==
                        typeof(ReceiptDocumentBuilder)));

        Assert.Equal(
            ServiceLifetime.Singleton,
            documentBuilderDescriptor.Lifetime);

        Assert.Equal(
            typeof(ReceiptDocumentBuilder),
            documentBuilderDescriptor
                .ImplementationType);

        using var serviceProvider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild =
                        true,

                    ValidateScopes =
                        true
                });

        var firstReceiptService =
            serviceProvider
                .GetRequiredService<
                    IReceiptService>();

        var secondReceiptService =
            serviceProvider
                .GetRequiredService<
                    IReceiptService>();

        var firstDocumentBuilder =
            serviceProvider
                .GetRequiredService<
                    ReceiptDocumentBuilder>();

        var secondDocumentBuilder =
            serviceProvider
                .GetRequiredService<
                    ReceiptDocumentBuilder>();

        Assert.Same(
            firstReceiptService,
            secondReceiptService);

        Assert.Same(
            firstDocumentBuilder,
            secondDocumentBuilder);

        Assert.IsType<WpfReceiptService>(
            firstReceiptService);

        var printerOptions =
            serviceProvider
                .GetRequiredService<
                    IOptions<ReceiptPrinterOptions>>()
                .Value;

        Assert.Equal(
            "Microsoft Print to PDF",
            printerOptions
                .GetNormalizedPrinterName());

        Assert.Equal(
            "K80",
            printerOptions
                .GetNormalizedPaperSize());
    }

    [Fact]
    public async Task
        Print_after_service_disposal_must_throw_object_disposed_exception()
    {
        var service =
            new WpfReceiptService(
                new ReceiptDocumentBuilder(),

                Options.Create(
                    new ReceiptPrinterOptions
                    {
                        PrinterName =
                            "Microsoft Print to PDF",

                        PaperSize =
                            ReceiptPrinterOptions
                                .SupportedPaperSize
                    }),

                NullLogger<WpfReceiptService>
                    .Instance);

        service.Dispose();

        var request =
            CreateConfiguredReceipt();

        await Assert.ThrowsAsync<
            ObjectDisposedException>(
                () =>
                    service.PrintAsync(
                        request,
                        TestContext
                            .Current
                            .CancellationToken));
    }

    private static ReceiptRequest
        CreateConfiguredReceipt()
    {
        var line =
            new ReceiptLineDto(
                orderItemId:
                    1,

                productId:
                    1,

                productCode:
                    "SP-PRINT-TEST",

                productName:
                    "Sản phẩm kiểm thử in",

                unitName:
                    "Cái",

                quantity:
                    1,

                unitSalePrice:
                    10_000,

                modifierAmountPerUnit:
                    0,

                finalUnitPrice:
                    10_000,

                grossAmount:
                    10_000,

                lineDiscountAmount:
                    0,

                netAmount:
                    10_000,

                notes:
                    null,

                modifiers:
                    []);

        var createdAtUtc =
            new DateTimeOffset(
                2026,
                7,
                23,
                16,
                0,
                0,
                TimeSpan.Zero);

        return new ReceiptRequest(
            store:
                new ReceiptStoreSnapshotDto(
                    name:
                        "Cửa hàng kiểm thử",

                    address:
                        "Địa chỉ kiểm thử",

                    phone:
                        "0900 000 000",

                    taxCode:
                        "0100000000",

                    footerMessage:
                        "Cảm ơn quý khách!"),

            copyKind:
                ReceiptCopyKind.Original,

            copyNumber:
                0,

            orderId:
                1,

            orderCode:
                "HD-PRINT-TEST",

            cashierName:
                "Thu ngân kiểm thử",

            createdAtUtc:
                createdAtUtc,

            paymentMethod:
                PaymentMethod.Cash,

            subtotal:
                10_000,

            discountAmount:
                0,

            totalAmount:
                10_000,

            cashReceived:
                20_000,

            changeAmount:
                10_000,

            lines:
            [
                line
            ],

            paidAtUtc:
                createdAtUtc);
    }
}