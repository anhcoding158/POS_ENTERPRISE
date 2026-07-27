using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class CheckoutRequestJournal : AuditableEntity
{
    private CheckoutRequestJournal()
    {
    }

    public CheckoutRequestJournal(
        Guid clientRequestId,
        string requestFingerprint,
        string canonicalRequestJson,
        string preparedQuoteFingerprint,
        string preparedQuoteJson,
        int preparedByUserId,
        DateTimeOffset utcNow)
    {
        ValidateRequestId(clientRequestId);
        ValidateFingerprint(requestFingerprint, nameof(requestFingerprint));
        ValidateFingerprint(preparedQuoteFingerprint, nameof(preparedQuoteFingerprint));
        ValidateJson(canonicalRequestJson, nameof(canonicalRequestJson));
        ValidateJson(preparedQuoteJson, nameof(preparedQuoteJson));
        if (preparedByUserId <= 0)
            throw new DomainException("CHECKOUT_JOURNAL.USER_REQUIRED", "Người chuẩn bị checkout không hợp lệ.");

        ClientRequestId = clientRequestId;
        RequestFingerprint = requestFingerprint;
        CanonicalRequestJson = canonicalRequestJson;
        PreparedQuoteFingerprint = preparedQuoteFingerprint;
        PreparedQuoteJson = preparedQuoteJson;
        PreparedByUserId = preparedByUserId;
        Status = CheckoutRequestStatus.Prepared;
        MarkCreated(utcNow);
    }

    public Guid ClientRequestId { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public string CanonicalRequestJson { get; private set; } = string.Empty;
    public string PreparedQuoteFingerprint { get; private set; } = string.Empty;
    public string PreparedQuoteJson { get; private set; } = string.Empty;
    public CheckoutRequestStatus Status { get; private set; }
    public int PreparedByUserId { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; private set; }
    public DateTimeOffset? AbandonedAtUtc { get; private set; }
    public int? AbandonedByUserId { get; private set; }
    public int? OrderId { get; private set; }
    public User? PreparedByUser { get; private set; }
    public User? AbandonedByUser { get; private set; }
    public Order? Order { get; private set; }

    public void Complete(int orderId, DateTimeOffset utcNow)
    {
        EnsurePrepared();
        if (orderId <= 0)
            throw new DomainException("CHECKOUT_JOURNAL.ORDER_REQUIRED", "Order hoàn tất không hợp lệ.");
        OrderId = orderId;
        CompletedAtUtc = Normalize(utcNow);
        Status = CheckoutRequestStatus.Completed;
        MarkUpdated(utcNow);
    }

    public void Acknowledge(DateTimeOffset utcNow)
    {
        if (Status != CheckoutRequestStatus.Completed)
            throw new DomainException("CHECKOUT_JOURNAL.NOT_COMPLETED", "Chỉ checkout đã hoàn tất mới được xác nhận.");
        if (AcknowledgedAtUtc.HasValue)
            return;
        AcknowledgedAtUtc = Normalize(utcNow);
        MarkUpdated(utcNow);
    }

    public void Abandon(int actorId, DateTimeOffset utcNow)
    {
        EnsurePrepared();
        if (actorId <= 0)
            throw new DomainException("CHECKOUT_JOURNAL.USER_REQUIRED", "Người bỏ checkout không hợp lệ.");
        AbandonedByUserId = actorId;
        AbandonedAtUtc = Normalize(utcNow);
        Status = CheckoutRequestStatus.Abandoned;
        MarkUpdated(utcNow);
    }

    private void EnsurePrepared()
    {
        if (Status != CheckoutRequestStatus.Prepared)
            throw new DomainException("CHECKOUT_JOURNAL.INVALID_TRANSITION", "Chỉ checkout đang chuẩn bị mới được chuyển trạng thái.");
    }

    private static void ValidateRequestId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("CHECKOUT_JOURNAL.REQUEST_ID_REQUIRED", "ClientRequestId không hợp lệ.");
    }

    private static void ValidateFingerprint(string value, string parameterName)
    {
        if (value is null || value.Length != 64 ||
            value.Any(character => character is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
            throw new DomainException("CHECKOUT_JOURNAL.INVALID_FINGERPRINT", $"{parameterName} phải là SHA-256 uppercase hex.");
    }

    private static void ValidateJson(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("CHECKOUT_JOURNAL.JSON_REQUIRED", $"{parameterName} không được rỗng.");
    }

    private static DateTimeOffset Normalize(DateTimeOffset value)
    {
        if (value == default)
            throw new DomainException("CHECKOUT_JOURNAL.TIME_REQUIRED", "Thời điểm không hợp lệ.");
        return value.ToUniversalTime();
    }
}
