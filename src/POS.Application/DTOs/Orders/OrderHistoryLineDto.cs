namespace POS.Application.DTOs.Orders;

public sealed record OrderHistoryLineDto(
    int OrderItemId,
    int ProductId,
    string ProductCode,
    string ProductName,
    string UnitName,
    int Quantity,
    long UnitSalePrice,
    long ModifierAmountPerUnit,
    long FinalUnitPrice,
    long GrossAmount,
    long LineDiscountAmount,
    long NetAmount,
    string? Notes,
    IReadOnlyList<OrderHistoryModifierDto> Modifiers,
    int ReturnedQuantity = 0,
    long RefundedAmount = 0);
