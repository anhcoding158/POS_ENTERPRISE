using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class PaymentIntentManualResolution : Entity
{
    private PaymentIntentManualResolution() { }

    public PaymentIntentManualResolution(
        int paymentIntentId,
        PaymentIntentManualResolutionType resolutionType,
        int resolvedByUserId,
        string reason,
        DateTimeOffset resolvedAtUtc,
        string? externalReference = null,
        int? linkedOrderId = null)
    {
        if (paymentIntentId <= 0 || resolvedByUserId <= 0 || resolvedAtUtc == default)
            throw new DomainException("PAYMENT_INTENT_RESOLUTION.INVALID_IDENTITY", "Thông tin xử lý không hợp lệ.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("PAYMENT_INTENT_RESOLUTION.REASON_REQUIRED", "Lý do xử lý là bắt buộc.");
        if (!Enum.IsDefined(resolutionType))
            throw new DomainException("PAYMENT_INTENT_RESOLUTION.TYPE_INVALID", "Loại xử lý không hợp lệ.");
        if (resolutionType == PaymentIntentManualResolutionType.LinkExistingOrder && linkedOrderId is null)
            throw new DomainException("PAYMENT_INTENT_RESOLUTION.ORDER_REQUIRED", "Phải chọn chính xác hóa đơn.");
        if (resolutionType == PaymentIntentManualResolutionType.RefundedExternally &&
            string.IsNullOrWhiteSpace(externalReference))
            throw new DomainException("PAYMENT_INTENT_RESOLUTION.EXTERNAL_REFERENCE_REQUIRED", "Mã hoặc ghi chú hoàn tiền ngoài POS là bắt buộc.");
        if (resolutionType != PaymentIntentManualResolutionType.LinkExistingOrder && linkedOrderId is not null)
            throw new DomainException("PAYMENT_INTENT_RESOLUTION.ORDER_NOT_ALLOWED", "Loại xử lý này không được liên kết hóa đơn.");

        PaymentIntentId = paymentIntentId;
        ResolutionType = resolutionType;
        ResolvedByUserId = resolvedByUserId;
        Reason = reason.Trim();
        ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim();
        LinkedOrderId = linkedOrderId;
        ResolvedAtUtc = resolvedAtUtc.ToUniversalTime();
    }

    public int PaymentIntentId { get; private set; }
    public PaymentIntentManualResolutionType ResolutionType { get; private set; }
    public DateTimeOffset ResolvedAtUtc { get; private set; }
    public int ResolvedByUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? ExternalReference { get; private set; }
    public int? LinkedOrderId { get; private set; }
    public PaymentIntent? PaymentIntent { get; private set; }
    public User? ResolvedByUser { get; private set; }
    public Order? LinkedOrder { get; private set; }
}
