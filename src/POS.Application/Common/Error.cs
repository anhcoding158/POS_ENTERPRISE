namespace POS.Application.Common;

/// <summary>
/// Mô tả một lỗi có mã ổn định và thông báo thân thiện.
///
/// Code được dùng để:
/// - kiểm tra trong Application;
/// - ánh xạ sang thông báo WPF;
/// - ghi log có cấu trúc;
/// - tránh phụ thuộc vào nội dung Message.
/// </summary>
public sealed record Error
{
    private Error()
    {
        Code = string.Empty;
        Message = string.Empty;
    }

    public Error(
        string code,
        string message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Mã lỗi không được để trống.",
                nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "Thông báo lỗi không được để trống.",
                nameof(message));
        }

        Code = code
            .Trim()
            .ToUpperInvariant();

        Message = message.Trim();
    }

    /// <summary>
    /// Đại diện cho trạng thái không có lỗi.
    /// </summary>
    public static Error None { get; } = new();

    /// <summary>
    /// Lỗi dùng khi một Result thành công nhưng dữ liệu
    /// bắt buộc lại bị null.
    /// </summary>
    public static Error NullValue { get; } = new(
        "GENERAL.NULL_VALUE",
        "Kết quả thành công không chứa dữ liệu.");

    public string Code { get; }

    public string Message { get; }

    public bool IsNone =>
        string.IsNullOrEmpty(Code);

    public override string ToString()
    {
        return IsNone
            ? "No error"
            : $"{Code}: {Message}";
    }
}