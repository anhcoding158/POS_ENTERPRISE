using System.Globalization;
using POS.Application.DTOs.Printing;

namespace POS.Wpf.ViewModels;

public sealed class LabelProductRowViewModel : ViewModelBase
{
    private string _quantityText = "1";
    private string _errorText = string.Empty;

    public LabelProductRowViewModel(ProductRowViewModel product)
    {
        Product = product ?? throw new ArgumentNullException(nameof(product));
    }

    public ProductRowViewModel Product { get; }
    public int ProductId => Product.Id;
    public string ProductName => Product.Name;
    public string ProductCode => Product.Code;
    public string? Barcode => Product.Barcode;
    public string SalePriceText => Product.SalePriceText;
    public string StatusText => Product.StatusText;

    public string QuantityText
    {
        get => _quantityText;
        set
        {
            if (!SetProperty(ref _quantityText, value ?? string.Empty)) return;
            OnPropertyChanged(nameof(QuantityDisplay));
        }
    }

    public string QuantityDisplay =>
        TryGetQuantity(out var quantity) ? quantity.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) : "—";

    public string ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
    }

    public bool TryGetQuantity(out int quantity)
    {
        if (!int.TryParse(QuantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) ||
            quantity <= 0 || quantity > 1000)
        {
            quantity = 0;
            return false;
        }
        return true;
    }

    public LabelProductSnapshot ToSnapshot(int quantity) =>
        new(ProductId, ProductCode, ProductName, Product.SalePrice, Barcode, Product.IsActive, quantity);
}
