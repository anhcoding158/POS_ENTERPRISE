using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        ImagePath = product.ImagePath;
        ProductImage =
            LoadProductThumbnail(
                ImagePath);

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
        IsArchived =
            product.IsArchived;

        AddToCartCommand =
            new AsyncRelayCommand(
                () =>
                    addToCartAsync(this),

                () =>
                    CanSell);
    }

    public SalesProductCardViewModel(
        SalesCatalogProductDto product,
        Func<SalesProductCardViewModel, Task> addToCartAsync)
        : this(
            new ProductListItemDto(
                product.Id,
                product.CategoryId,
                product.CategoryName,
                product.Code,
                product.Barcode,
                product.Name,
                product.UnitName,
                0,
                product.SalePrice,
                0,
                product.StockQuantity,
                product.MinimumStock,
                product.TrackInventory,
                product.AllowNegativeStock,
                product.IsLowStock,
                product.IsOutOfStock,
                product.IsActive,
                product.IsArchived)
            {
                ImagePath = product.ImagePath
            },
            addToCartAsync)
    {
    }

    public int ProductId { get; }

    public int CategoryId { get; }

    public string CategoryName { get; }

    public string Code { get; }

    public string? Barcode { get; }

    public string Name { get; }

    public string UnitName { get; }

    public string? ImagePath { get; }

    public BitmapSource? ProductImage { get; }

    public bool HasProductImage =>
        ProductImage is not null;

    public long SalePrice { get; }

    public int StockQuantity { get; }

    public int MinimumStock { get; }

    public bool TrackInventory { get; }

    public bool AllowNegativeStock { get; }

    public bool IsLowStock { get; }

    public bool IsOutOfStock { get; }

    public bool IsActive { get; }
    public bool IsArchived { get; }

    public AsyncRelayCommand
        AddToCartCommand
    {
        get;
    }

    public bool CanSell =>
        IsActive &&
        !IsArchived &&
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

    private static BitmapImage?
        LoadProductThumbnail(
            string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(
                imagePath) ||
            !File.Exists(
                imagePath))
        {
            return null;
        }

        try
        {
            using var stream =
                new FileStream(
                    imagePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite |
                    FileShare.Delete);

            var bitmap =
                new BitmapImage();

            bitmap.BeginInit();

            bitmap.CacheOption =
                BitmapCacheOption.OnLoad;

            bitmap.DecodePixelWidth =
                144;

            bitmap.StreamSource =
                stream;

            bitmap.EndInit();

            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }

            return bitmap;
        }
        catch (Exception exception)
            when (
                exception is PathTooLongException or
                IOException or
                UnauthorizedAccessException or
                NotSupportedException or
                FileFormatException or
                ArgumentException)
        {
            return null;
        }
    }
}
