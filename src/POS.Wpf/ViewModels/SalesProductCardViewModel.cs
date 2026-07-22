using System.Globalization;
using POS.Application.DTOs.Products;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// Thẻ sản phẩm dùng trên catalog bán hàng.
///
/// Đây chỉ là mô hình hiển thị.
/// Giá chính thức vẫn do CheckoutService đọc từ database.
/// </summary>
public sealed class SalesProductCardViewModel
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    public SalesProductCardViewModel(
        ProductListItemDto product,
        Func<
            SalesProductCardViewModel,
            Task> addToCartAsync)
    {
        ArgumentNullException.ThrowIfNull(
            product);

        ArgumentNullException.ThrowIfNull(
            addToCartAsync);

        ProductId = product.Id;
        CategoryId = product.CategoryId;
        CategoryName = product.CategoryName;

        Code = product.Code;
        Barcode = product.Barcode;
        Name = product.Name;
        UnitName = product.UnitName;

        SalePrice = product.SalePrice;

        StockQuantity =
            product.StockQuantity;

        MinimumStock =
            product.MinimumStock;

        TrackInventory =
            product.TrackInventory;

        AllowNegativeStock =
            product.AllowNegativeStock;

        IsLowStock =
            product.IsLowStock;

        IsOutOfStock =
            product.IsOutOfStock;

        IsActive =
            product.IsActive;

        AddToCartCommand =
            new AsyncRelayCommand(
                () =>
                    addToCartAsync(this),

                () =>
                    CanSell);
    }

    public int ProductId { get; }

    public int CategoryId { get; }

    public string CategoryName { get; }

    public string Code { get; }

    public string? Barcode { get; }

    public string Name { get; }

    public string UnitName { get; }

    public long SalePrice { get; }

    public int StockQuantity { get; }

    public int MinimumStock { get; }

    public bool TrackInventory { get; }

    public bool AllowNegativeStock { get; }

    public bool IsLowStock { get; }

    public bool IsOutOfStock { get; }

    public bool IsActive { get; }

    public AsyncRelayCommand
        AddToCartCommand
    {
        get;
    }

    public bool CanSell =>
        IsActive &&
        (
            !TrackInventory ||
            AllowNegativeStock ||
            StockQuantity > 0
        );

    public string SalePriceText =>
        $"{SalePrice.ToString(
            "N0",
            VietnameseCulture)} ₫";

    public string ProductInitial
    {
        get
        {
            var normalized =
                Name.Trim();

            return normalized.Length == 0
                ? "P"
                : normalized[0]
                    .ToString()
                    .ToUpper(
                        VietnameseCulture);
        }
    }

    public string AvailabilityText
    {
        get
        {
            if (!IsActive)
            {
                return "Sản phẩm đang ngừng bán";
            }

            if (!TrackInventory)
            {
                return "Luôn sẵn sàng";
            }

            if (AllowNegativeStock)
            {
                return "Cho phép bán không giới hạn tồn";
            }

            if (StockQuantity <= 0)
            {
                return "Hết hàng";
            }

            if (IsLowStock)
            {
                return
                    $"Sắp hết • còn " +
                    $"{StockQuantity:N0} {UnitName}";
            }

            return
                $"Còn {StockQuantity:N0} {UnitName}";
        }
    }

    public string AddButtonText =>
        CanSell
            ? "Thêm vào đơn"
            : "Không thể bán";
}