namespace POS.Application.DTOs.Checkout;

/// <summary>
/// Một modifier/topping được chọn cho sản phẩm.
/// </summary>
public sealed record CheckoutModifierRequest(
    int ModifierId,
    int Quantity);

/// <summary>
/// Một dòng sản phẩm được gửi tới CheckoutService.
///
/// Giá sản phẩm và giá modifier tuyệt đối không lấy từ WPF.
/// CheckoutService phải đọc lại từ database để chống sửa giá.
/// </summary>
public sealed class CheckoutLineRequest
{
    public CheckoutLineRequest(
        int productId,
        int quantity,
        IEnumerable<CheckoutModifierRequest>? modifiers = null,
        long lineDiscountAmount = 0,
        string? notes = null)
    {
        ProductId = productId;
        Quantity = quantity;

        Modifiers = modifiers?
            .ToArray()
            ?? Array.Empty<CheckoutModifierRequest>();

        LineDiscountAmount = lineDiscountAmount;

        Notes = string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim();
    }

    public int ProductId { get; }

    public int Quantity { get; }

    public IReadOnlyList<CheckoutModifierRequest> Modifiers { get; }

    public long LineDiscountAmount { get; }

    public string? Notes { get; }
}