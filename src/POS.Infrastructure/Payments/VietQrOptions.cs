namespace POS.Infrastructure.Payments;

/// <summary>
/// Cấu hình VietQR được bind từ section Payment.
///
/// Cấu hình tài khoản ngân hàng chỉ nằm ở Infrastructure.
/// Application và giao diện không được tự truyền BIN,
/// số tài khoản hoặc tên chủ tài khoản vào service.
/// </summary>
public sealed class VietQrOptions
{
    public const string SectionName =
        "Payment";

    private const int BankBinLength = 6;

    private const int MinimumAccountNumberLength = 3;
    private const int MaximumAccountNumberLength = 19;

    private const int MinimumAccountNameLength = 2;
    private const int MaximumAccountNameLength = 100;

    private const int MaximumTransferContentPrefixLength = 20;

    private const int MinimumQrPixelsPerModule = 4;
    private const int MaximumQrPixelsPerModule = 20;

    /// <summary>
    /// Bật hoặc tắt toàn bộ chức năng VietQR.
    ///
    /// Khi false, ứng dụng vẫn khởi động bình thường dù
    /// thông tin tài khoản chưa được cấu hình.
    /// </summary>
    public bool EnableVietQr
    {
        get;
        set;
    }

    /// <summary>
    /// BIN ngân hàng theo danh sách thành viên NAPAS.
    /// Giá trị phải gồm đúng 6 chữ số.
    /// </summary>
    public string BankBin
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Số tài khoản nhận tiền.
    /// </summary>
    public string AccountNumber
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Tên chủ tài khoản dùng để hiển thị trên giao diện.
    /// </summary>
    public string AccountName
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Tiền tố nội dung chuyển khoản.
    ///
    /// Ví dụ:
    /// POS HD202607230001
    /// </summary>
    public string TransferContentPrefix
    {
        get;
        set;
    } = "POS";

    /// <summary>
    /// Cho phép renderer đưa QR lên hóa đơn ở checkpoint sau.
    /// </summary>
    public bool DisplayQrOnReceipt
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Số pixel cho mỗi module khi tạo ảnh PNG bằng QRCoder.
    /// </summary>
    public int QrPixelsPerModule
    {
        get;
        set;
    } = 8;

    /// <summary>
    /// Kiểm tra cấu hình.
    ///
    /// Khi VietQR bị tắt, thông tin ngân hàng được phép trống.
    /// Khi VietQR được bật, mọi thông tin bắt buộc phải hợp lệ.
    /// </summary>
    public void Validate()
    {
        if (QrPixelsPerModule is
            < MinimumQrPixelsPerModule or
            > MaximumQrPixelsPerModule)
        {
            throw new InvalidOperationException(
                $"Payment:QrPixelsPerModule phải nằm trong khoảng " +
                $"{MinimumQrPixelsPerModule} đến " +
                $"{MaximumQrPixelsPerModule}.");
        }

        if (!EnableVietQr)
        {
            return;
        }

        var normalizedBankBin =
            GetNormalizedBankBin();

        if (normalizedBankBin.Length !=
                BankBinLength ||
            !ContainsOnlyAsciiDigits(
                normalizedBankBin))
        {
            throw new InvalidOperationException(
                "Payment:BankBin phải gồm đúng 6 chữ số.");
        }

        var normalizedAccountNumber =
            GetNormalizedAccountNumber();

        if (normalizedAccountNumber.Length is
                < MinimumAccountNumberLength or
                > MaximumAccountNumberLength ||
            !ContainsOnlyAsciiDigits(
                normalizedAccountNumber))
        {
            throw new InvalidOperationException(
                $"Payment:AccountNumber phải gồm từ " +
                $"{MinimumAccountNumberLength} đến " +
                $"{MaximumAccountNumberLength} chữ số.");
        }

        var normalizedAccountName =
            GetNormalizedAccountName();

        if (normalizedAccountName.Length is
            < MinimumAccountNameLength or
            > MaximumAccountNameLength)
        {
            throw new InvalidOperationException(
                $"Payment:AccountName phải có từ " +
                $"{MinimumAccountNameLength} đến " +
                $"{MaximumAccountNameLength} ký tự.");
        }

        if (ContainsControlCharacter(
                normalizedAccountName))
        {
            throw new InvalidOperationException(
                "Payment:AccountName chứa ký tự điều khiển " +
                "không hợp lệ.");
        }

        var normalizedPrefix =
            GetNormalizedTransferContentPrefix();

        if (normalizedPrefix.Length == 0 ||
            normalizedPrefix.Length >
            MaximumTransferContentPrefixLength)
        {
            throw new InvalidOperationException(
                $"Payment:TransferContentPrefix phải có từ 1 đến " +
                $"{MaximumTransferContentPrefixLength} ký tự.");
        }

        if (ContainsControlCharacter(
                normalizedPrefix))
        {
            throw new InvalidOperationException(
                "Payment:TransferContentPrefix chứa ký tự " +
                "điều khiển không hợp lệ.");
        }
    }

    public string GetNormalizedBankBin()
    {
        return RemoveWhitespace(
            BankBin);
    }

    public string GetNormalizedAccountNumber()
    {
        return RemoveWhitespace(
            AccountNumber);
    }

    public string GetNormalizedAccountName()
    {
        return NormalizeText(
                AccountName)
            .ToUpperInvariant();
    }

    public string
        GetNormalizedTransferContentPrefix()
    {
        return NormalizeText(
                TransferContentPrefix)
            .ToUpperInvariant();
    }

    private static string RemoveWhitespace(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        return string.Concat(
            value.Where(
                character =>
                    !char.IsWhiteSpace(
                        character)));
    }

    private static string NormalizeText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? string.Empty
            : value.Trim();
    }

    private static bool
        ContainsOnlyAsciiDigits(
            string value)
    {
        foreach (var character in value)
        {
            if (character is
                < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool
        ContainsControlCharacter(
            string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(
                    character))
            {
                return true;
            }
        }

        return false;
    }
}