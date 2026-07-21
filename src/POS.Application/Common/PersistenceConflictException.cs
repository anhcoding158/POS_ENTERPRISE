namespace POS.Application.Common;

/// <summary>
/// Phân loại xung đột xảy ra khi lưu dữ liệu.
/// </summary>
public enum PersistenceConflictKind
{
    Unknown = 0,

    /// <summary>
    /// Bản ghi đã được một thao tác khác thay đổi.
    /// </summary>
    Concurrency = 1,

    /// <summary>
    /// Dữ liệu vi phạm unique constraint.
    /// </summary>
    UniqueConstraint = 2
}

/// <summary>
/// Các target ổn định để Infrastructure thông báo
/// constraint nào đã bị vi phạm.
///
/// Application không phụ thuộc tên bảng hoặc index SQLite.
/// </summary>
public static class PersistenceConflictTargets
{
    public const string ProductCode =
        "product.code";

    public const string ProductBarcode =
        "product.barcode";

    public const string CategoryName =
        "category.name";

    public const string UserNormalizedUsername =
        "user.normalized_username";

    public const string OrderCode =
        "order.code";
}

/// <summary>
/// Exception trung gian giữa Infrastructure và Application.
/// </summary>
public sealed class PersistenceConflictException :
    Exception
{
    public PersistenceConflictException()
        : this(
            PersistenceConflictKind.Unknown,
            "Đã xảy ra xung đột khi lưu dữ liệu.")
    {
    }

    public PersistenceConflictException(
        string message)
        : this(
            PersistenceConflictKind.Unknown,
            message)
    {
    }

    public PersistenceConflictException(
        string message,
        Exception innerException)
        : this(
            PersistenceConflictKind.Unknown,
            message,
            target: null,
            innerException)
    {
    }

    public PersistenceConflictException(
        PersistenceConflictKind kind,
        string message,
        string? target = null,
        Exception? innerException = null)
        : base(
            message,
            innerException)
    {
        if (string.IsNullOrWhiteSpace(
                message))
        {
            throw new ArgumentException(
                "Thông báo lỗi không được để trống.",
                nameof(message));
        }

        Kind = kind;

        Target =
            string.IsNullOrWhiteSpace(
                target)
                ? null
                : target.Trim();
    }

    public PersistenceConflictKind Kind { get; }

    public string? Target { get; }
}