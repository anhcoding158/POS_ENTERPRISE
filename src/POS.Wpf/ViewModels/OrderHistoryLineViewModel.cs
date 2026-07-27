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
    public string FinalUnitPriceText =>
        FinalUnitPrice.ToString("N0", VietnameseCulture) + " ₫";
    public string NetAmountText =>
        NetAmount.ToString("N0", VietnameseCulture) + " ₫";
}
