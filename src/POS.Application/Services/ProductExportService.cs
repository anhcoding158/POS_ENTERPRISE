using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Exports;
using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

/// <summary>
/// Read-only export use case. Every report is materialized from one bounded,
/// ordered repository query before a writer is given any data.
/// </summary>
public sealed class ProductExportService : IProductExportService
{
    public const int MaximumRows = 100_000;

    private readonly IProductRepository _productRepository;
    private readonly IInventoryMovementRepository _movementRepository;
    private readonly IPermissionService _permissionService;

    public ProductExportService(
        IProductRepository productRepository,
        IInventoryMovementRepository movementRepository,
        IPermissionService permissionService)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _movementRepository = movementRepository ?? throw new ArgumentNullException(nameof(movementRepository));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    public async Task<Result<ProductExportData>> ExportAsync(
        ProductExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var authorization = _permissionService.Authorize(
            request.ReportType == ProductExportReportType.InventoryHistory
                ? SystemCapability.ViewInventoryHistory
                : request.ReportType == ProductExportReportType.ProductImportTemplate
                    ? SystemCapability.ManageProducts
                    : SystemCapability.ViewProductCatalog);

        if (authorization.IsFailure)
        {
            return Result.Failure<ProductExportData>(authorization.AppError);
        }

        return request.ReportType == ProductExportReportType.InventoryHistory
            ? await ExportHistoryAsync(request.HistoryFilters, cancellationToken)
            : request.ReportType == ProductExportReportType.ProductImportTemplate
                ? Result.Success(CreateTemplate())
                : await ExportProductsAsync(request, cancellationToken);
    }

    private async Task<Result<ProductExportData>> ExportProductsAsync(
        ProductExportRequest request,
        CancellationToken cancellationToken)
    {
        var filters = request.ProductFilters ?? new ProductSearchRequest();
        var isArchived = request.ReportType == ProductExportReportType.ArchivedProducts
            ? true
            : filters.IsArchived;
        var isLowStock = request.ReportType == ProductExportReportType.LowStock
            ? true
            : filters.IsLowStock;

        var products = await _productRepository.ExportAsync(
            filters.SearchTerm,
            filters.CategoryId,
            filters.IsActive,
            isLowStock,
            isArchived,
            MaximumRows + 1,
            cancellationToken);

        if (products.Count > MaximumRows)
        {
            return TooManyRows<ProductExportData>();
        }

        var includeCost = _permissionService.HasPermission(SystemCapability.ManageProducts);
        var columns = ProductColumns(request.ReportType, includeCost);
        var rows = products.Select(product => ProductRow(product, request.ReportType, includeCost)).ToArray();

        return Result.Success(new ProductExportData(
            request.ReportType,
            columns,
            rows,
            rows.Length,
            includeCost,
            SuggestedName(request.ReportType),
            ["Dữ liệu được xuất theo phạm vi và quyền hiện tại."]));
    }

