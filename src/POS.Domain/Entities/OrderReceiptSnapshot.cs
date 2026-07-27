namespace POS.Domain.Entities;

/// <summary>
/// Bản chụp hóa đơn gốc bất biến đã được serialize tại thời điểm bán.
/// </summary>
public sealed class OrderReceiptSnapshot
{
    public OrderReceiptSnapshot(
        int orderId,
        int snapshotVersion,
        string payloadJson,
        DateTimeOffset createdAtUtc)
    {
        if (orderId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orderId),
                orderId,
                "Mã đơn hàng phải lớn hơn 0.");
        }

        if (snapshotVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshotVersion),
                snapshotVersion,
                "Phiên bản snapshot phải lớn hơn 0.");
        }

        if (string.IsNullOrWhiteSpace(
                payloadJson))
        {
            throw new ArgumentException(
                "Nội dung snapshot không được để trống.",
                nameof(payloadJson));
        }

        if (createdAtUtc == default)
        {
            throw new ArgumentException(
                "Thời điểm tạo snapshot không hợp lệ.",
                nameof(createdAtUtc));
        }

        OrderId =
            orderId;

        SnapshotVersion =
            snapshotVersion;

        PayloadJson =
            payloadJson;

        CreatedAtUtc =
            createdAtUtc.ToUniversalTime();
    }

    public int OrderId { get; private set; }

    public int SnapshotVersion { get; private set; }

    public string PayloadJson { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
