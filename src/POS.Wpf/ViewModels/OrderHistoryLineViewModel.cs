using System.Globalization;
using POS.Application.DTOs.Orders;

namespace POS.Wpf.ViewModels;

public sealed class OrderHistoryLineViewModel
{
    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    public OrderHistoryLineViewModel(OrderHistoryLineDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ProductCode = source.ProductCode;
        ProductName = source.ProductName;
        UnitName = source.UnitName;
        Quantity = source.Quantity;
        FinalUnitPrice = source.FinalUnitPrice;
        NetAmount = source.NetAmount;
        Notes = source.Notes;
        LineDiscountAmount = source.LineDiscountAmount;
        ReturnedQuantity = source.ReturnedQuantity;
        RefundedAmount = source.RefundedAmount;
        ModifierSummary = string.Join(
            ", ",
            source.Modifiers.Select(modifier =>
                $"{modifier.ModifierName} x{modifier.Quantity}"));
    }

    public string ProductCode { get; }
    public string ProductName { get; }
    public string UnitName { get; }
    public int Quantity { get; }
    public long FinalUnitPrice { get; }
    public long NetAmount { get; }
    public string? Notes { get; }
    public string ModifierSummary { get; }
    public long LineDiscountAmount { get; }
    public int ReturnedQuantity { get; }
    public long RefundedAmount { get; }
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
    public bool HasModifiers => !string.IsNullOrWhiteSpace(ModifierSummary);
    public string LineDetailsText
    {
        get
        {
            var parts = new List<string>();
            if (LineDiscountAmount > 0) parts.Add($"Giảm {LineDiscountAmount.ToString("N0", VietnameseCulture)} ₫");
            if (ReturnedQuantity > 0)
            {
                var returnState = ReturnedQuantity >= Quantity ? "Đã trả toàn bộ" : "Đã trả";
                parts.Add($"{returnState} {ReturnedQuantity:N0}/{Quantity:N0} · hoàn {RefundedAmount.ToString("N0", VietnameseCulture)} ₫");
            }
            if (HasModifiers) parts.Add($"Tùy chọn: {ModifierSummary}");
            if (HasNotes) parts.Add($"Ghi chú: {Notes}");
            return parts.Count == 0 ? "—" : string.Join("\n", parts);
        }
    }
    public string FinalUnitPriceText =>
        FinalUnitPrice.ToString("N0", VietnameseCulture) + " ₫";
    public string NetAmountText =>
        NetAmount.ToString("N0", VietnameseCulture) + " ₫";
}