    private async Task<Result<ProductExportData>> ExportHistoryAsync(
        POS.Application.DTOs.Inventory.InventorySearchRequest? filters,
        CancellationToken cancellationToken)
    {
        var effectiveFilters = filters ?? new POS.Application.DTOs.Inventory.InventorySearchRequest();
        var movements = await _movementRepository.ExportAsync(
            effectiveFilters.ProductId,
            effectiveFilters.MovementType,
            effectiveFilters.FromUtc,
            effectiveFilters.ToUtc,
            effectiveFilters.ReferenceType,
            effectiveFilters.ProductSearchTerm,
            MaximumRows + 1,
            cancellationToken);

        if (movements.Count > MaximumRows)
        {
            return TooManyRows<ProductExportData>();
        }

        var columns = new[]
        {
            new ProductExportColumn("occurred_at", "Thời gian", ProductExportCellType.DateTime),
            new ProductExportColumn("product_code", "Mã sản phẩm", ProductExportCellType.Text),
            new ProductExportColumn("product_name", "Tên sản phẩm", ProductExportCellType.Text),
            new ProductExportColumn("movement_type", "Loại thay đổi", ProductExportCellType.Text),
            new ProductExportColumn("quantity_before", "Tồn trước", ProductExportCellType.Number),
            new ProductExportColumn("quantity_delta", "Thay đổi", ProductExportCellType.Number),
            new ProductExportColumn("quantity_after", "Tồn sau", ProductExportCellType.Number),
            new ProductExportColumn("unit", "Đơn vị", ProductExportCellType.Text),
            new ProductExportColumn("reason", "Lý do", ProductExportCellType.Text),
            new ProductExportColumn("reference_type", "Nguồn giao dịch", ProductExportCellType.Text),
            new ProductExportColumn("reference_id", "Mã chứng từ", ProductExportCellType.Text),
            new ProductExportColumn("performed_by", "Người thực hiện", ProductExportCellType.Number)
        };

        var rows = movements.Select(movement => new ProductExportRow(
            [
                ProductExportCell.DateTimeValue(movement.OccurredAtUtc),
                ProductExportCell.TextValue(movement.Product?.Code),
                ProductExportCell.TextValue(movement.Product?.Name),
                ProductExportCell.TextValue(MovementLabel(movement.MovementType)),
                ProductExportCell.NumberValue(movement.QuantityBefore),
                ProductExportCell.NumberValue(movement.QuantityDelta),
                ProductExportCell.NumberValue(movement.QuantityAfter),
                ProductExportCell.TextValue(movement.Product?.UnitName),
                ProductExportCell.TextValue(movement.Reason),
                ProductExportCell.TextValue(movement.ReferenceType),
                ProductExportCell.TextValue(movement.ReferenceId),
                movement.PerformedByUserId.HasValue
                    ? ProductExportCell.NumberValue(movement.PerformedByUserId.Value)
                    : ProductExportCell.TextValue(string.Empty)
            ])).ToArray();

        return Result.Success(new ProductExportData(
            ProductExportReportType.InventoryHistory,
            columns,
            rows,
            rows.Length,
            false,
            "lich-su-ton-kho",
            ["Lịch sử được xuất theo đúng bộ lọc thời gian, loại thay đổi, nguồn và quyền hiện tại."]));
    }

    private ProductExportData CreateTemplate() => new(
        ProductExportReportType.ProductImportTemplate,
        ProductImportSchemaColumns(),
        [],
        0,
        _permissionService.HasPermission(SystemCapability.ManageProducts),
        "mau-nhap-san-pham",
        [
            "Điền một sản phẩm trên mỗi dòng; Mã sản phẩm, Tên sản phẩm, Danh mục, Đơn vị tính, Giá bán và Giá vốn là bắt buộc.",
            "Danh mục phải tồn tại và đang hoạt động; không tự tạo danh mục hoặc đơn vị tính."
        ]);

    private static ProductExportColumn[] ProductImportSchemaColumns() =>
        POS.Application.ProductImports.ProductImportSchemaCatalog.Fields
            .Select(field => new ProductExportColumn(
                field.CanonicalKey,
                field.VietnameseLabel,
                field.DataType is POS.Application.DTOs.ProductImports.ProductImportFieldType.VndAmount or POS.Application.DTOs.ProductImports.ProductImportFieldType.NonNegativeInteger
                    ? ProductExportCellType.Number
                    : ProductExportCellType.Text))
            .ToArray();

