using POS.Application.DTOs.Inventory;
using POS.Application.DTOs.Products;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Exports;

public enum ProductExportReportType
{
    ProductCatalog,
    CurrentStock,
    LowStock,
    ArchivedProducts,
    InventoryHistory,
    ProductImportTemplate
}

public enum ProductExportFormat
{
    Csv,
    Xlsx
}

public enum ProductExportCellType
{
    Text,
    Number,
    DateTime
}

public sealed record ProductExportRequest(
    ProductExportReportType ReportType,
    ProductSearchRequest? ProductFilters = null,
    InventorySearchRequest? HistoryFilters = null);

public sealed record ProductExportColumn(
    string Key,
    string Header,
    ProductExportCellType CellType);

public sealed record ProductExportCell(
    ProductExportCellType Type,
    string? Text = null,
    long? Number = null,
    DateTimeOffset? DateTime = null)
{
    public static ProductExportCell TextValue(string? value) =>
        new(ProductExportCellType.Text, Text: value ?? string.Empty);

    public static ProductExportCell NumberValue(long value) =>
        new(ProductExportCellType.Number, Number: value);

    public static ProductExportCell DateTimeValue(DateTimeOffset value) =>
        new(ProductExportCellType.DateTime, DateTime: value);
}

public sealed record ProductExportRow(
    IReadOnlyList<ProductExportCell> Cells);

public sealed record ProductExportData(
    ProductExportReportType ReportType,
    IReadOnlyList<ProductExportColumn> Columns,
    IReadOnlyList<ProductExportRow> Rows,
    int RowCount,
    bool IncludesCostPrice,
    string SuggestedFileName,
    IReadOnlyList<string> Instructions);
