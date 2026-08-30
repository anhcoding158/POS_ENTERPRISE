using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using POS.Application.Abstractions.ProductImports;
using POS.Application.DTOs.ProductImports;
using POS.Application.ProductImports;
using POS.Domain.Constants;

namespace POS.Infrastructure.ProductImports;

/// <summary>
/// Secure, read-only CSV/XLSX preview parser. This type deliberately has no
/// DbContext/repository dependency: R5.1A cannot mutate Product data.
/// </summary>
public sealed class ProductImportPreviewService : IProductImportPreviewService
{
    private static readonly Encoding Utf8Strict = new UTF8Encoding(false, true);

    public async Task<ProductImportPreviewResult> PreviewAsync(
        string filePath,
        ProductImportPreviewOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var limits = options?.Limits ?? new ProductImportLimits();
        ValidateLimits(limits);

        var fileName = Path.GetFileName(filePath);
        var fallbackFile = new ProductImportFileMetadata(
            fileName,
            Path.GetExtension(fileName),
            0,
            DateTimeOffset.MinValue);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return Failure(fallbackFile, "FILE_PATH_INVALID", "Đường dẫn tệp không hợp lệ.");
        }

        if (!TryGetRegularFile(fullPath, out var fileInfo))
        {
            return Failure(fallbackFile, "FILE_NOT_REGULAR", "Chỉ chấp nhận tệp thường, không chấp nhận thư mục hoặc liên kết.");
        }

        var metadata = new ProductImportFileMetadata(
            fileInfo.Name,
            fileInfo.Extension,
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc);

        if (fileInfo.Length > limits.MaximumFileSizeBytes)
        {
            return Failure(metadata, "FILE_TOO_LARGE", "Tệp vượt quá giới hạn kích thước cho phép.");
        }

        var extension = fileInfo.Extension.ToLowerInvariant();
        if (extension is not ".csv" and not ".xlsx")
        {
            return Failure(metadata, "EXTENSION_UNSUPPORTED", "Chỉ hỗ trợ tệp .csv hoặc .xlsx; không hỗ trợ .xls/.xlsm.");
        }

        try
        {
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 32 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            cancellationToken.ThrowIfCancellationRequested();
            var signature = await ReadSignatureAsync(stream, cancellationToken);
            stream.Position = 0;

            var contentHash =
                await SHA256.HashDataAsync(
                    stream,
                    cancellationToken);
            metadata = metadata with
            {
                ContentSha256 =
                    Convert.ToHexString(contentHash)
            };
            stream.Position = 0;

            if (extension == ".xlsx")
            {
                if (!IsZipSignature(signature))
                {
                    return Failure(metadata, "SIGNATURE_MISMATCH", "Tệp .xlsx không có chữ ký container hợp lệ.", ProductImportFormat.Xlsx);
                }

                return await ParseXlsxAsync(stream, metadata, limits, options, cancellationToken);
            }

            if (LooksLikeBinaryOrZip(signature))
            {
                return Failure(metadata, "SIGNATURE_MISMATCH", "Tệp .csv có chữ ký nhị phân không phù hợp.", ProductImportFormat.Csv);
            }

            return await ParseCsvAsync(stream, metadata, limits, options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ImportDataException exception)
        {
            return Failure(metadata, exception.Code, exception.SafeMessage, extension == ".xlsx" ? ProductImportFormat.Xlsx : ProductImportFormat.Csv);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or XmlException)
        {
            return Failure(metadata, "FILE_READ_FAILED", "Không thể đọc tệp import an toàn.", extension == ".xlsx" ? ProductImportFormat.Xlsx : ProductImportFormat.Csv);
        }
    }

    private static async Task<ProductImportPreviewResult> ParseCsvAsync(
        Stream stream,
        ProductImportFileMetadata metadata,
        ProductImportLimits limits,
        ProductImportPreviewOptions? options,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            stream,
            Utf8Strict,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 32 * 1024,
            leaveOpen: true);

