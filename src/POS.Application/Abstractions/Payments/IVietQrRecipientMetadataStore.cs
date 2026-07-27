using POS.Application.Common;

namespace POS.Application.Abstractions.Payments;

public sealed record VietQrRecipientMetadata
{
    private const int
        MaximumBankNameLength =
            120;

    private const int
        MaximumAccountNameLength =
            160;

    public VietQrRecipientMetadata(
        string bankName,
        string accountName)
    {
        BankName =
            Validate(
                bankName,
                MaximumBankNameLength,
                nameof(bankName));

        AccountName =
            Validate(
                accountName,
                MaximumAccountNameLength,
                nameof(accountName));
    }

    public string BankName { get; }

    public string AccountName { get; }

    private static string Validate(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(
            value,
            parameterName);

        if (value.Any(
                char.IsControl))
        {
            throw new ArgumentException(
                "Giá trị không được chứa ký tự điều khiển.",
                parameterName);
        }

        var normalized =
            value.Trim();

        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                "Giá trị không được để trống.",
                parameterName);
        }

        if (normalized.Length >
            maximumLength)
        {
            throw new ArgumentException(
                $"Giá trị không được vượt quá {maximumLength} ký tự.",
                parameterName);
        }

        return normalized;
    }
}

public interface IVietQrRecipientMetadataStore
{
    bool IsConfigured
    {
        get;
    }

    Result<VietQrRecipientMetadata> Load();

    Result Save(
        VietQrRecipientMetadata metadata);

    Result Delete();
}
