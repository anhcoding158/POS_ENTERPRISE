namespace POS.Domain.Common;

/// <summary>
/// Lỗi phát sinh khi một quy tắc nghiệp vụ của Domain bị vi phạm.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(
        string code,
        string message)
        : base(message)
    {
        Code = ValidateCode(code);
    }

    public DomainException(
        string code,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Code = ValidateCode(code);
    }

    /// <summary>
    /// Mã lỗi ổn định dùng cho Application và giao diện.
    /// </summary>
    public string Code { get; }

    private static string ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Mã lỗi Domain không được để trống.",
                nameof(code));
        }

        return code.Trim().ToUpperInvariant();
    }
}