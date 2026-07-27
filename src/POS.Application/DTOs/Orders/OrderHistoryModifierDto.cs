namespace POS.Application.DTOs.Orders;

public sealed record OrderHistoryModifierDto(
    int ModifierId,
    int ModifierGroupId,
    string ModifierGroupName,
    string ModifierName,
    int Quantity,
    long UnitAdditionalPrice,
    long AmountPerProductUnit);
