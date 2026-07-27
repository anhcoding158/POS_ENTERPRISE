using POS.Domain.Enums;

namespace POS.Application.DTOs.Orders;

public sealed record OrderHistorySearchRequest(
    string? SearchTerm = null,
    OrderStatus? Status = null,
    PaymentMethod? PaymentMethod = null,
    int? CashierUserId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 25);
