using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.StoreSetup;
using POS.Application.DTOs.Payments;
using POS.Infrastructure.Payments;

namespace POS.Infrastructure.StoreSetup;

public sealed class StoreSettingsQrPreviewService(ILoggerFactory loggerFactory) : IStoreSettingsQrPreviewService
{
    public Task<byte[]> GenerateAsync(StoreSettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings); cancellationToken.ThrowIfCancellationRequested();
        var options = new VietQrOptions { EnableVietQr = settings.VietQrEnabled, BankBin = settings.BankBin ?? "", AccountNumber = settings.BankAccountNumber ?? "", AccountName = settings.BankAccountName ?? "", TransferContentPrefix = settings.VietQrContent ?? "POS", QrPixelsPerModule = 8 };
        var service = new VietQrService(Options.Create(options), loggerFactory.CreateLogger<VietQrService>());
        var result = service.GeneratePng(new VietQrRequest(1, "STORE-SETUP", settings.VietQrContent));
        if (result.IsFailure) throw new InvalidOperationException(result.AppError.Message);
        return Task.FromResult(result.Value);
    }
}
