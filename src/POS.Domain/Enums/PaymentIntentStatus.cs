namespace POS.Domain.Enums;

public enum PaymentIntentStatus
{
    Created = 1,
    Presented = 2,
    Confirmed = 3,
    Completed = 4,
    Cancelled = 5,
    Expired = 6
}
