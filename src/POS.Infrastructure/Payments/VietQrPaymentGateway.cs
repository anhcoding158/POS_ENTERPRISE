using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Payments;
using POS.Application.Common;
using POS.Application.DTOs.Payments;
using QRCoder;

namespace POS.Infrastructure.Payments;

public sealed class VietQrPaymentGateway(
    IVietQrService vietQr,
    StoredVietQrService storedVietQr,
    IVietQrRecipientMetadataStore recipientMetadata,
    IOptions<VietQrOptions> options) : IVietQrPaymentGateway
{
    private const int PixelsPerModule = 12;

    public Result<VietQrPaymentPayload> Build(long amount, string displayCode)
    {
        var settings = options.Value;
        try
        {
            settings.Validate();
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<VietQrPaymentPayload>(
                new AppError(ErrorCodes.Payments.VietQrInvalidPayload, exception.Message));
        }

        var transferContent = $"{settings.GetNormalizedTransferContentPrefix()} {displayCode}";

        if (storedVietQr.IsConfigured)
        {
            var storedPayload = storedVietQr.BuildPayload(
                new VietQrRequest(amount, displayCode, transferContent));
            if (storedPayload.IsFailure)
                return Result.Failure<VietQrPaymentPayload>(storedPayload.AppError);
            var account = TryReadAccount(storedPayload.Value);
            if (account is null)
                return Result.Failure<VietQrPaymentPayload>(
                    new AppError(ErrorCodes.Payments.VietQrInvalidPayload,
                        "Không đọc được tài khoản nhận tiền từ payload VietQR đã lưu."));
            var metadata = recipientMetadata.Load();
            if (metadata.IsFailure)
                return Result.Failure<VietQrPaymentPayload>(metadata.AppError);
            return Result.Success(new VietQrPaymentPayload(
                storedPayload.Value,
                transferContent,
                account.Value.BankCode,
                account.Value.AccountNumber,
                metadata.Value.AccountName));
        }

        if (!settings.EnableVietQr)
            return Result.Failure<VietQrPaymentPayload>(
                new AppError(ErrorCodes.Payments.VietQrInvalidPayload, "VietQR chưa được cấu hình."));

        var payload = vietQr.BuildPayload(new VietQrRequest(amount, displayCode, transferContent));
        return payload.IsFailure
            ? Result.Failure<VietQrPaymentPayload>(payload.AppError)
            : Result.Success(new VietQrPaymentPayload(
                payload.Value,
                transferContent,
                settings.GetNormalizedBankBin(),
                settings.GetNormalizedAccountNumber(),
                settings.GetNormalizedAccountName()));
    }

    public Result<byte[]> RenderPng(string payloadText)
    {
        if (string.IsNullOrWhiteSpace(payloadText))
            return Result.Failure<byte[]>(new AppError(
                ErrorCodes.Payments.VietQrInvalidPayload,
                "Payload VietQR đã lưu không hợp lệ."));

        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(
                payloadText.Trim(),
                QRCodeGenerator.ECCLevel.Q);
            using var code = new PngByteQRCode(data);
            var png = code.GetGraphic(PixelsPerModule);
            return png.Length == 0
                ? Result.Failure<byte[]>(new AppError(
                    ErrorCodes.Payments.VietQrGenerationFailed,
                    "Không thể render payload VietQR đã lưu."))
                : Result.Success(png);
        }
        catch (Exception exception)
        {
            return Result.Failure<byte[]>(new AppError(
                ErrorCodes.Payments.VietQrGenerationFailed,
                $"Không thể render payload VietQR đã lưu: {exception.Message}"));
        }
    }

    private static (string BankCode, string AccountNumber)? TryReadAccount(string payload)
    {
        foreach (var field in ReadFields(payload))
        {
            if (!int.TryParse(field.Tag, out var tagNumber) || tagNumber is < 26 or > 51)
                continue;
            var merchant = ReadFields(field.Value);
            if (!merchant.Any(x => x.Tag == "00" && x.Value == "A000000727"))
                continue;
            var beneficiary = merchant.SingleOrDefault(x => x.Tag == "01");
            if (beneficiary == default)
                continue;
            var account = ReadFields(beneficiary.Value);
            var bankCode = account.SingleOrDefault(x => x.Tag == "00").Value;
            var accountNumber = account.SingleOrDefault(x => x.Tag == "01").Value;
            if (!string.IsNullOrWhiteSpace(bankCode) && !string.IsNullOrWhiteSpace(accountNumber))
                return (bankCode, accountNumber);
        }
        return null;
    }

    private static List<(string Tag, string Value)> ReadFields(string value)
    {
        var result = new List<(string, string)>();
        for (var index = 0; index + 4 <= value.Length;)
        {
            if (!int.TryParse(value.AsSpan(index + 2, 2), out var length) ||
                index + 4 + length > value.Length)
                return [];
            result.Add((value.Substring(index, 2), value.Substring(index + 4, length)));
            index += 4 + length;
        }
        return result;
    }
}
