using System.IO.Compression;
using System.Text;
using POS.Application.DTOs.ProductImports;
using POS.Application.ProductImports;
using POS.Infrastructure.ProductImports;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductImportPreviewTests
{
    private static readonly string[] ExpectedFieldKeys =
    [
        "product_code",
        "barcode",
        "name",
        "category_name",
        "unit_name",
        "sale_price",
        "cost_price",
        "initial_stock_quantity",
        "minimum_stock",
        "is_active",
        "notes"
    ];

    private readonly ProductImportPreviewService service = new();

    [Fact]
    public void Schema_catalog_covers_exactly_the_eleven_source_fields_in_order()
    {
        Assert.Equal(ExpectedFieldKeys, ProductImportSchemaCatalog.Fields.Select(field => field.CanonicalKey));
        Assert.Equal(11, ProductImportSchemaCatalog.Fields.Count);
        Assert.Equal("Trạng thái", ProductImportSchemaCatalog.Fields.Single(field => field.CanonicalKey == "is_active").VietnameseLabel);
        Assert.Equal("Ghi chú", ProductImportSchemaCatalog.Fields.Single(field => field.CanonicalKey == "notes").VietnameseLabel);
        Assert.All(ProductImportSchemaCatalog.Fields, field => Assert.NotEmpty(field.Example));
    }

    [Fact]
    public async Task Valid_utf8_bom_csv_maps_all_eleven_fields_and_preserves_barcode()
    {
        using var fixture = new FixtureScope();
        var path = fixture.Write("products.csv", CsvRow(
            ["ProductCode", "Barcode", "Tên", "Danh mục", "Đơn vị tính", "Giá bán", "Giá vốn", "Tồn đầu", "Tồn tối thiểu", "Trạng thái", "Ghi chú"],
            [" sp001 ", "08900123", " Cà phê rang xay ", "Đồ uống", "Gói", "35000", "25000", "20", "5", "Đang bán", "Gói 500 g"],
            bom: true));

        var result = await service.PreviewAsync(path, Options());

        Assert.Equal(ProductImportFormat.Csv, result.Format);
        Assert.Equal(11, result.Headers.Count);
        Assert.True(result.CanImport);
        Assert.Empty(result.FileIssues);
        var row = Assert.Single(result.PreviewRows);
        Assert.Equal("SP001", row.ProductCode);
        Assert.Equal("08900123", row.Barcode);
        Assert.Equal("Cà phê rang xay", row.Name);
        Assert.Equal("Đồ uống", row.CategoryName);
        Assert.Equal("Gói", row.UnitName);
        Assert.Equal(35000, row.SalePrice);
        Assert.Equal(25000, row.CostPrice);
        Assert.Equal(20, row.InitialStockQuantity);
        Assert.Equal(5, row.MinimumStock);
        Assert.True(row.IsActive);
        Assert.Equal("Gói 500 g", row.Notes);
    }

    [Fact]
    public async Task Vietnamese_aliases_and_semicolon_delimiter_are_mapped_deterministically()
    {
        using var fixture = new FixtureScope();
        var path = fixture.Write("products.csv", CsvRow(
            ["product code", "BAR CODE", "product name", "category name", "unit", "sale price", "cost price", "opening balance", "reorder level", "status", "description"],
            ["SP002", "00077", "Trà", "Đồ uống", "Hộp", "12000", "9000", "2", "1", "Có", "Hàng mới"],
            ';'));

        var result = await service.PreviewAsync(path, Options());

        Assert.True(result.CanImport);
        Assert.Equal(ExpectedFieldKeys, result.Headers.Select(header => header.CanonicalFieldKey));
        var row = Assert.Single(result.PreviewRows);
        Assert.Equal("00077", row.Barcode);
        Assert.Equal(2, row.InitialStockQuantity);
        Assert.Equal(1, row.MinimumStock);
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task Missing_duplicate_and_unknown_headers_are_reported_without_silent_mapping()
    {
        using var fixture = new FixtureScope();
        var path = fixture.Write("headers.csv", CsvRow(
            ["ProductCode", "product code", "Tên", "Danh mục", "Đơn vị tính", "Giá bán", "Cột lạ"],
            ["SP003", "SP003B", "Nước", "Đồ uống", "Chai", "10000", "7000", "x"]));

        var result = await service.PreviewAsync(path, Options());

        Assert.Contains(result.FileIssues, issue => issue.Code == "HEADER_DUPLICATE");
        Assert.Contains(result.FileIssues, issue => issue.Code == "HEADER_UNKNOWN");
        Assert.Contains(result.FileIssues, issue => issue.Code == "HEADER_REQUIRED_MISSING" && issue.FieldKey == "cost_price");
        Assert.False(result.CanImport);
    }

    [Fact]
    public async Task Duplicate_codes_and_barcodes_are_invalid_rows_and_summary_is_accurate()
    {
        using var fixture = new FixtureScope();
        var path = fixture.Write("duplicates.csv", CsvLines(
            HeaderRow,
            [
                "SP004,00001,Sản phẩm 1,Đồ uống,Cái,100,50,0,0,Đang bán,",
                "sp004,00001,Sản phẩm 2,Đồ uống,Cái,200,100,0,0,Đang bán,"
            ]));

        var result = await service.PreviewAsync(path, Options());

        Assert.Equal(2, result.Summary.TotalDataRows);
        Assert.Equal(1, result.Summary.DuplicateProductCodeCount);
        Assert.Equal(1, result.Summary.DuplicateBarcodeCount);
        Assert.Equal(1, result.Summary.ValidRows);
        Assert.Equal(1, result.Summary.InvalidRows);
        Assert.Contains(result.PreviewRows[1].Issues, issue => issue.Code == "DUPLICATE_PRODUCT_CODE");
        Assert.Contains(result.PreviewRows[1].Issues, issue => issue.Code == "DUPLICATE_BARCODE");
        Assert.False(result.CanImport);
    }

    [Fact]
    public async Task Invalid_numbers_status_negative_and_overflow_values_are_rejected()
    {
        using var fixture = new FixtureScope();
        var path = fixture.Write("invalid.csv", CsvLines(HeaderRow,
            ["SP005,00002,Sản phẩm,Đồ uống,Cái,-1,1.5,-2,999999999999999999999,Không rõ,Ghi chú"]));

        var result = await service.PreviewAsync(path, Options());
        var row = Assert.Single(result.PreviewRows);

        Assert.Contains(row.Issues, issue => issue.Code == "NUMBER_NEGATIVE" && issue.FieldKey == "sale_price");
        Assert.Contains(row.Issues, issue => issue.FieldKey == "cost_price");
        Assert.Contains(row.Issues, issue => issue.Code == "NUMBER_NEGATIVE" && issue.FieldKey == "initial_stock_quantity");
        Assert.Contains(row.Issues, issue => issue.FieldKey == "minimum_stock");
        Assert.Contains(row.Issues, issue => issue.Code == "STATUS_INVALID");
        Assert.False(result.CanImport);
    }

    [Fact]
    public async Task Preview_is_bounded_while_summary_counts_all_rows()
    {
        using var fixture = new FixtureScope();
        var path = fixture.Write("many.csv", CsvLines(HeaderRow,
            [
                "SP006,00006,A,Đồ uống,Cái,100,50,0,0,Đang bán,",
                "SP007,00007,B,Đồ uống,Cái,200,100,0,0,Đang bán,",
                "SP008,00008,C,Đồ uống,Cái,300,150,0,0,Đang bán,"
            ]));

        var result = await service.PreviewAsync(path, new ProductImportPreviewOptions(
            new ProductImportLimits(MaximumPreviewRowCount: 1),
            Options().References));

        Assert.Single(result.PreviewRows);
        Assert.Equal(3, result.Summary.TotalDataRows);
        Assert.Equal(3, result.Summary.ValidRows);
        Assert.Equal(0, result.Summary.InvalidRows);
    }

    [Fact]
    public async Task Unsupported_extensions_signature_mismatch_and_malformed_csv_are_safe_failures()
    {
        using var fixture = new FixtureScope();
        var unsupported = fixture.Write("products.xls", Encoding.UTF8.GetBytes("not supported"));
        var mismatched = fixture.Write("products.xlsx", Encoding.UTF8.GetBytes("not a zip"));
        var malformed = fixture.Write("malformed.csv", Encoding.UTF8.GetBytes("ProductCode,Barcode\n\"unclosed"));

        var unsupportedResult = await service.PreviewAsync(unsupported);
        var mismatchResult = await service.PreviewAsync(mismatched);
        var malformedResult = await service.PreviewAsync(malformed);

        Assert.Contains(unsupportedResult.FileIssues, issue => issue.Code == "EXTENSION_UNSUPPORTED");
        Assert.Contains(mismatchResult.FileIssues, issue => issue.Code == "SIGNATURE_MISMATCH");
        Assert.Contains(malformedResult.FileIssues, issue => issue.Code == "CSV_MALFORMED");
        Assert.DoesNotContain(unsupportedResult.FileIssues, issue => issue.Message.Contains(unsupported, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Valid_xlsx_is_read_without_formula_evaluation()
    {
        using var fixture = new FixtureScope();
        var path = fixture.WriteXlsx("products.xlsx", XlsxWorksheet(
            HeaderRow,
            ["SP009", "00009", "Bánh", "Đồ uống", "Cái", "45000", "30000", "4", "1", "Đang bán", "Hộp"]));

        var result = await service.PreviewAsync(path, Options());

        Assert.Equal(ProductImportFormat.Xlsx, result.Format);
        Assert.True(result.CanImport);
        var row = Assert.Single(result.PreviewRows);
        Assert.Equal("00009", row.Barcode);
        Assert.Equal(45000, row.SalePrice);
        Assert.Equal("Hộp", row.Notes);
    }

    [Fact]
    public async Task Xlsx_formula_and_external_content_are_rejected()
    {
        using var fixture = new FixtureScope();
        var formula = fixture.WriteXlsx("formula.xlsx", XlsxWorksheetWithFormula());
        var external = fixture.WriteXlsx("external.xlsx", XlsxWorksheet(HeaderRow, ["SP010", "00010", "C", "Đồ uống", "Cái", "100", "50", "0", "0", "Đang bán", ""]), includeExternalLink: true);

        var formulaResult = await service.PreviewAsync(formula, Options());
        var externalResult = await service.PreviewAsync(external, Options());

        Assert.Contains(formulaResult.FileIssues, issue => issue.Code == "FORMULA_CELL");
        Assert.False(formulaResult.CanImport);
        Assert.Contains(externalResult.FileIssues, issue => issue.Code == "EXTERNAL_OR_MACRO_CONTENT");
        Assert.False(externalResult.CanImport);
    }

    [Fact]
    public async Task Cancellation_is_observed_and_file_handle_is_released()
    {
        using var fixture = new FixtureScope();
        var path = fixture.Write("cancel.csv", CsvLines(HeaderRow, ["SP011,00011,C,Đồ uống,Cái,100,50,0,0,Đang bán,"]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.PreviewAsync(path, Options(), cancellation.Token));
        await service.PreviewAsync(path, Options());
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    }

    [Fact]
    public async Task Data_limits_are_enforced_without_unbounded_preview()
    {
        using var fixture = new FixtureScope();
        var path = fixture.Write("limited.csv", CsvLines(HeaderRow, ["SP012,00012,Quá dài,Đồ uống,Cái,100,50,0,0,Đang bán,ghi chú"]));
        var limits = new ProductImportLimits(MaximumCellLength: 3);

        var result = await service.PreviewAsync(path, new ProductImportPreviewOptions(limits, Options().References));

        Assert.Contains(result.FileIssues, issue => issue.Code == "CELL_TOO_LONG");
        Assert.False(result.CanImport);
    }

    private static ProductImportPreviewOptions Options() => new(
        References: new ProductImportReferenceData(
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Đồ uống"] = 1 },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cái", "Gói", "Hộp", "Chai" }));

    private static string[] HeaderRow => ["ProductCode", "Barcode", "Tên", "Danh mục", "Đơn vị tính", "Giá bán", "Giá vốn", "Tồn đầu", "Tồn tối thiểu", "Trạng thái", "Ghi chú"];

    private static string CsvRow(string[] headers, string[] row, char delimiter = ',', bool bom = false)
    {
        var content = string.Join(delimiter, headers) + "\r\n" + string.Join(delimiter, row) + "\r\n";
        return bom ? "\uFEFF" + content : content;
    }

    private static string CsvLines(string[] headers, string[] rows, char delimiter = ',') =>
        string.Join(delimiter, headers) + "\r\n" + string.Join("\r\n", rows) + "\r\n";

    private static string XlsxWorksheet(string[] headers, string[] row, bool includeExternalLink = false)
    {
        var headerCells = string.Concat(headers.Select((value, index) => InlineCell(1, index, value)));
        var rowCells = string.Concat(row.Select((value, index) => InlineCell(2, index, value)));
        var external = includeExternalLink ? "<externalLinks><externalLink r:id=\"rId2\" /></externalLinks>" : string.Empty;
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheetData><row r=\"1\">{headerCells}</row><row r=\"2\">{rowCells}</row></sheetData>{external}</worksheet>";
    }

    private static string XlsxWorksheetWithFormula()
    {
        var headerCells = string.Concat(HeaderRow.Select((value, index) => InlineCell(1, index, value)));
        var cells = new[] { "SP013", "00013", "C", "Đồ uống", "Cái", string.Empty, "50", "0", "0", "Đang bán", "" };
        var rowCells = string.Concat(cells.Select((value, index) => value.Length == 0 && index == 5 ? FormulaCell(2, index) : InlineCell(2, index, value)));
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\">{headerCells}</row><row r=\"2\">{rowCells}</row></sheetData></worksheet>";
    }

    private static string InlineCell(int row, int column, string value) => $"<c r=\"{ColumnName(column)}{row}\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(value)}</t></is></c>";
    private static string FormulaCell(int row, int column) => $"<c r=\"{ColumnName(column)}{row}\"><f>1+1</f><v>2</v></c>";
    private static string ColumnName(int index) => ((char)('A' + index)).ToString();

    private sealed class FixtureScope : IDisposable
    {
        private const string Prefix = "POS-Enterprise-R51A-";
        private readonly string root = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));

        public FixtureScope() => Directory.CreateDirectory(root);

        public string Write(string fileName, string content) => Write(fileName, new UTF8Encoding(false).GetBytes(content));

        public string Write(string fileName, byte[] content)
        {
            var path = GetOwnedPath(fileName);
            File.WriteAllBytes(path, content);
            return path;
        }

        public string WriteXlsx(string fileName, string worksheet, bool includeExternalLink = false)
        {
            var path = GetOwnedPath(fileName);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\" /></Types>");
                WriteEntry(archive, "xl/workbook.xml", "<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Products\" sheetId=\"1\" r:id=\"rId1\" /></sheets></workbook>");
                var relationships = includeExternalLink
                    ? "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"worksheet\" Target=\"worksheets/sheet1.xml\" /><Relationship Id=\"rId2\" Type=\"externalLink\" Target=\"externalLinks/externalLink1.xml\" /></Relationships>"
                    : "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"worksheet\" Target=\"worksheets/sheet1.xml\" /></Relationships>";
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", relationships);
                WriteEntry(archive, "xl/worksheets/sheet1.xml", worksheet);
                if (includeExternalLink) WriteEntry(archive, "xl/externalLinks/externalLink1.xml", "<externalLink />");
            }

            return path;
        }

        private string GetOwnedPath(string fileName)
        {
            Assert.DoesNotContain("..", fileName, StringComparison.Ordinal);
            var path = Path.GetFullPath(Path.Combine(root, fileName));
            Assert.StartsWith(root + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
            return path;
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        public void Dispose()
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (root.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith(Prefix, StringComparison.Ordinal))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
