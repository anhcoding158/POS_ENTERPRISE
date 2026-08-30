using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using POS.Application.Abstractions.Exports;
using POS.Application.DTOs.Exports;

namespace POS.Infrastructure.Exports;

/// <summary>
/// Writes only typed export cells. User data is never emitted as an XLSX formula
/// or external-link relationship.
/// </summary>
public sealed class ProductExportFileWriter : IProductExportWriter
{
    public async Task WriteAsync(
        ProductExportData data,
        ProductExportFormat format,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Đường dẫn tệp không hợp lệ.", nameof(destinationPath));
        }

        var finalPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(finalPath)
            ?? throw new ArgumentException("Không xác định được thư mục đích.", nameof(destinationPath));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            if (format == ProductExportFormat.Csv)
            {
                await WriteCsvAsync(data, temporaryPath, cancellationToken);
            }
            else if (format == ProductExportFormat.Xlsx)
            {
                await WriteXlsxAsync(data, temporaryPath, cancellationToken);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(format));
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath, overwrite: true);
        }
        finally
        {
            TryDeleteTemporary(temporaryPath);
        }
    }

    private static async Task WriteCsvAsync(
        ProductExportData data,
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32 * 1024, useAsync: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 32 * 1024, leaveOpen: false);

        await writer.WriteLineAsync(string.Join(',', data.Columns.Select(column => Csv(column.Header))));
        foreach (var row in data.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.Cells.Count != data.Columns.Count)
            {
                throw new InvalidDataException("Số cột trong dòng xuất không khớp tiêu đề.");
            }

            await writer.WriteLineAsync(string.Join(',', row.Cells.Select(Csv)));
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static async Task WriteXlsxAsync(
        ProductExportData data,
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32 * 1024, useAsync: true);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        await WriteXmlEntryAsync(archive, "[Content_Types].xml", writer =>
        {
            writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
            Override(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            Override(writer, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            if (data.Instructions.Count > 0)
            {
                Override(writer, "/xl/worksheets/sheet2.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            }

            Override(writer, "/_rels/.rels", "application/vnd.openxmlformats-package.relationships+xml");
            Override(writer, "/xl/_rels/workbook.xml.rels", "application/vnd.openxmlformats-package.relationships+xml");
            writer.WriteEndElement();
        }, cancellationToken);

        await WriteXmlEntryAsync(archive, "_rels/.rels", writer =>
        {
            writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            Relationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
            writer.WriteEndElement();
        }, cancellationToken);

        await WriteXmlEntryAsync(archive, "xl/workbook.xml", writer =>
        {
            writer.WriteStartElement("workbook", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            writer.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            writer.WriteStartElement("sheets");
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", "Products");
            writer.WriteAttributeString("sheetId", "1");
            writer.WriteAttributeString("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", "rId1");
            writer.WriteEndElement();
            if (data.Instructions.Count > 0)
            {
                writer.WriteStartElement("sheet");
                writer.WriteAttributeString("name", "Hướng dẫn");
                writer.WriteAttributeString("sheetId", "2");
                writer.WriteAttributeString("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", "rId2");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }, cancellationToken);

        await WriteXmlEntryAsync(archive, "xl/_rels/workbook.xml.rels", writer =>
        {
            writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            Relationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet1.xml");
            if (data.Instructions.Count > 0)
            {
                Relationship(writer, "rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet2.xml");
            }

            writer.WriteEndElement();
        }, cancellationToken);

        await WriteXmlEntryAsync(archive, "xl/worksheets/sheet1.xml", writer => WriteDataSheet(writer, data), cancellationToken);
        if (data.Instructions.Count > 0)
        {
            await WriteXmlEntryAsync(archive, "xl/worksheets/sheet2.xml", writer => WriteInstructionsSheet(writer, data), cancellationToken);
        }
    }

    private static void WriteDataSheet(XmlWriter writer, ProductExportData data)
    {
        writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteStartElement("sheetViews");
        writer.WriteStartElement("sheetView");
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane");
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cols");
        for (var index = 0; index < data.Columns.Count; index++)
        {
            writer.WriteStartElement("col");
            writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", (data.Columns[index].Header.Length > 20 ? 22 : 16).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("bestFit", "1");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteStartElement("sheetData");
        WriteSheetRow(writer, 1, data.Columns.Select(column => ProductExportCell.TextValue(column.Header)).ToArray());
        for (var index = 0; index < data.Rows.Count; index++)
        {
            WriteSheetRow(writer, index + 2, data.Rows[index].Cells);
        }

        writer.WriteEndElement();
        var lastColumn = ExcelColumn(data.Columns.Count);
        var lastRow = Math.Max(1, data.Rows.Count + 1);
        writer.WriteStartElement("autoFilter");
        writer.WriteAttributeString("ref", $"A1:{lastColumn}{lastRow}");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteInstructionsSheet(XmlWriter writer, ProductExportData data)
    {
        writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteStartElement("sheetData");
        for (var index = 0; index < data.Instructions.Count; index++)
        {
            WriteSheetRow(writer, index + 1, [ProductExportCell.TextValue(data.Instructions[index])]);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteSheetRow(XmlWriter writer, int rowNumber, IReadOnlyList<ProductExportCell> cells)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", $"{ExcelColumn(index + 1)}{rowNumber}");
            if (cell.Type == ProductExportCellType.Number && cell.Number.HasValue)
            {
                writer.WriteElementString("v", cell.Number.Value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteStartElement("is");
                writer.WriteElementString("t", CellText(cell));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static string Csv(ProductExportColumn column) => Csv(column.Header);

    private static string Csv(ProductExportCell cell) => Csv(CellText(cell), cell.Type == ProductExportCellType.Text);

    private static string Csv(string value) => Csv(value, text: true);

    private static string Csv(string value, bool text)
    {
        var safe = text ? SafeText(value) : value;
        return $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string CellText(ProductExportCell cell) => cell.Type switch
    {
        ProductExportCellType.Number => cell.Number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        ProductExportCellType.DateTime => cell.DateTime?.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? string.Empty,
        _ => SafeText(cell.Text ?? string.Empty)
    };

    private static string SafeText(string value)
    {
        var cleaned = new string(value.Where(character => character is '\t' or '\r' or '\n' || !char.IsControl(character)).ToArray());
        if (cleaned.TrimStart() is [var first, ..] && first is '=' or '+' or '-' or '@')
        {
            return $"'{cleaned}";
        }

        return cleaned;
    }

    private static string ExcelColumn(int number)
    {
        var result = string.Empty;
        while (number > 0)
        {
            number--;
            result = (char)('A' + number % 26) + result;
            number /= 26;
        }

        return result;
    }

    private static async Task WriteXmlEntryAsync(
        ZipArchive archive,
        string name,
        Action<XmlWriter> write,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        await using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Async = true, Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false });
        write(writer);
        await writer.FlushAsync();
    }

    private static void Override(XmlWriter writer, string partName, string contentType)
    {
        writer.WriteStartElement("Override");
        writer.WriteAttributeString("PartName", partName);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void Relationship(XmlWriter writer, string id, string type, string target)
    {
        writer.WriteStartElement("Relationship");
        writer.WriteAttributeString("Id", id);
        writer.WriteAttributeString("Type", type);
        writer.WriteAttributeString("Target", target);
        writer.WriteEndElement();
    }

    private static void TryDeleteTemporary(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Never mask the export error with best-effort temporary cleanup.
        }
    }
}
