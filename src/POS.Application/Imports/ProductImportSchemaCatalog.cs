using System.Text;
using POS.Application.DTOs.ProductImports;
using POS.Domain.Constants;

namespace POS.Application.ProductImports;

/// <summary>
/// Catalog duy nhất cho đúng 11 trường Product được source-of-truth R5.1A yêu cầu.
/// Category được nhập bằng tên để checkpoint preview không cần mở database;
/// R5.1B sẽ đối chiếu tên này với Category active trước khi ghi.
/// </summary>
public sealed record ProductImportFieldDefinition(
    string CanonicalKey,
    string VietnameseLabel,
    IReadOnlyList<string> Aliases,
    bool Required,
    ProductImportFieldType DataType,
    int? MaximumLength,
    string Normalization,
    string ValidationRule,
    string Example);

public static class ProductImportSchemaCatalog
{
    public static IReadOnlyList<ProductImportFieldDefinition> Fields { get; } =
    [
        new(
            "product_code",
            "Mã sản phẩm",
            ["mã sản phẩm", "ma san pham", "code", "product code", "productcode", "product_code", "sku", "mã hàng", "ma hang"],
            true,
            ProductImportFieldType.Text,
            BusinessRules.Products.CodeMaxLength,
            "Trim; uppercase invariant",
            "Bắt buộc; không rỗng; tối đa 50 ký tự",
            "SP001"),
        new(
            "barcode",
            "Mã vạch",
            ["mã vạch", "ma vach", "barcode", "bar code", "ean", "upc"],
            false,
            ProductImportFieldType.Barcode,
            BusinessRules.Products.BarcodeMaxLength,
            "Trim; giữ nguyên leading zero",
            "Tùy chọn; tối đa 50 ký tự; không parse số",
            "089123456789"),
        new(
            "name",
            "Tên sản phẩm",
            ["tên", "ten", "tên sản phẩm", "ten san pham", "name", "product name", "product_name"],
            true,
            ProductImportFieldType.Text,
            BusinessRules.Products.NameMaxLength,
            "Trim; giữ nguyên Unicode",
            "Bắt buộc; không rỗng; tối đa 200 ký tự",
            "Cà phê rang xay"),
        new(
            "category_name",
            "Danh mục",
            ["danh mục", "danh muc", "category", "category name", "category_name"],
            true,
            ProductImportFieldType.Text,
            BusinessRules.Categories.NameMaxLength,
            "Trim; giữ nguyên Unicode",
            "Bắt buộc; phải đối chiếu Category active trước khi import",
            "Đồ uống"),
        new(
            "unit_name",
            "Đơn vị tính",
            ["đơn vị", "don vi", "đơn vị tính", "don vi tinh", "unit", "unit name", "unit_name"],
            true,
            ProductImportFieldType.Text,
            BusinessRules.Products.UnitNameMaxLength,
            "Trim; giữ nguyên Unicode",
            "Bắt buộc; tối đa 50 ký tự; Product hiện lưu text, không có Unit entity",
            "Cái"),
        new(
            "sale_price",
            "Giá bán (VND)",
            ["giá bán", "gia ban", "giá bán (vnd)", "gia ban (vnd)", "price", "sale price", "sale_price"],
            true,
            ProductImportFieldType.VndAmount,
            null,
            "Trim; integer VND, không dùng floating point",
            "Bắt buộc; chữ số invariant không phân cách; 0..999.999.999.999",
            "35000"),
        new(
            "cost_price",
            "Giá vốn (VND)",
            ["giá vốn", "gia von", "giá vốn (vnd)", "gia von (vnd)", "cost", "cost price", "cost_price"],
            true,
            ProductImportFieldType.VndAmount,
            null,
            "Trim; integer VND, không dùng floating point",
            "Bắt buộc; chữ số invariant không phân cách; 0..999.999.999.999",
            "25000"),
        new(
            "initial_stock_quantity",
            "Tồn đầu kỳ",
            ["tồn đầu", "ton dau", "tồn đầu kỳ", "ton dau ky", "initial stock", "initial_stock", "opening stock", "opening balance"],
            false,
            ProductImportFieldType.NonNegativeInteger,
            null,
            "Trim; integer invariant",
            "Mặc định 0; 0..999.999.999",
            "20"),
        new(
            "minimum_stock",
            "Tồn tối thiểu",
            ["tồn tối thiểu", "ton toi thieu", "minimum stock", "minimum_stock", "reorder level"],
            false,
            ProductImportFieldType.NonNegativeInteger,
            null,
            "Trim; integer invariant",
            "Mặc định 0; 0..999.999.999",
            "5"),
        new(
            "is_active",
            "Trạng thái",
            ["trạng thái", "trang thai", "status", "state", "is active", "is_active", "đang bán", "dang ban"],
            false,
            ProductImportFieldType.Boolean,
            null,
            "Trim; Đang bán/Ngừng bán hoặc Có/Không hoặc true/false",
            "Mặc định Đang bán; ánh xạ Product.IsActive",
            "Đang bán"),
        new(
            "notes",
            "Ghi chú",
            ["ghi chú", "ghi chu", "mô tả", "mo ta", "description", "notes", "note"],
            false,
            ProductImportFieldType.Text,
            BusinessRules.Products.DescriptionMaxLength,
            "Trim; ô rỗng thành null; ánh xạ Product.Description",
            "Tùy chọn; tối đa 2.000 ký tự",
            "Gói 500 g")
    ];

    public static ProductImportFieldDefinition? Find(string header)
    {
        var normalized = NormalizeHeader(header);

        return Fields.FirstOrDefault(field =>
            NormalizeHeader(field.CanonicalKey) == normalized ||
            field.Aliases.Any(alias => NormalizeHeader(alias) == normalized));
    }

    public static string NormalizeHeader(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is '_' or '-' || char.IsWhiteSpace(character))
            {
                builder.Append(' ');
            }
            else
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return string.Join(
            ' ',
            builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
