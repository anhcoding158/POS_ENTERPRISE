namespace POS.Application.DTOs.Checkout;

/// <summary>
/// Modifier/topping đã được lưu trong dòng hóa đơn.
/// </summary>
public sealed record CheckoutLineModifierResultDto(
    int ModifierId,
    int ModifierGroupId,
    string ModifierGroupName,
    string ModifierName,
    int Quantity,
    long UnitAdditionalPrice,
    long AmountPerProductUnit);

/// <summary>
/// Kết quả của một dòng hàng sau khi checkout.
/// </summary>
public sealed record CheckoutLineResultDto(
    int OrderItemId,
    int ProductId,
    string ProductCode,
    string ProductName,
    string UnitName,
    int Quantity,
    long UnitCostPrice,
    long UnitSalePrice,
    long ModifierAmountPerUnit,
    long FinalUnitPrice,
    long GrossAmount,
    long LineDiscountAmount,
    long NetAmount,
    string? Notes,
    IReadOnlyList<CheckoutLineModifierResultDto> Modifiers);