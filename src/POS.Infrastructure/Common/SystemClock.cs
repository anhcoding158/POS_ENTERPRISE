using POS.Application.Abstractions.DateTime;

namespace POS.Infrastructure.Common;

/// <summary>
/// Đồng hồ hệ thống sử dụng thời gian UTC.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow =>
        DateTimeOffset.UtcNow;
}