using System.Globalization;
using POS.Application.DTOs.Products;

namespace POS.Wpf.ViewModels;

/// <summary>
/// Mô hình hiển thị một dòng sản phẩm.
///
/// DTO Application không chứa logic giao diện.
/// Việc định dạng VND và trạng thái hiển thị nằm tại WPF.
/// </summary>
public sealed class ProductRowViewModel
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo("vi-VN");

    public ProductRowViewModel(
        ProductListItemDto product)
    {
        ArgumentNullException.ThrowIfNull(product);

        Id = product.Id;
        CategoryId = product.CategoryId;
        CategoryName = product.CategoryName;
        Code = product.Code;
        Barcode = product.Barcode;
        Name = product.Name;
        UnitName = product.UnitName;

        CostPrice = product.CostPrice;
        SalePrice = product.SalePrice;
        ProfitPerUnit = product.ProfitPerUnit;

        StockQuantity = product.StockQuantity;
        MinimumStock = product.MinimumStock;

        TrackInventory = product.TrackInventory;
        AllowNegativeStock = product.AllowNegativeStock;
        IsLowStock = product.IsLowStock;
        IsOutOfStock = product.IsOutOfStock;
        IsActive = product.IsActive;
    }

    public int Id { get; }

    public int CategoryId { get; }

    public string CategoryName { get; }

    public string Code { get; }

    public string? Barcode { get; }

    public string Name { get; }

    public string UnitName { get; }

    public long CostPrice { get; }

    public long SalePrice { get; }

    public long ProfitPerUnit { get; }

    public int StockQuantity { get; }

    public int MinimumStock { get; }

    public bool TrackInventory { get; }

    public bool AllowNegativeStock { get; }

    public bool IsLowStock { get; }

    public bool IsOutOfStock { get; }

    public bool IsActive { get; }

    public string SalePriceText =>
        $"{SalePrice.ToString("N0", VietnameseCulture)} ₫";

    public string StockDisplay =>
        TrackInventory
            ? $"{StockQuantity.ToString("N0", VietnameseCulture)} " +
              UnitName
            : "Không theo dõi";

    public string StockStateText
    {
        get
        {
            if (!TrackInventory)
            {
                return "Không theo dõi";
            }

            if (IsOutOfStock)
            {
                return "Hết hàng";
            }

            if (IsLowStock)
            {
                return "Sắp hết";
            }

            return "Ổn định";
        }
    }

    public string StatusText =>
        IsActive
            ? "Đang bán"
            : "Ngừng bán";
}