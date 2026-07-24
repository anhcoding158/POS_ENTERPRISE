using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using POS.Application.DTOs.Payments;
using POS.Infrastructure.Payments;
using POS.Wpf.Services;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử composition và availability của dialog VietQR.
///
/// Test không mở Window và không yêu cầu WPF Application.
/// </summary>
public sealed class VietQrPaymentCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void
        Dialog_service_must_be_singleton_and_expose_configuration_state(
            bool enableVietQr)
    {
        var services =
            CreateServices(
                enableVietQr);

        var descriptor =
            Assert.Single(
                services,
                service =>
                    service.ServiceType ==
                    typeof(
                        IVietQrPaymentDialogService));

        Assert.Equal(
            ServiceLifetime.Singleton,
            descriptor.Lifetime);

        Assert.Equal(
            typeof(
                VietQrPaymentDialogService),
            descriptor.ImplementationType);

        using var provider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild =
                        true,

                    ValidateScopes =
                        true
                });

        var first =
            provider
                .GetRequiredService<
                    IVietQrPaymentDialogService>();

        var second =
            provider
                .GetRequiredService<
                    IVietQrPaymentDialogService>();

        Assert.Same(
            first,
            second);

        Assert.Equal(
            enableVietQr,
            first.IsEnabled);
    }

    [Fact]
    public async Task
        Disabled_dialog_must_stop_before_calling_vietqr_core()
    {
        var fakeCore =
            new CountingVietQrService();

        var options =
            CreateOptions(
                enableVietQr:
                    false);

        var dialogService =
            new VietQrPaymentDialogService(
                fakeCore,
                Options.Create(
                    options));

        var result =
            await dialogService.ShowAsync(
                new VietQrPaymentDialogRequest(
                    amount:
                        50_000,

                    paymentReference:
                        "QR-DISABLED-COMPOSITION"),

                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Payments
                .VietQrNotConfigured,
            result.Error.Code);

        Assert.Equal(
            0,
            fakeCore.BuildPayloadCallCount);

        Assert.Equal(
            0,
            fakeCore.GeneratePngCallCount);
    }

    private static ServiceCollection
        CreateServices(
            bool enableVietQr)
    {
        var services =
            new ServiceCollection();

        services.AddOptions<
                VietQrOptions>()
            .Configure(
                options =>
                {
                    CopyOptions(
                        source:
                            CreateOptions(
                                enableVietQr),

                        destination:
                            options);
                });

        services.AddSingleton<
            IVietQrService,
            CountingVietQrService>();

        services.AddSingleton<
            IVietQrPaymentDialogService,
            VietQrPaymentDialogService>();

        return services;
    }

    private static VietQrOptions
        CreateOptions(
            bool enableVietQr)
    {
        return new VietQrOptions
        {
            EnableVietQr =
                enableVietQr,

            BankBin =
                enableVietQr
                    ? "970422"
                    : string.Empty,

            AccountNumber =
                enableVietQr
                    ? "123456789"
                    : string.Empty,

            AccountName =
                enableVietQr
                    ? "NGUYEN VAN A"
                    : string.Empty,

            TransferContentPrefix =
                "POS",

            DisplayQrOnReceipt =
                true,

            QrPixelsPerModule =
                8
        };
    }

    private static void CopyOptions(
        VietQrOptions source,
        VietQrOptions destination)
    {
        destination.EnableVietQr =
            source.EnableVietQr;

        destination.BankBin =
            source.BankBin;

        destination.AccountNumber =
            source.AccountNumber;

        destination.AccountName =
            source.AccountName;

        destination.TransferContentPrefix =
            source.TransferContentPrefix;

        destination.DisplayQrOnReceipt =
            source.DisplayQrOnReceipt;

        destination.QrPixelsPerModule =
            source.QrPixelsPerModule;
    }

    private sealed class CountingVietQrService :
        IVietQrService
    {
        public int BuildPayloadCallCount
        {
            get;
            private set;
        }

        public int GeneratePngCallCount
        {
            get;
            private set;
        }

        public Result<string> BuildPayload(
            VietQrRequest request)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            BuildPayloadCallCount++;

            return Result.Success(
                "0002016304ABCD");
        }

        public Result<byte[]> GeneratePng(
            VietQrRequest request)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            GeneratePngCallCount++;

            return Result.Success<byte[]>(
            [
                0x89,
                0x50,
                0x4E,
                0x47,
                0x0D,
                0x0A,
                0x1A,
                0x0A
            ]);
        }
    }
}