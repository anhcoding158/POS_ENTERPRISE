using POS.Application.DTOs.Checkout;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Payments;

public sealed record CreatePaymentIntentRequest(
    Guid ClientRequestId,
    CheckoutRequest Checkout);

public sealed record PaymentIntentDto(
    int Id,
    string DisplayCode,
    PaymentIntentStatus Status,
    long Amount,
    string Currency,
    string TransferContent,
    string PayloadText,
    string BankCode,
    string AccountNumber,
    string AccountName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    int? HeldSaleId,
    int? CompletedOrderId,
    bool IsReplay);

public sealed record PaymentIntentPendingDto(
    int Id,
    string DisplayCode,
    PaymentIntentStatus Status,
    long Amount,
    string Currency,
    string TransferContent,
    string PayloadText,
    string BankCode,
    string AccountNumber,
    string AccountName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    int? HeldSaleId,
    bool CanPresent,
    bool CanShowQr,
    bool CanConfirm,
    bool CanCancel,
    bool CanRetryCheckout,
    bool IsStale,
    string? Warning)
{
    public DateTimeOffset? ConfirmedAtUtc { get; init; }
    public int? ConfirmedByUserId { get; init; }
    public bool IsCrossPaymentConflict { get; init; }
    public int? ConflictingOrderId { get; init; }
    public string? ConflictingOrderCode { get; init; }
    public PaymentMethod? ConflictingPaymentMethod { get; init; }
}

public sealed record ResolvePaymentIntentManuallyRequest(
    int PaymentIntentId,
    PaymentIntentManualResolutionType ResolutionType,
    string Reason,
    string? ExternalReference = null,
    int? LinkedOrderId = null);

public sealed record PaymentIntentManualResolutionDto(
    int Id,
    int PaymentIntentId,
    string DisplayCode,
    PaymentIntentManualResolutionType ResolutionType,
    DateTimeOffset ResolvedAtUtc,
    int ResolvedByUserId,
    string Reason,
    string? ExternalReference,
    int? LinkedOrderId,
    string? LinkedOrderCode,
    long Amount);
