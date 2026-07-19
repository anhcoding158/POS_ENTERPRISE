namespace POS.Application.DTOs.Printing;

/// <summary>
/// Modifier hoặc topping được in dưới một dòng sản phẩm.
/// </summary>
public sealed record ReceiptModifierDto(
    string Name,
    int Quantity,
    long UnitAdditionalPrice,
    long AmountPerProductUnit);

/// <summary>
/// Một dòng sản phẩm trên hóa đơn.
///
/// Giá vốn không được đưa vào DTO hóa đơn vì đây là
/// thông tin nội bộ của cửa hàng.
/// </summary>
public sealed record ReceiptLineDto(
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
    IReadOnlyList<ReceiptModifierDto> Modifiers);