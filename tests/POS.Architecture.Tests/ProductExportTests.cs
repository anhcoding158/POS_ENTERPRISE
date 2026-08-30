using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Exports;
using POS.Application.DTOs.Inventory;
using POS.Application.DTOs.Products;
using POS.Application.ProductImports;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Exports;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductExportTests
{
    [Fact]
    public async Task Product_catalog_export_is_typed_and_preserves_leading_zero_barcode()
    {
        var repository = new FakeProductRepository
        {
            Products = [new Product(1, "SP001", "Cà phê", "Gói", 25000, 35000, 4, 5, true, false, DateTimeOffset.UtcNow, "08900123")]
        };
        var service = new ProductExportService(repository, new FakeMovementRepository(), new FakePermissionService(SystemCapability.ViewProductCatalog, SystemCapability.ManageProducts));

        var result = await service.ExportAsync(new(ProductExportReportType.ProductCatalog));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.RowCount);
        Assert.Contains(result.Value.Columns, column => column.Key == "cost_price");
        Assert.Equal("08900123", result.Value.Rows[0].Cells[1].Text);
        Assert.True(repository.LastExportArguments!.Value.IsLowStock is null);
    }

    [Fact]
    public async Task Low_stock_export_reuses_database_filter_and_does_not_leak_cost_without_manage_permission()
    {
        var repository = new FakeProductRepository();
        var service = new ProductExportService(repository, new FakeMovementRepository(), new FakePermissionService(SystemCapability.ViewProductCatalog));

        var result = await service.ExportAsync(new(ProductExportReportType.LowStock));

        Assert.True(result.IsSuccess);
        Assert.True(repository.LastExportArguments!.Value.IsLowStock);
        Assert.DoesNotContain(result.Value.Columns, column => column.Key == "cost_price");
    }

    [Fact]
    public async Task Inventory_history_export_keeps_customer_return_as_a_distinct_business_label()
    {
        var movement = new InventoryMovement(1, InventoryMovementType.CustomerReturn, 2, 3, 5, "Khách trả hàng", DateTimeOffset.UtcNow);
        var repository = new FakeMovementRepository { Movements = [movement] };
        var service = new ProductExportService(new FakeProductRepository(), repository, new FakePermissionService(SystemCapability.ViewInventoryHistory));

        var result = await service.ExportAsync(new(ProductExportReportType.InventoryHistory));

        Assert.True(result.IsSuccess);
        Assert.Equal("Khách trả hàng", result.Value.Rows[0].Cells[3].Text);
    }

    [Fact]
    public async Task Import_template_uses_the_single_eleven_field_schema_and_has_no_data_rows()
    {
        var service = new ProductExportService(new FakeProductRepository(), new FakeMovementRepository(), new FakePermissionService(SystemCapability.ManageProducts));

        var result = await service.ExportAsync(new(ProductExportReportType.ProductImportTemplate));

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductImportSchemaCatalog.Fields.Count, result.Value.Columns.Count);
        Assert.Equal(11, result.Value.Columns.Count);
        Assert.Empty(result.Value.Rows);
        Assert.Equal(ProductImportSchemaCatalog.Fields.Select(field => field.CanonicalKey), result.Value.Columns.Select(column => column.Key));
    }

    [Fact]
    public async Task Writer_creates_readable_csv_and_xlsx_without_formula_cells_and_cleans_temp_output()
    {
        var data = new ProductExportData(
            ProductExportReportType.ProductCatalog,
            [new("code", "Mã sản phẩm", ProductExportCellType.Text), new("value", "Ghi chú", ProductExportCellType.Text)],
            [new([ProductExportCell.TextValue("000123"), ProductExportCell.TextValue("=HYPERLINK(\"https://example.invalid\")")])],
            1,
            false,
            "test",
            []);
        var directory = Path.Combine(Path.GetTempPath(), $"POS-Enterprise-ExportTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var writer = new ProductExportFileWriter();
            var csvPath = Path.Combine(directory, "report.csv");
            var xlsxPath = Path.Combine(directory, "report.xlsx");
            await writer.WriteAsync(data, ProductExportFormat.Csv, csvPath);
            await writer.WriteAsync(data, ProductExportFormat.Xlsx, xlsxPath);

            var csv = await File.ReadAllTextAsync(csvPath);
            Assert.Contains("000123", csv);
            Assert.Contains("'=HYPERLINK", csv);

            using var archive = ZipFile.OpenRead(xlsxPath);
            var sheet = archive.GetEntry("xl/worksheets/sheet1.xml");
            Assert.NotNull(sheet);
            using var reader = new StreamReader(sheet!.Open());
            var xml = await reader.ReadToEndAsync();
            Assert.Contains("000123", xml);
            Assert.DoesNotContain("<f", xml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("HYPERLINK", xml);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class FakePermissionService(params SystemCapability[] permissions) : IPermissionService
    {
        private readonly HashSet<SystemCapability> _permissions = permissions.ToHashSet();

        public bool HasPermission(SystemCapability permission) => _permissions.Contains(permission);

        public Result Authorize(SystemCapability permission) =>
            HasPermission(permission)
                ? Result.Success()
                : Result.Failure(new AppError("FORBIDDEN", "Không có quyền."));
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public IReadOnlyList<Product> Products { get; set; } = [];
        public (string? SearchTerm, int? CategoryId, bool? IsActive, bool? IsLowStock, bool? IsArchived)? LastExportArguments { get; private set; }

        public Task<IReadOnlyList<Product>> ExportAsync(string? searchTerm, int? categoryId, bool? isActive, bool? isLowStock, bool? isArchived, int maximumRows, CancellationToken cancellationToken = default)
        {
            LastExportArguments = (searchTerm, categoryId, isActive, isLowStock, isArchived);
            return Task.FromResult(Products);
        }

        public Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<Product?> GetByIdReadOnlyAsync(int productId, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task ReloadTrackedAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<PagedResult<Product>> SearchAsync(string? searchTerm, int? categoryId, bool? isActive, bool? isLowStock, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<Product>> SearchAsync(string? searchTerm, int? categoryId, bool? isActive, bool? isLowStock, int pageNumber, int pageSize, bool? isArchived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> CodeExistsAsync(string code, int? excludeProductId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> BarcodeExistsAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeMovementRepository : IInventoryMovementRepository
    {
        public IReadOnlyList<InventoryMovement> Movements { get; set; } = [];
        public Task<IReadOnlyList<InventoryMovement>> ExportAsync(int? productId, InventoryMovementType? movementType, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? referenceType, string? productSearchTerm, int maximumRows, CancellationToken cancellationToken = default) => Task.FromResult(Movements);
        public Task<InventoryMovement?> GetByIdAsync(int movementId, CancellationToken cancellationToken = default) => Task.FromResult<InventoryMovement?>(null);
        public Task<PagedResult<InventoryMovement>> SearchAsync(int? productId, InventoryMovementType? movementType, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? referenceType, int pageNumber, int pageSize, string? productSearchTerm = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InventoryMovementSummaryDto> GetSummaryAsync(int? productId, InventoryMovementType? movementType, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? referenceType, string? productSearchTerm = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(InventoryMovement movement, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
