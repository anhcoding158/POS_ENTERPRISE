using POS.Domain.Enums;

namespace POS.Application.DTOs.Orders;

public sealed record OrderHistorySearchRequest(
    string? SearchTerm = null,
    OrderHistoryStatus? Status = null,
    PaymentMethod? PaymentMethod = null,
    int? CashierUserId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 25);

public enum OrderHistoryStatus
{
    Completed = 1,
    PartiallyReturned = 2,
    FullyReturned = 3
}
