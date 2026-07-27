using POS.Domain.Enums;

namespace POS.Application.DTOs.Checkout;

public sealed record CheckoutPreparationDto(
    Guid ClientRequestId,
    CheckoutRequestStatus Status,
    string RequestFingerprint,
    string PreparedQuoteFingerprint,
    string PreparedQuoteJson,
    int? OrderId);

public sealed record CheckoutRecoveryDto(
    Guid ClientRequestId,
    CheckoutRequestStatus Status,
    DateTimeOffset CreatedAtUtc,
    int? OrderId,
    string? OrderCode,
    long TotalAmount,
    PaymentMethod PaymentMethod,
    IReadOnlyList<CheckoutRecoveryLineDto> Lines,
    CheckoutRequest? PreparedRequest,
    bool CanRetry,
    bool CanAbandon);

public sealed record CheckoutRecoveryLineDto(
    int ProductId,
    string ProductCode,
    string ProductName,
    string UnitName,
    int Quantity,
    long UnitSalePrice,
    long LineTotal);
