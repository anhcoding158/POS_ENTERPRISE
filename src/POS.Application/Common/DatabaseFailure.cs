namespace POS.Application.Common;

/// <summary>
/// Phân loại lỗi lưu trữ trung lập với database provider.
/// </summary>
public enum DatabaseFailureKind
{
    Busy,
    Locked,
    DiskFull,
    Corruption,
    Unknown
}

/// <summary>
/// Lỗi database đã được adapter Infrastructure phân loại.
/// Exception gốc được giữ làm InnerException cho technical logging.
/// </summary>
public sealed class DatabaseOperationException : Exception
{
    public DatabaseOperationException(
        DatabaseFailureKind kind,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public DatabaseFailureKind Kind { get; }
}
