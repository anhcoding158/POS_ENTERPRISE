namespace POS.Application.Abstractions.DateTime;

/// <summary>
/// Nguồn thời gian của ứng dụng.
///
/// Không gọi DateTimeOffset.UtcNow trực tiếp trong service.
/// Nhờ interface này, unit test có thể dùng thời gian cố định.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

//public sealed class SystemClock : IClock
//{
//    public DateTimeOffset UtcNow =>
//        DateTimeOffset.UtcNow;
//}