        var content = await reader.ReadToEndAsync(cancellationToken);
        var delimiter = DetectDelimiter(content);
        var records = ParseCsvRecords(content, delimiter, limits, cancellationToken);
        return BuildPreview(metadata, ProductImportFormat.Csv, records, limits, options?.References, options?.ColumnMappings, "CSV", ["CSV"]);
    }

    private static async Task<ProductImportPreviewResult> ParseXlsxAsync(
        Stream stream,
        ProductImportFileMetadata metadata,
        ProductImportLimits limits,
        ProductImportPreviewOptions? options,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entries = archive.Entries;
        if (entries.Count > 512)
        {
            return Failure(metadata, "WORKBOOK_TOO_MANY_ENTRIES", "Bảng tính chứa quá nhiều thành phần.", ProductImportFormat.Xlsx);
        }

        long uncompressedBytes = 0;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.FullName.Contains("..", StringComparison.Ordinal) || entry.FullName.StartsWith('/'))
            {
                return Failure(metadata, "CONTAINER_ENTRY_UNSAFE", "Container XLSX chứa đường dẫn thành phần không an toàn.", ProductImportFormat.Xlsx);
            }

            if (entry.Length > limits.MaximumXlsxEntryBytes ||
                (uncompressedBytes = checked(uncompressedBytes + entry.Length)) > limits.MaximumXlsxUncompressedBytes)
            {
                return Failure(metadata, "WORKBOOK_TOO_LARGE", "Bảng tính vượt quá giới hạn giải nén an toàn.", ProductImportFormat.Xlsx);
            }
        }

        if (entries.Any(entry => entry.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase) ||
                                 entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
        {
            return Failure(metadata, "EXTERNAL_OR_MACRO_CONTENT", "Tệp XLSX chứa macro hoặc liên kết ngoài không được phép.", ProductImportFormat.Xlsx);
        }

        var workbookEntry = FindEntry(entries, "xl/workbook.xml");
        if (workbookEntry is null)
        {
            return Failure(metadata, "WORKBOOK_MISSING", "Tệp XLSX thiếu workbook hợp lệ.", ProductImportFormat.Xlsx);
        }

        var workbook = await ReadXmlAsync(workbookEntry, limits, cancellationToken);
        var workbookNamespace = workbook.Root?.Name.Namespace ?? XNamespace.None;
        if (workbook.Descendants().Any(element => element.Name.LocalName == "externalLink"))
        {
            return Failure(metadata, "EXTERNAL_LINKS_NOT_ALLOWED", "Liên kết ngoài trong XLSX không được phép.", ProductImportFormat.Xlsx);
        }

        var sheetElements = workbook.Descendants(workbookNamespace + "sheet").ToArray();
        if (sheetElements.Length == 0)
        {
            return Failure(metadata, "WORKSHEET_MISSING", "Tệp XLSX không có worksheet dữ liệu.", ProductImportFormat.Xlsx);
        }

        if (sheetElements.Length > limits.MaximumWorksheetCount)
        {
            return Failure(metadata, "WORKSHEET_LIMIT_EXCEEDED", "Bảng tính có quá nhiều worksheet.", ProductImportFormat.Xlsx);
        }

        var relationshipsEntry = FindEntry(entries, "xl/_rels/workbook.xml.rels");
        var relationships = relationshipsEntry is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await ReadWorkbookRelationshipsAsync(relationshipsEntry, limits, cancellationToken);

        foreach (var relationship in relationships)
        {
            if (relationship.Value.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
                relationship.Value.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ||
                relationship.Value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return Failure(metadata, "EXTERNAL_LINKS_NOT_ALLOWED", "Workbook có tham chiếu tài nguyên ngoài.", ProductImportFormat.Xlsx);
            }
        }

        var worksheetNames = sheetElements
            .Select(sheet => sheet.Attribute("name")?.Value?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray();

        if (worksheetNames.Length != sheetElements.Length || worksheetNames.Distinct(StringComparer.Ordinal).Count() != worksheetNames.Length)
        {
            return Failure(metadata, "WORKSHEET_NAME_INVALID", "Tên worksheet không hợp lệ hoặc bị trùng.", ProductImportFormat.Xlsx);
        }

        var selectedWorksheetName = options?.WorksheetName;
        if (sheetElements.Length > 1 && string.IsNullOrWhiteSpace(selectedWorksheetName))
        {
            return CreateResult(
                metadata,
                ProductImportFormat.Xlsx,
                [],
                [new ProductImportIssue(ProductImportIssueSeverity.Error, "WORKSHEET_SELECTION_REQUIRED", "Tệp có nhiều worksheet; hãy chọn worksheet cần nhập.")],
                [],
                new ProductImportSummary(0, 0, 0, 0, 1, 0, 0, 0),
                worksheetNames: worksheetNames);
        }

        var selectedIndex = string.IsNullOrWhiteSpace(selectedWorksheetName)
            ? 0
            : Array.FindIndex(worksheetNames, name => string.Equals(name, selectedWorksheetName.Trim(), StringComparison.Ordinal));
        if (selectedIndex < 0)
        {
            return CreateResult(
                metadata,
                ProductImportFormat.Xlsx,
                [],
                [new ProductImportIssue(ProductImportIssueSeverity.Error, "WORKSHEET_NOT_FOUND", "Worksheet đã chọn không còn tồn tại trong tệp.")],
                [],
                new ProductImportSummary(0, 0, 0, 0, 1, 0, 0, 0),
                worksheetNames: worksheetNames);
        }

        var selectedSheet = sheetElements[selectedIndex];
        var relationshipId = selectedSheet.Attribute("{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id")?.Value;
        if (relationshipId is null || !relationships.TryGetValue(relationshipId, out var target))
        {
            return Failure(metadata, "WORKSHEET_RELATIONSHIP_INVALID", "Worksheet không có liên kết nội bộ hợp lệ.", ProductImportFormat.Xlsx);
        }

        var worksheetPath = ResolveInternalXlsxPath(target);
        if (worksheetPath is null)
        {
            return Failure(metadata, "WORKSHEET_RELATIONSHIP_INVALID", "Worksheet trỏ tới tài nguyên ngoài hoặc đường dẫn không an toàn.", ProductImportFormat.Xlsx);
        }

        var worksheetEntry = FindEntry(entries, worksheetPath);
        if (worksheetEntry is null)
        {
            return Failure(metadata, "WORKSHEET_MISSING", "Không tìm thấy worksheet dữ liệu.", ProductImportFormat.Xlsx);
        }

        var sharedStrings = await ReadSharedStringsAsync(FindEntry(entries, "xl/sharedStrings.xml"), limits, cancellationToken);
        var worksheet = await ReadXmlAsync(worksheetEntry, limits, cancellationToken);

        var worksheetEntries = entries.Where(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        foreach (var entry in worksheetEntries)
        {
            var document = ReferenceEquals(entry, worksheetEntry) ? worksheet : await ReadXmlAsync(entry, limits, cancellationToken);
            if (document.Descendants().Any(element => element.Name.LocalName == "f"))
            {
                return CreateResult(
                    metadata,
                    ProductImportFormat.Xlsx,
                    [],
                    [new ProductImportIssue(ProductImportIssueSeverity.Error, "FORMULA_CELL", "Ô công thức không được đánh giá trong preview.")],
                    [],
                    new ProductImportSummary(0, 0, 0, 0, 1, 0, 0, 0),
                    worksheetNames: worksheetNames,
                    selectedWorksheetName: worksheetNames[selectedIndex]);
            }
        }

        return BuildPreview(
            metadata,
            ProductImportFormat.Xlsx,
            ParseWorksheet(worksheet, sharedStrings, limits, cancellationToken),
            limits,
            options?.References,
            options?.ColumnMappings,
            worksheetNames[selectedIndex],
            worksheetNames);
    }

    private static ProductImportPreviewResult BuildPreview(
        ProductImportFileMetadata metadata,
        ProductImportFormat format,
        IReadOnlyList<RawRecord> records,
        ProductImportLimits limits,
        ProductImportReferenceData? references,
        IReadOnlyList<ProductImportColumnMapping>? columnMappings,
        string? selectedWorksheetName,
        IReadOnlyList<string>? worksheetNames)
    {
        var fileIssues = new List<ProductImportIssue>();
        if (records.Count == 0)
        {
            fileIssues.Add(new(ProductImportIssueSeverity.Error, "HEADER_MISSING", "Không tìm thấy dòng tiêu đề."));
            return CreateResult(metadata, format, [], fileIssues, [], new ProductImportSummary(0, 0, 0, 0, 1, 0, 0, 0));
        }

        var headerRecord = records[0];
        if (headerRecord.Values.Count == 0 || headerRecord.Values.All(string.IsNullOrWhiteSpace))
        {
            fileIssues.Add(new(ProductImportIssueSeverity.Error, "HEADER_MISSING", "Dòng tiêu đề không được để trống."));
        }

        var headers = new List<ProductImportHeader>(headerRecord.Values.Count);
        var mapped = new Dictionary<int, ProductImportFieldDefinition>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var mappingByColumn = columnMappings?
            .GroupBy(mapping => mapping.ColumnIndex)
            .ToDictionary(group => group.Key, group => group.Last())
            ?? new Dictionary<int, ProductImportColumnMapping>();

        if (columnMappings is not null && mappingByColumn.Count != columnMappings.Count)
        {
            fileIssues.Add(new(ProductImportIssueSeverity.Error, "MAPPING_COLUMN_DUPLICATE", "Một cột nguồn được ánh xạ nhiều lần."));
        }

        foreach (var mapping in mappingByColumn.Values.Where(mapping => mapping.ColumnIndex < 0 || mapping.ColumnIndex >= headerRecord.Values.Count))
        {
            fileIssues.Add(new(ProductImportIssueSeverity.Error, "MAPPING_COLUMN_INVALID", "Cột nguồn được ánh xạ không tồn tại trong header."));
        }

        for (var index = 0; index < headerRecord.Values.Count; index++)
        {
            var original = headerRecord.Values[index].Trim().TrimStart('\uFEFF');
            var hasOverride = mappingByColumn.TryGetValue(index, out var overrideMapping);
            var definition = hasOverride
                ? ProductImportSchemaCatalog.FindByCanonicalKey(overrideMapping!.CanonicalFieldKey)
                : string.IsNullOrWhiteSpace(original) ? null : ProductImportSchemaCatalog.Find(original);
            if (hasOverride && !string.IsNullOrWhiteSpace(overrideMapping!.CanonicalFieldKey) && definition is null)
            {
                fileIssues.Add(new(ProductImportIssueSeverity.Error, "MAPPING_TARGET_INVALID", "Trường đích được chọn không hợp lệ.", null, null, index));
            }
            var isKnown = definition is not null;
            headers.Add(new(index, original, definition?.CanonicalKey, isKnown)
            {
                SampleValue = records.Count > 1 && index < records[1].Values.Count
                    ? records[1].Values[index]
                    : null
            });

            if (definition is null)
            {
                fileIssues.Add(new(ProductImportIssueSeverity.Warning, "HEADER_UNKNOWN", $"Cột '{SafeHeader(original)}' không được nhận diện; cột sẽ không được ánh xạ.", null, null, index));
                continue;
            }

            if (!seenKeys.Add(definition.CanonicalKey))
            {
                fileIssues.Add(new(ProductImportIssueSeverity.Error, "HEADER_DUPLICATE", $"Cột '{definition.VietnameseLabel}' bị lặp.", null, definition.CanonicalKey, index));
                continue;
            }

            mapped[index] = definition;
        }

        foreach (var required in ProductImportSchemaCatalog.Fields.Where(field => field.Required))
        {
            if (!seenKeys.Contains(required.CanonicalKey))
            {
                fileIssues.Add(new(ProductImportIssueSeverity.Error, "HEADER_REQUIRED_MISSING", $"Thiếu cột bắt buộc '{required.VietnameseLabel}'.", null, required.CanonicalKey));
            }
        }

        var allRows = new List<ProductImportRow>();
        var previewRows = new List<ProductImportRow>();
        var codeRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var barcodeRows = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicateCodes = 0;
        var duplicateBarcodes = 0;
        var emptyRows = 0;

        foreach (var record in records.Skip(1))
        {
            var row = ConvertRow(record, mapped, references);
            if (row.Issues.Count == 0 && row.ProductCode is null && record.Values.All(string.IsNullOrWhiteSpace))
            {
                emptyRows++;
                continue;
            }

            var rowIssues = row.Issues.ToList();
            if (row.ProductCode is not null)
            {
                if (codeRows.ContainsKey(row.ProductCode))
                {
                    duplicateCodes++;
                    rowIssues.Add(new(ProductImportIssueSeverity.Error, "DUPLICATE_PRODUCT_CODE", "Mã sản phẩm bị trùng trong tệp.", row.SourceRowNumber, "product_code"));
                }
                else
                {
                    codeRows[row.ProductCode] = row.SourceRowNumber;
                }
            }

            if (row.Barcode is not null)
            {
                if (barcodeRows.ContainsKey(row.Barcode))
                {
                    duplicateBarcodes++;
                    rowIssues.Add(new(ProductImportIssueSeverity.Error, "DUPLICATE_BARCODE", "Mã vạch bị trùng trong tệp.", row.SourceRowNumber, "barcode"));
                }
                else
                {
                    barcodeRows[row.Barcode] = row.SourceRowNumber;
                }
            }

            row = row with { Issues = rowIssues };
            allRows.Add(row);
            if (previewRows.Count < limits.MaximumPreviewRowCount)
            {
                previewRows.Add(row);
            }
        }

        var errorCount = fileIssues.Count(issue => issue.Severity == ProductImportIssueSeverity.Error) + allRows.Sum(row => row.Issues.Count(issue => issue.Severity == ProductImportIssueSeverity.Error));
        var warningCount = fileIssues.Count(issue => issue.Severity == ProductImportIssueSeverity.Warning) + allRows.Sum(row => row.Issues.Count(issue => issue.Severity == ProductImportIssueSeverity.Warning));
        var invalidRows = allRows.Count(row => row.Issues.Any(issue => issue.Severity == ProductImportIssueSeverity.Error));
        var summary = new ProductImportSummary(
            allRows.Count,
            emptyRows,
            allRows.Count - invalidRows,
            invalidRows,
            errorCount,
            warningCount,
            duplicateCodes,
            duplicateBarcodes);

        return CreateResult(
            metadata,
            format,
            headers,
            fileIssues,
            previewRows,
            summary,
            allRows,
            references,
            selectedWorksheetName,
            worksheetNames,
            columnMappings);
    }

    private static ProductImportRow ConvertRow(
        RawRecord record,
        Dictionary<int, ProductImportFieldDefinition> mapped,
        ProductImportReferenceData? references)
    {
        var issues = new List<ProductImportIssue>();
        foreach (var formulaColumn in record.FormulaColumns)
        {
            if (mapped.TryGetValue(formulaColumn, out var formulaField))
            {
                issues.Add(new ProductImportIssue(
                    ProductImportIssueSeverity.Error,
                    "FORMULA_CELL",
                    "Ô công thức không được đánh giá trong preview.",
                    record.SourceRowNumber,
                    formulaField.CanonicalKey,
                    formulaColumn));
            }
        }

        string? Read(string key)
        {
            var match = mapped.FirstOrDefault(pair => pair.Value.CanonicalKey == key);
            return match.Value is not null && match.Key < record.Values.Count ? record.Values[match.Key] : null;
        }

        string? Text(string key, int maxLength, bool required)
        {
            var value = Read(key)?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required) issues.Add(Error("VALUE_REQUIRED", "Giá trị bắt buộc không được để trống.", record, key));
                return null;
            }

            if (value.Length > maxLength)
            {
                issues.Add(Error("VALUE_TOO_LONG", "Giá trị vượt quá giới hạn độ dài.", record, key));
                return null;
            }

            if (value.StartsWith('='))
            {
                issues.Add(Error("FORMULA_CELL", "Ô công thức không được đánh giá trong preview.", record, key));
                return null;
            }

            return value;
        }

        string? ProductCode()
        {
            var value = Text("product_code", BusinessRules.Products.CodeMaxLength, true);
            return value?.ToUpperInvariant();
        }

        string? Barcode()
        {
            var value = Text("barcode", BusinessRules.Products.BarcodeMaxLength, false);
            return value;
        }

        long? Amount(string key)
        {
            var value = Read(key)?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add(Error("VALUE_REQUIRED", "Giá trị tiền bắt buộc không được để trống.", record, key));
                return null;
            }

            if (!TryParseUnsignedInteger(value, out var parsed, out var code))
            {
                issues.Add(Error(code, "Giá tiền phải là số nguyên VND không phân cách, không âm và không mơ hồ văn hóa.", record, key));
                return null;
            }

            if (parsed > BusinessRules.Products.MaximumPrice)
            {
                issues.Add(Error("VALUE_OVERFLOW", "Giá tiền vượt quá giới hạn hệ thống.", record, key));
                return null;
            }

            return parsed;
        }

        int? Quantity(string key, bool required = false)
        {
            var raw = Read(key)?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (required) issues.Add(Error("VALUE_REQUIRED", "Số lượng bắt buộc không được để trống.", record, key));
                return required ? null : 0;
            }

            if (!TryParseUnsignedInteger(raw, out var parsed, out var code) || parsed > BusinessRules.Products.MaximumStockQuantity)
            {
                issues.Add(Error(code == "NUMBER_NEGATIVE" ? code : "QUANTITY_INVALID", "Số lượng phải là số nguyên không âm trong giới hạn hệ thống.", record, key));
                return null;
            }

            return (int)parsed;
        }

        bool? Status()
        {
            const string key = "is_active";
            var raw = Read(key)?.Trim();
            if (string.IsNullOrWhiteSpace(raw)) return true;
            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw.Equals("1", StringComparison.Ordinal) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase) || raw.Equals("có", StringComparison.OrdinalIgnoreCase) || raw.Equals("co", StringComparison.OrdinalIgnoreCase)) return true;
            if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw.Equals("0", StringComparison.Ordinal) || raw.Equals("no", StringComparison.OrdinalIgnoreCase) || raw.Equals("không", StringComparison.OrdinalIgnoreCase) || raw.Equals("khong", StringComparison.OrdinalIgnoreCase)) return false;
            if (raw.Equals("đang bán", StringComparison.OrdinalIgnoreCase) || raw.Equals("dang ban", StringComparison.OrdinalIgnoreCase)) return true;
            if (raw.Equals("ngừng bán", StringComparison.OrdinalIgnoreCase) || raw.Equals("ngung ban", StringComparison.OrdinalIgnoreCase) || raw.Equals("inactive", StringComparison.OrdinalIgnoreCase)) return false;
            issues.Add(Error("STATUS_INVALID", "Trạng thái phải là Đang bán/Ngừng bán, Có/Không hoặc true/false.", record, key));
            return null;
        }

        var category = Text("category_name", BusinessRules.Categories.NameMaxLength, true);
        if (category is not null && references?.CategoryIdsByNormalizedName is not null &&
            !references.CategoryIdsByNormalizedName.Keys.Any(key => ProductImportSchemaCatalog.NormalizeHeader(key) == ProductImportSchemaCatalog.NormalizeHeader(category)))
        {
            issues.Add(Error("CATEGORY_NOT_FOUND", $"Danh mục '{SafeCell(category)}' chưa có trong cửa hàng; hãy sửa tên hoặc tạo danh mục trước khi nhập.", record, "category_name"));
        }
        else if (category is not null && references?.CategoryIdsByNormalizedName is null)
        {
            issues.Add(Warning("CATEGORY_UNRESOLVED", "Danh mục chưa được đối chiếu vì preview không mở database.", record, "category_name"));
        }

        var unit = Text("unit_name", BusinessRules.Products.UnitNameMaxLength, true);
        if (unit is not null && references?.KnownUnitNames is not null &&
            !references.KnownUnitNames.Any(known => string.Equals(known.Trim(), unit, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(Warning("UNIT_UNKNOWN", "Đơn vị chưa có trong catalog tham chiếu; Product hiện lưu đơn vị dạng text và không tự tạo Unit.", record, "unit_name"));
        }

        var initialStock = Quantity("initial_stock_quantity");
        var minimumStock = Quantity("minimum_stock");

        return new ProductImportRow(
            record.SourceRowNumber,
            ProductCode(),
            Barcode(),
            Text("name", BusinessRules.Products.NameMaxLength, true),
            category,
            unit,
            Amount("sale_price"),
            Amount("cost_price"),
            initialStock,
            minimumStock,
            Status(),
            Text("notes", BusinessRules.Products.DescriptionMaxLength, false),
            issues);
    }

    private static List<RawRecord> ParseWorksheet(
        XDocument document,
        IReadOnlyList<string> sharedStrings,
        ProductImportLimits limits,
        CancellationToken cancellationToken)
    {
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;
        var records = new List<RawRecord>();
        var nextRowNumber = 1;
        foreach (var row in document.Descendants(ns + "row"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowNumber = int.TryParse(row.Attribute("r")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRow) ? parsedRow : nextRowNumber;
            nextRowNumber = rowNumber + 1;
            var cells = new string[limits.MaximumColumnCount];
            var formulas = new HashSet<int>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = cell.Attribute("r")?.Value;
                var column = reference is null ? Array.FindIndex(cells, value => value is null) : ColumnIndex(reference);
                if (column < 0 || column >= limits.MaximumColumnCount)
                {
                    throw new ImportDataException("COLUMN_LIMIT_EXCEEDED", "Worksheet vượt quá giới hạn số cột.");
                }

                if (cell.Element(ns + "f") is not null)
                {
                    formulas.Add(column);
                    cells[column] = string.Empty;
                    continue;
                }

                var value = cell.Element(ns + "v")?.Value ?? cell.Element(ns + "is")?.Value ?? string.Empty;
                var type = cell.Attribute("t")?.Value;
                if (type == "s" && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                {
                    value = sharedStrings[sharedIndex];
                }
                else if (type == "b")
                {
                    value = value == "1" ? "true" : "false";
                }

                if (value.Length > limits.MaximumCellLength)
                {
                    throw new ImportDataException("CELL_TOO_LONG", "Ô dữ liệu vượt quá giới hạn độ dài.");
                }

                cells[column] = value;
            }

            var width = cells.Length;
            while (width > 0 && cells[width - 1] is null) width--;
            records.Add(new RawRecord(rowNumber, cells[..width].Select(value => value ?? string.Empty).ToArray(), formulas));
            if (records.Count > limits.MaximumDataRowCount + 1)
            {
                throw new ImportDataException("ROW_LIMIT_EXCEEDED", "Worksheet vượt quá giới hạn số dòng.");
            }
        }

        return records;
    }

    private static List<RawRecord> ParseCsvRecords(string content, char delimiter, ProductImportLimits limits, CancellationToken cancellationToken)
    {
        var records = new List<RawRecord>();
        var values = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var rowStart = 1;
        var fieldStart = true;

        void AddCell()
        {
            if (cell.Length > limits.MaximumCellLength) throw new ImportDataException("CELL_TOO_LONG", "Ô dữ liệu vượt quá giới hạn độ dài.");
            values.Add(cell.ToString());
            cell.Clear();
            fieldStart = true;
        }

        void AddRecord()
        {
            AddCell();
            if (values.Count > limits.MaximumColumnCount) throw new ImportDataException("COLUMN_LIMIT_EXCEEDED", "CSV vượt quá giới hạn số cột.");
            records.Add(new RawRecord(rowStart, values.ToArray(), new HashSet<int>()));
            values.Clear();
            rowStart = line + 1;
            if (records.Count > limits.MaximumDataRowCount + 1) throw new ImportDataException("ROW_LIMIT_EXCEEDED", "CSV vượt quá giới hạn số dòng.");
        }

        for (var index = 0; index < content.Length; index++)
        {
            if ((index & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            var character = content[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        cell.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (character == '\n') line++;
                    cell.Append(character);
                }

                continue;
            }

            if (character == '"' && fieldStart)
            {
                inQuotes = true;
                fieldStart = false;
            }
            else if (character == delimiter)
            {
                AddCell();
            }
            else if (character == '\r' || character == '\n')
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n') index++;
                AddRecord();
                line++;
            }
            else
            {
                cell.Append(character);
                fieldStart = false;
            }
        }

        if (inQuotes) throw new ImportDataException("CSV_MALFORMED", "CSV có chuỗi ngoặc kép chưa đóng.");
        if (cell.Length > 0 || values.Count > 0 || (content.Length > 0 && !content.EndsWith('\n') && !content.EndsWith('\r')))
        {
            AddRecord();
        }

        return records;
    }

    private static char DetectDelimiter(string content)
    {
        var counts = new Dictionary<char, int> { [','] = 0, [';'] = 0, ['\t'] = 0 };
        var inQuotes = false;
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < content.Length && content[index + 1] == '"') index++;
                else inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && (character == '\r' || character == '\n')) break;
            if (!inQuotes && counts.TryGetValue(character, out var count)) counts[character] = count + 1;
        }

        var nonZero = counts.Where(pair => pair.Value > 0).OrderByDescending(pair => pair.Value).ToArray();
        if (nonZero.Length == 0) return ',';
        if (nonZero.Length > 1 && nonZero[0].Value == nonZero[1].Value)
        {
            throw new ImportDataException("CSV_DELIMITER_AMBIGUOUS", "Không thể nhận diện duy nhất dấu phân cách CSV.");
        }

        return nonZero[0].Key;
    }

    private static async Task<XDocument> ReadXmlAsync(ZipArchiveEntry entry, ProductImportLimits limits, CancellationToken cancellationToken)
    {
        var bytes = await ReadEntryBytesAsync(entry, limits.MaximumXlsxEntryBytes, cancellationToken);
        using var memory = new MemoryStream(bytes, writable: false);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = limits.MaximumXlsxEntryBytes
        };
        using var reader = XmlReader.Create(memory, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static async Task<byte[]> ReadEntryBytesAsync(ZipArchiveEntry entry, long maxBytes, CancellationToken cancellationToken)
    {
        if (entry.Length > maxBytes) throw new ImportDataException("WORKBOOK_ENTRY_TOO_LARGE", "Thành phần XLSX vượt quá giới hạn.");
        await using var source = entry.Open();
        using var destination = new MemoryStream((int)Math.Min(entry.Length, int.MaxValue));
        var buffer = new byte[32 * 1024];
        var total = 0L;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes) throw new ImportDataException("WORKBOOK_ENTRY_TOO_LARGE", "Thành phần XLSX vượt quá giới hạn.");
            destination.Write(buffer, 0, read);
        }
        return destination.ToArray();
    }

    private static async Task<IReadOnlyList<string>> ReadSharedStringsAsync(ZipArchiveEntry? entry, ProductImportLimits limits, CancellationToken cancellationToken)
    {
        if (entry is null) return [];
        var document = await ReadXmlAsync(entry, limits, cancellationToken);
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;
        return document.Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static async Task<Dictionary<string, string>> ReadWorkbookRelationshipsAsync(ZipArchiveEntry entry, ProductImportLimits limits, CancellationToken cancellationToken)
    {
        var document = await ReadXmlAsync(entry, limits, cancellationToken);
        return document.Root?.Elements().Where(element => element.Name.LocalName == "Relationship")
            .Where(element => element.Attribute("Id") is not null && element.Attribute("Target") is not null)
            .ToDictionary(element => element.Attribute("Id")!.Value, element => element.Attribute("Target")!.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static ZipArchiveEntry? FindEntry(IEnumerable<ZipArchiveEntry> entries, string name) => entries.FirstOrDefault(entry => string.Equals(entry.FullName, name, StringComparison.OrdinalIgnoreCase));

    private static string? ResolveInternalXlsxPath(string target)
    {
        if (target.Contains(':', StringComparison.Ordinal) || target.StartsWith('/') || target.StartsWith("..", StringComparison.Ordinal)) return null;
        var path = target.Replace('\\', '/');
        if (!path.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) path = "xl/" + path;
        return path;
    }

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
        {
            index = checked(index * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }
        return index - 1;
    }

    private static bool TryParseUnsignedInteger(string value, out long parsed, out string code)
    {
        parsed = 0;
        code = "NUMBER_INVALID";
        if (value.Length > 0 && value[0] == '-')
        {
            code = "NUMBER_NEGATIVE";
            return false;
        }
        if (value.Length == 0 || value.Any(character => character is < '0' or > '9')) return false;
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed))
        {
            code = "NUMBER_OVERFLOW";
            return false;
        }
        return true;
    }

    private static ProductImportIssue Error(string code, string message, RawRecord record, string field) => new(ProductImportIssueSeverity.Error, code, message, record.SourceRowNumber, field);
    private static ProductImportIssue Warning(string code, string message, RawRecord record, string field) => new(ProductImportIssueSeverity.Warning, code, message, record.SourceRowNumber, field);

    private static string SafeHeader(string value) => SafeCell(value);

    private static string SafeCell(string value)
    {
        var sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 80 ? sanitized : sanitized[..80] + "…";
    }

    private static async Task<byte[]> ReadSignatureAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4];
        var read = await stream.ReadAsync(buffer, cancellationToken);
        return buffer[..read];
    }

    private static bool IsZipSignature(byte[] signature) => signature.Length >= 4 && signature[0] == 0x50 && signature[1] == 0x4B && signature[2] is 0x03 or 0x05 or 0x07 && signature[3] is 0x04 or 0x06 or 0x08;
    private static bool LooksLikeBinaryOrZip(byte[] signature) => signature.Length >= 2 && ((signature[0] == 0x4D && signature[1] == 0x5A) || (signature[0] == 0x50 && signature[1] == 0x4B));

    private static bool TryGetRegularFile(string path, out FileInfo fileInfo)
    {
        fileInfo = new FileInfo(path);
        try
        {
            if (!fileInfo.Exists || fileInfo.Attributes.HasFlag(FileAttributes.Directory | FileAttributes.ReparsePoint)) return false;
            var directory = fileInfo.Directory;
            while (directory is not null)
            {
                if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
                directory = directory.Parent;
            }
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static void ValidateLimits(ProductImportLimits limits)
    {
        if (limits.MaximumFileSizeBytes <= 0 || limits.MaximumWorksheetCount <= 0 || limits.MaximumDataRowCount <= 0 || limits.MaximumColumnCount <= 0 || limits.MaximumCellLength <= 0 || limits.MaximumPreviewRowCount < 0 || limits.MaximumXlsxEntryBytes <= 0 || limits.MaximumXlsxUncompressedBytes < limits.MaximumXlsxEntryBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "Giới hạn import không hợp lệ.");
        }
    }

    private static ProductImportPreviewResult Failure(ProductImportFileMetadata metadata, string code, string message, ProductImportFormat format = ProductImportFormat.Unknown) => CreateResult(metadata, format, [], [new ProductImportIssue(ProductImportIssueSeverity.Error, code, message)], [], new ProductImportSummary(0, 0, 0, 0, 1, 0, 0, 0));

    private static ProductImportPreviewResult CreateResult(
        ProductImportFileMetadata metadata,
        ProductImportFormat format,
        IReadOnlyList<ProductImportHeader> headers,
        IReadOnlyList<ProductImportIssue> fileIssues,
        IReadOnlyList<ProductImportRow> rows,
        ProductImportSummary summary,
        IReadOnlyList<ProductImportRow>? validatedRows = null,
        ProductImportReferenceData? references = null,
        string? selectedWorksheetName = null,
        IReadOnlyList<string>? worksheetNames = null,
        IReadOnlyList<ProductImportColumnMapping>? columnMappings = null)
    {
        return new ProductImportPreviewResult(
            metadata,
            format,
            headers,
            fileIssues,
            rows,
            summary)
        {
            ValidatedRows = validatedRows ?? rows,
            ReferenceSnapshot = references is null
                ? null
                : new ProductImportReferenceSnapshot(
                    references.CategoryIdsByNormalizedName is null
                        ? null
                        : new Dictionary<string, int>(
                            references.CategoryIdsByNormalizedName,
                            StringComparer.Ordinal),
                    references.KnownUnitNames is null
                        ? null
                        : new HashSet<string>(
                            references.KnownUnitNames,
                            StringComparer.OrdinalIgnoreCase))
            ,
            SelectedWorksheetName = selectedWorksheetName,
            WorksheetNames = worksheetNames ?? [],
            ColumnMappings = columnMappings
        };
    }

    private sealed record RawRecord(int SourceRowNumber, IReadOnlyList<string> Values, IReadOnlySet<int> FormulaColumns);

    private sealed class ImportDataException(string code, string safeMessage) : Exception
    {
        public string Code { get; } = code;
        public string SafeMessage { get; } = safeMessage;
    }
}
