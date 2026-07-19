namespace POS.Domain.Common;

/// <summary>
/// Lớp cơ sở cho các thực thể cần lưu thời điểm tạo và cập nhật.
/// </summary>
public abstract class AuditableEntity : Entity
{
    /// <summary>
    /// Thời điểm tạo theo UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Thời điểm cập nhật gần nhất theo UTC.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Khởi tạo thông tin audit khi thực thể được tạo mới.
    /// </summary>
    public void MarkCreated(DateTimeOffset utcNow)
    {
        var normalizedUtc = NormalizeUtc(utcNow);

        if (CreatedAtUtc != default)
        {
            throw new DomainException(
                "AUDIT.ALREADY_INITIALIZED",
                "Thông tin thời điểm tạo của thực thể đã được khởi tạo.");
        }

        CreatedAtUtc = normalizedUtc;
        UpdatedAtUtc = normalizedUtc;
    }

    /// <summary>
    /// Cập nhật thời điểm chỉnh sửa gần nhất.
    /// </summary>
    public void MarkUpdated(DateTimeOffset utcNow)
    {
        var normalizedUtc = NormalizeUtc(utcNow);

        if (CreatedAtUtc == default)
        {
            CreatedAtUtc = normalizedUtc;
        }

        if (normalizedUtc < CreatedAtUtc)
        {
            throw new DomainException(
                "AUDIT.INVALID_UPDATED_TIME",
                "Thời điểm cập nhật không được nhỏ hơn thời điểm tạo.");
        }

        UpdatedAtUtc = normalizedUtc;
    }

    private static DateTimeOffset NormalizeUtc(
        DateTimeOffset value)
    {
        if (value == default)
        {
            throw new DomainException(
                "AUDIT.TIME_REQUIRED",
                "Thời điểm audit không được để trống.");
        }

        return value.ToUniversalTime();
    }
}