    private static List<ProductExportColumn> ProductColumns(
        ProductExportReportType reportType,
        bool includeCost)
    {
        var columns = new List<ProductExportColumn>
        {
            new("product_code", "Mã sản phẩm", ProductExportCellType.Text),
            new("barcode", "Mã vạch", ProductExportCellType.Text),
            new("name", "Tên sản phẩm", ProductExportCellType.Text),
            new("category", "Danh mục", ProductExportCellType.Text),
            new("unit", "Đơn vị", ProductExportCellType.Text)
        };

        if (reportType is ProductExportReportType.ProductCatalog)
        {
            columns.Add(new("sale_price", "Giá bán (VND)", ProductExportCellType.Number));
        }

        if (includeCost && reportType is ProductExportReportType.ProductCatalog or ProductExportReportType.CurrentStock)
        {
            columns.Add(new("cost_price", "Giá vốn (VND)", ProductExportCellType.Number));
        }

        if (reportType is ProductExportReportType.CurrentStock or ProductExportReportType.LowStock or ProductExportReportType.ArchivedProducts)
        {
            columns.Add(new("stock_quantity", "Tồn hiện tại", ProductExportCellType.Number));
            columns.Add(new("minimum_stock", "Tồn tối thiểu", ProductExportCellType.Number));
            columns.Add(new("track_inventory", "Theo dõi tồn", ProductExportCellType.Text));
        }

        columns.Add(new("status", "Trạng thái", ProductExportCellType.Text));
        columns.Add(new("archived", "Lưu trữ", ProductExportCellType.Text));
        return columns;
    }

    private static ProductExportRow ProductRow(Product product, ProductExportReportType reportType, bool includeCost)
    {
        var cells = new List<ProductExportCell>
        {
            ProductExportCell.TextValue(product.Code),
            ProductExportCell.TextValue(product.Barcode),
            ProductExportCell.TextValue(product.Name),
            ProductExportCell.TextValue(product.Category?.Name),
            ProductExportCell.TextValue(product.UnitName)
        };

        if (reportType is ProductExportReportType.ProductCatalog)
        {
            cells.Add(ProductExportCell.NumberValue(product.SalePrice));
        }

        if (includeCost && reportType is ProductExportReportType.ProductCatalog or ProductExportReportType.CurrentStock)
        {
            cells.Add(ProductExportCell.NumberValue(product.CostPrice));
        }

        if (reportType is ProductExportReportType.CurrentStock or ProductExportReportType.LowStock or ProductExportReportType.ArchivedProducts)
        {
            cells.Add(ProductExportCell.NumberValue(product.StockQuantity));
            cells.Add(ProductExportCell.NumberValue(product.MinimumStock));
            cells.Add(ProductExportCell.TextValue(product.TrackInventory ? "Có" : "Không"));
        }

        cells.Add(ProductExportCell.TextValue(product.IsActive ? "Đang bán" : "Ngừng bán"));
        cells.Add(ProductExportCell.TextValue(product.IsArchived ? "Đã lưu trữ" : "Chưa lưu trữ"));
        return new ProductExportRow(cells);
    }

    private static string SuggestedName(ProductExportReportType reportType) => reportType switch
    {
        ProductExportReportType.ProductCatalog => "danh-sach-san-pham",
        ProductExportReportType.CurrentStock => "ton-hien-tai",
        ProductExportReportType.LowStock => "san-pham-duoi-nguong-ton",
        ProductExportReportType.ArchivedProducts => "san-pham-da-luu-tru",
        _ => "san-pham"
    };

    private static string MovementLabel(InventoryMovementType value) => value switch
    {
        InventoryMovementType.Sale => "Bán hàng",
        InventoryMovementType.Refund => "Hoàn hàng",
        InventoryMovementType.OpeningBalance => "Tồn đầu kỳ",
        InventoryMovementType.Stocktake => "Kiểm kê",
        InventoryMovementType.Adjustment => "Điều chỉnh kho",
        InventoryMovementType.CustomerReturn => "Khách trả hàng",
        _ => value.ToString()
    };

    private static Result<T> TooManyRows<T>() => Result.Failure<T>(new AppError(
        ErrorCodes.General.Validation,
        $"Số bản ghi vượt quá giới hạn xuất {MaximumRows:N0}. Hãy thu hẹp bộ lọc rồi thử lại."));
}
