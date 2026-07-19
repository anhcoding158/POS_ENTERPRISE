using POS.Domain.Common;
using POS.Domain.Constants;

namespace POS.Domain.Entities;

/// <summary>
/// Sản phẩm được bán và theo dõi tồn kho.
/// Tiền sử dụng long vì VND được lưu theo đơn vị đồng.
/// </summary>
public sealed class Product : AuditableEntity
{
    private Product()
    {
    }

    public Product(
        int categoryId,
        string code,
        string name,
        string unitName,
        long costPrice,
        long salePrice,
        int stockQuantity,
        int minimumStock,
        bool trackInventory,
        bool allowNegativeStock,
        DateTimeOffset utcNow,
        string? barcode = null,
        string? description = null,
        string? imagePath = null)
    {
        SetCategoryId(categoryId);
        SetCode(code);
        SetBarcode(barcode);
        SetName(name);
        SetDescription(description);
        SetUnitName(unitName);
        SetImagePath(imagePath);
        SetPrices(costPrice, salePrice);

        ConfigureInventoryInternal(
            stockQuantity,
            minimumStock,
            trackInventory,
            allowNegativeStock);

        IsActive = true;

        MarkCreated(utcNow);
    }

    public int CategoryId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string? Barcode { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string UnitName { get; private set; } = string.Empty;

    public string? ImagePath { get; private set; }

    public long CostPrice { get; private set; }

    public long SalePrice { get; private set; }

    public int StockQuantity { get; private set; }

    public int MinimumStock { get; private set; }

    public bool TrackInventory { get; private set; }

    public bool AllowNegativeStock { get; private set; }

    public bool IsActive { get; private set; }

    public Category? Category { get; private set; }

    public long ProfitPerUnit =>
        SalePrice - CostPrice;

    public bool IsOutOfStock =>
        TrackInventory &&
        StockQuantity <= 0;

    public bool IsLowStock =>
        TrackInventory &&
        StockQuantity <= MinimumStock;

    public bool CanFulfill(int quantity)
    {
        if (quantity <= 0)
        {
            return false;
        }

        if (!TrackInventory ||
            AllowNegativeStock)
        {
            return true;
        }

        return StockQuantity >= quantity;
    }

    public void UpdateDetails(
        int categoryId,
        string code,
        string? barcode,
        string name,
        string? description,
        string unitName,
        string? imagePath,
        DateTimeOffset utcNow)
    {
        SetCategoryId(categoryId);
        SetCode(code);
        SetBarcode(barcode);
        SetName(name);
        SetDescription(description);
        SetUnitName(unitName);
        SetImagePath(imagePath);

        MarkUpdated(utcNow);
    }

    public void ChangePrices(
        long costPrice,
        long salePrice,
        DateTimeOffset utcNow)
    {
        SetPrices(costPrice, salePrice);
        MarkUpdated(utcNow);
    }

    public void ConfigureInventory(
        int minimumStock,
        bool trackInventory,
        bool allowNegativeStock,
        DateTimeOffset utcNow)
    {
        ConfigureInventoryInternal(
            StockQuantity,
            minimumStock,
            trackInventory,
            allowNegativeStock);

        MarkUpdated(utcNow);
    }

    public void IncreaseStock(
        int quantity,
        DateTimeOffset utcNow)
    {
        EnsureInventoryTrackingEnabled();

        if (quantity <= 0)
        {
            throw new DomainException(
                "PRODUCT.INVALID_STOCK_INCREASE",
                "Số lượng nhập kho phải lớn hơn 0.");
        }

        if (quantity >
            BusinessRules.Products.MaximumStockQuantity -
            StockQuantity)
        {
            throw new DomainException(
                "PRODUCT.STOCK_OVERFLOW",
                "Tồn kho vượt quá giới hạn hệ thống.");
        }

        StockQuantity += quantity;

        MarkUpdated(utcNow);
    }

    public void DecreaseStock(
        int quantity,
        DateTimeOffset utcNow)
    {
        if (!TrackInventory)
        {
            return;
        }

        if (quantity <= 0)
        {
            throw new DomainException(
                "PRODUCT.INVALID_STOCK_DECREASE",
                "Số lượng xuất kho phải lớn hơn 0.");
        }

        var remaining = StockQuantity - quantity;

        if (!AllowNegativeStock && remaining < 0)
        {
            throw new DomainException(
                "PRODUCT.INSUFFICIENT_STOCK",
                $"Sản phẩm {Name} không đủ tồn kho.");
        }

        if (remaining <
            -BusinessRules.Products.MaximumStockQuantity)
        {
            throw new DomainException(
                "PRODUCT.STOCK_UNDERFLOW",
                "Tồn kho âm vượt quá giới hạn hệ thống.");
        }

        StockQuantity = remaining;

        MarkUpdated(utcNow);
    }

    public void ReconcileStock(
        int actualQuantity,
        DateTimeOffset utcNow)
    {
        EnsureInventoryTrackingEnabled();

        ValidateStockQuantity(
            actualQuantity,
            AllowNegativeStock);

        StockQuantity = actualQuantity;

        MarkUpdated(utcNow);
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        MarkUpdated(utcNow);
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        MarkUpdated(utcNow);
    }

    private void SetCategoryId(int categoryId)
    {
        if (categoryId <= 0)
        {
            throw new DomainException(
                "PRODUCT.INVALID_CATEGORY_ID",
                "Danh mục sản phẩm không hợp lệ.");
        }

        CategoryId = categoryId;
    }

    private void SetCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException(
                "PRODUCT.CODE_REQUIRED",
                "Mã sản phẩm không được để trống.");
        }

