namespace POS.Domain.Enums;

/// <summary>
/// Trạng thái xử lý một sự kiện đồng bộ trong Outbox.
/// </summary>
public enum SyncStatus
{
    Pending = 1,

    Processing = 2,

    Processed = 3,

    Failed = 4,

    DeadLetter = 5
}