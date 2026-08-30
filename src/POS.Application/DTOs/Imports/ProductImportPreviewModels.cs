namespace POS.Application.DTOs.ProductImports;

public enum ProductImportFormat
{
    Unknown = 0,
    Csv = 1,
    Xlsx = 2
}

public enum ProductImportFieldType
{
    Text = 0,
    Barcode = 1,
    VndAmount = 2,
    NonNegativeInteger = 3,
    Boolean = 4
}

public enum ProductImportIssueSeverity
{
    Warning = 0,
    Error = 1
}

public sealed record ProductImportLimits(
    long MaximumFileSizeBytes = 25 * 1024 * 1024,
    int MaximumWorksheetCount = 10,
    int MaximumDataRowCount = 10_000,
    int MaximumColumnCount = 64,
    int MaximumCellLength = 2_000,
    int MaximumPreviewRowCount = 100,
    long MaximumXlsxEntryBytes = 10 * 1024 * 1024,
    long MaximumXlsxUncompressedBytes = 50 * 1024 * 1024);

public sealed record ProductImportReferenceData(
    IReadOnlyDictionary<string, int>? CategoryIdsByNormalizedName = null,
    IReadOnlySet<string>? KnownUnitNames = null);

/// <summary>
/// Ánh xạ một cột nguồn vào một trường Product chuẩn hóa.
/// CanonicalFieldKey null nghĩa là người dùng chủ động bỏ ánh xạ cột.
/// </summary>
public sealed record ProductImportColumnMapping(
    int ColumnIndex,
    string? CanonicalFieldKey);

public sealed record ProductImportPreviewOptions(
    ProductImportLimits? Limits = null,
    ProductImportReferenceData? References = null,
    string? WorksheetName = null,
    IReadOnlyList<ProductImportColumnMapping>? ColumnMappings = null);

public sealed record ProductImportFileMetadata(
    string FileName,
    string Extension,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc,
    string? ContentSha256 = null);

public sealed record ProductImportReferenceSnapshot(
    IReadOnlyDictionary<string, int>? CategoryIdsByNormalizedName,
    IReadOnlySet<string>? KnownUnitNames);

public sealed record ProductImportHeader(
    int ColumnIndex,
    string OriginalName,
    string? CanonicalFieldKey,
    bool IsKnown)
{
    public string? SampleValue { get; init; }
}

public sealed record ProductImportIssue(
    ProductImportIssueSeverity Severity,
    string Code,
    string Message,
    int? SourceRowNumber = null,
    string? FieldKey = null,
    int? ColumnIndex = null);

/// <summary>
/// Một dòng đã được chuẩn hóa kiểu dữ liệu. Giá trị lỗi được trả về null,
/// còn lỗi cụ thể nằm trong Issues để UI có thể trình bày theo ô/dòng.
/// </summary>
public sealed record ProductImportRow(
    int SourceRowNumber,
    string? ProductCode,
    string? Barcode,
    string? Name,
    string? CategoryName,
    string? UnitName,
    long? SalePrice,
    long? CostPrice,
    int? InitialStockQuantity,
    int? MinimumStock,
    bool? IsActive,
    string? Notes,
    IReadOnlyList<ProductImportIssue> Issues);

public sealed record ProductImportSummary(
    int TotalDataRows,
    int EmptyRows,
    int ValidRows,
    int InvalidRows,
    int ErrorCount,
    int WarningCount,
    int DuplicateProductCodeCount,
    int DuplicateBarcodeCount);

public sealed record ProductImportPreviewResult(
    ProductImportFileMetadata File,
    ProductImportFormat Format,
    IReadOnlyList<ProductImportHeader> Headers,
    IReadOnlyList<ProductImportIssue> FileIssues,
    IReadOnlyList<ProductImportRow> PreviewRows,
    ProductImportSummary Summary)
{
    /// <summary>
    /// Toàn bộ row đã validate; PreviewRows chỉ là phần hiển thị bounded.
    /// </summary>
    public IReadOnlyList<ProductImportRow> ValidatedRows { get; init; } = PreviewRows;

    /// <summary>
    /// Snapshot reference data dùng để chống import nhầm sau khi preview cũ.
    /// </summary>
    public ProductImportReferenceSnapshot? ReferenceSnapshot { get; init; }

    /// <summary>
    /// Tên worksheet đã chọn. CSV dùng tên cố định "CSV".
    /// </summary>
    public string? SelectedWorksheetName { get; init; }

    /// <summary>
    /// Danh sách worksheet an toàn để UI cho người dùng chọn.
    /// </summary>
    public IReadOnlyList<string> WorksheetNames { get; init; } = [];

    /// <summary>
    /// Mapping người dùng đã xác nhận; null nghĩa là dùng auto-mapping.
    /// </summary>
    public IReadOnlyList<ProductImportColumnMapping>? ColumnMappings { get; init; }

    public bool CanImport =>
        Summary.TotalDataRows > 0 &&
        Summary.InvalidRows == 0 &&
        Summary.ErrorCount == 0;
}
