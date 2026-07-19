using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Sự kiện chờ xử lý theo Outbox Pattern.
///
/// OutboxMessage không kế thừa Entity vì sử dụng khóa Guid
/// và có vòng đời kỹ thuật riêng.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(
        string aggregateType,
        string aggregateId,
        string eventType,
        string payloadJson,
        DateTimeOffset utcNow)
    {
        Id = Guid.NewGuid();

        SetAggregateType(aggregateType);
        SetAggregateId(aggregateId);
        SetEventType(eventType);
        SetPayloadJson(payloadJson);

        CreatedAtUtc = NormalizeUtc(utcNow);
        Status = SyncStatus.Pending;
    }

    public Guid Id { get; private set; }

    public string AggregateType { get; private set; } =
        string.Empty;

    public string AggregateId { get; private set; } =
        string.Empty;

    public string EventType { get; private set; } =
        string.Empty;

    public string PayloadJson { get; private set; } =
        string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public int RetryCount { get; private set; }

    public SyncStatus Status { get; private set; }

    public string? LastError { get; private set; }

    public void MarkProcessing(DateTimeOffset utcNow)
    {
        if (Status is not (
            SyncStatus.Pending or
            SyncStatus.Failed))
        {
            throw new DomainException(
                "OUTBOX.CANNOT_START_PROCESSING",
                "Trạng thái hiện tại không cho phép xử lý sự kiện.");
        }

        if (RetryCount >=
            BusinessRules.Outbox.MaximumRetryCount)
        {
            Status = SyncStatus.DeadLetter;

            throw new DomainException(
                "OUTBOX.RETRY_LIMIT_REACHED",
                "Sự kiện đã vượt quá số lần thử lại.");
        }

        Status = SyncStatus.Processing;
        LastAttemptAtUtc = NormalizeUtc(utcNow);
        LastError = null;
    }

    public void MarkProcessed(DateTimeOffset utcNow)
    {
        if (Status != SyncStatus.Processing)
        {
            throw new DomainException(
                "OUTBOX.NOT_PROCESSING",
                "Chỉ sự kiện đang xử lý mới được đánh dấu thành công.");
        }

        Status = SyncStatus.Processed;
        ProcessedAtUtc = NormalizeUtc(utcNow);
        LastError = null;
    }

    public void MarkFailed(
        string error,
        DateTimeOffset utcNow)
    {
        if (Status != SyncStatus.Processing)
        {
            throw new DomainException(
                "OUTBOX.NOT_PROCESSING",
                "Chỉ sự kiện đang xử lý mới được đánh dấu thất bại.");
        }

        if (string.IsNullOrWhiteSpace(error))
        {
            throw new DomainException(
                "OUTBOX.ERROR_REQUIRED",
                "Phải có thông tin lỗi khi xử lý thất bại.");
        }

        if (RetryCount < int.MaxValue)
        {
            RetryCount++;
        }

        LastAttemptAtUtc = NormalizeUtc(utcNow);
        LastError = TruncateError(error);

        Status =
            RetryCount >=
            BusinessRules.Outbox.MaximumRetryCount
                ? SyncStatus.DeadLetter
                : SyncStatus.Failed;
    }

    public void Requeue(DateTimeOffset utcNow)
    {
        if (Status != SyncStatus.Failed)
        {
            throw new DomainException(
                "OUTBOX.CANNOT_REQUEUE",
                "Chỉ sự kiện thất bại mới được đưa vào hàng đợi lại.");
        }

        if (RetryCount >=
            BusinessRules.Outbox.MaximumRetryCount)
        {
            Status = SyncStatus.DeadLetter;

            throw new DomainException(
                "OUTBOX.RETRY_LIMIT_REACHED",
                "Sự kiện đã vượt quá số lần thử lại.");
        }

        Status = SyncStatus.Pending;
        LastAttemptAtUtc = NormalizeUtc(utcNow);
        LastError = null;
    }

    private void SetAggregateType(string value)
    {
        AggregateType = ValidateRequiredText(
            value,
            BusinessRules.Outbox.AggregateTypeMaxLength,
            "OUTBOX.AGGREGATE_TYPE_REQUIRED",
            "OUTBOX.AGGREGATE_TYPE_TOO_LONG",
            "Loại aggregate");
    }

    private void SetAggregateId(string value)
    {
        AggregateId = ValidateRequiredText(
            value,
            BusinessRules.Outbox.AggregateIdMaxLength,
            "OUTBOX.AGGREGATE_ID_REQUIRED",
            "OUTBOX.AGGREGATE_ID_TOO_LONG",
            "Mã aggregate");
    }

    private void SetEventType(string value)
    {
        EventType = ValidateRequiredText(
            value,
            BusinessRules.Outbox.EventTypeMaxLength,
            "OUTBOX.EVENT_TYPE_REQUIRED",
            "OUTBOX.EVENT_TYPE_TOO_LONG",
            "Loại sự kiện");
    }

    private void SetPayloadJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(
                "OUTBOX.PAYLOAD_REQUIRED",
                "Payload sự kiện không được để trống.");
        }

        PayloadJson = value;
    }

    private static string ValidateRequiredText(
        string value,
        int maximumLength,
        string requiredCode,
        string tooLongCode,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(
                requiredCode,
                $"{fieldName} không được để trống.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maximumLength)
        {
            throw new DomainException(
                tooLongCode,
                $"{fieldName} vượt quá giới hạn.");
        }

        return trimmed;
    }

    private static string TruncateError(string value)
    {
        var trimmed = value.Trim();

        return trimmed.Length <=
               BusinessRules.Outbox.ErrorMaxLength
            ? trimmed
            : trimmed[
                ..BusinessRules.Outbox.ErrorMaxLength];
    }

    private static DateTimeOffset NormalizeUtc(
        DateTimeOffset value)
    {
        if (value == default)
        {
            throw new DomainException(
                "OUTBOX.TIME_REQUIRED",
                "Thời điểm xử lý không được để trống.");
        }

        return value.ToUniversalTime();
    }
}