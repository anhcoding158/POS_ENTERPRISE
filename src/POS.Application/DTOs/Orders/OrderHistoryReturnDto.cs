using POS.Domain.Enums;

namespace POS.Application.DTOs.Orders;

public sealed record OrderHistoryReturnDto(
    DateTimeOffset CreatedAtUtc,
    string ProcessedBy,
    int ReturnedQuantity,
    long RefundedAmount,
    string Reason,
    PaymentMethod RefundMethod);
