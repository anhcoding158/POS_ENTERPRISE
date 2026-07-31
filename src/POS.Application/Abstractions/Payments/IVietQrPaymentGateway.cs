using POS.Application.Common;

namespace POS.Application.Abstractions.Payments;

public sealed record VietQrPaymentPayload(
    string PayloadText,
    string TransferContent,
    string BankCode,
    string AccountNumber,
    string AccountName);

public interface IVietQrPaymentGateway
{
    Result<VietQrPaymentPayload> Build(long amount, string displayCode);

    Result<byte[]> RenderPng(string payloadText);
}