        var normalized = code
            .Trim()
            .ToUpperInvariant();

        if (normalized.Length >
            BusinessRules.Products.CodeMaxLength)
        {
            throw new DomainException(
                "PRODUCT.CODE_TOO_LONG",
                "Mã sản phẩm vượt quá giới hạn.");
        }

        Code = normalized;
    }

    private void SetBarcode(string? barcode)
    {
        var normalized = string.IsNullOrWhiteSpace(barcode)
            ? null
            : barcode.Trim();

        if (normalized?.Length >
            BusinessRules.Products.BarcodeMaxLength)
        {
            throw new DomainException(
                "PRODUCT.BARCODE_TOO_LONG",
                "Mã vạch vượt quá giới hạn.");
        }

        Barcode = normalized;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(
                "PRODUCT.NAME_REQUIRED",
                "Tên sản phẩm không được để trống.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length >
            BusinessRules.Products.NameMaxLength)
        {
            throw new DomainException(
                "PRODUCT.NAME_TOO_LONG",
                "Tên sản phẩm vượt quá giới hạn.");
        }

        Name = trimmed;
    }

    private void SetDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalized?.Length >
            BusinessRules.Products.DescriptionMaxLength)
        {
            throw new DomainException(
                "PRODUCT.DESCRIPTION_TOO_LONG",
                "Mô tả sản phẩm vượt quá giới hạn.");
        }

        Description = normalized;
    }

    private void SetUnitName(string unitName)
    {
        if (string.IsNullOrWhiteSpace(unitName))
        {
            throw new DomainException(
                "PRODUCT.UNIT_REQUIRED",
                "Đơn vị tính không được để trống.");
        }

        var trimmed = unitName.Trim();

        if (trimmed.Length >
            BusinessRules.Products.UnitNameMaxLength)
        {
            throw new DomainException(
                "PRODUCT.UNIT_TOO_LONG",
                "Đơn vị tính vượt quá giới hạn.");
        }

        UnitName = trimmed;
    }

    private void SetImagePath(string? imagePath)
    {
        var normalized = string.IsNullOrWhiteSpace(imagePath)
            ? null
            : imagePath.Trim();

        if (normalized?.Length >
            BusinessRules.Products.ImagePathMaxLength)
        {
            throw new DomainException(
                "PRODUCT.IMAGE_PATH_TOO_LONG",
                "Đường dẫn ảnh vượt quá giới hạn.");
        }

        ImagePath = normalized;
    }

    private void SetPrices(
        long costPrice,
        long salePrice)
    {
        ValidatePrice(
            costPrice,
            "PRODUCT.INVALID_COST_PRICE",
            "Giá vốn");

        ValidatePrice(
            salePrice,
            "PRODUCT.INVALID_SALE_PRICE",
            "Giá bán");

        CostPrice = costPrice;
        SalePrice = salePrice;
    }

    private void ConfigureInventoryInternal(
        int stockQuantity,
        int minimumStock,
        bool trackInventory,
        bool allowNegativeStock)
    {
        if (minimumStock < 0 ||
            minimumStock >
            BusinessRules.Products.MaximumStockQuantity)
        {
            throw new DomainException(
                "PRODUCT.INVALID_MINIMUM_STOCK",
                "Mức tồn kho tối thiểu không hợp lệ.");
        }

        ValidateStockQuantity(
            stockQuantity,
            allowNegativeStock);

        TrackInventory = trackInventory;
        AllowNegativeStock =
            trackInventory && allowNegativeStock;

        MinimumStock = trackInventory
            ? minimumStock
            : 0;

        StockQuantity = stockQuantity;
    }

    private void EnsureInventoryTrackingEnabled()
    {
        if (!TrackInventory)
        {
            throw new DomainException(
                "PRODUCT.INVENTORY_NOT_TRACKED",
                "Sản phẩm này không theo dõi tồn kho.");
        }
    }

    private static void ValidateStockQuantity(
        int quantity,
        bool allowNegativeStock)
    {
        if (quantity >
            BusinessRules.Products.MaximumStockQuantity)
        {
            throw new DomainException(
                "PRODUCT.STOCK_TOO_LARGE",
                "Tồn kho vượt quá giới hạn.");
        }

        if (!allowNegativeStock && quantity < 0)
        {
            throw new DomainException(
                "PRODUCT.NEGATIVE_STOCK_NOT_ALLOWED",
                "Sản phẩm không cho phép tồn kho âm.");
        }

        if (quantity <
            -BusinessRules.Products.MaximumStockQuantity)
        {
            throw new DomainException(
                "PRODUCT.STOCK_TOO_LOW",
                "Tồn kho âm vượt quá giới hạn.");
        }
    }

    private static void ValidatePrice(
        long value,
        string code,
        string fieldName)
    {
        if (value < 0 ||
            value > BusinessRules.Products.MaximumPrice)
        {
            throw new DomainException(
                code,
                $"{fieldName} không hợp lệ.");
        }
    }
}