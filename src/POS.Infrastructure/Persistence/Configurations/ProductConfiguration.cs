using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping Product sang bảng Products trong SQLite.
/// </summary>
public sealed class ProductConfiguration :
    IEntityTypeConfiguration<Product>
{
    public void Configure(
        EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(
            "Products",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Products_CostPrice_Range",
                    $"\"CostPrice\" >= 0 AND " +
                    $"\"CostPrice\" <= " +
                    $"{BusinessRules.Products.MaximumPrice}");

                table.HasCheckConstraint(
                    "CK_Products_SalePrice_Range",
                    $"\"SalePrice\" >= 0 AND " +
                    $"\"SalePrice\" <= " +
                    $"{BusinessRules.Products.MaximumPrice}");

                table.HasCheckConstraint(
                    "CK_Products_StockQuantity_Range",
                    $"\"StockQuantity\" >= " +
                    $"-{BusinessRules.Products.MaximumStockQuantity} " +
                    $"AND \"StockQuantity\" <= " +
                    $"{BusinessRules.Products.MaximumStockQuantity}");

                table.HasCheckConstraint(
                    "CK_Products_MinimumStock_Range",
                    $"\"MinimumStock\" >= 0 AND " +
                    $"\"MinimumStock\" <= " +
                    $"{BusinessRules.Products.MaximumStockQuantity}");

                /*
                 * Chỉ sản phẩm theo dõi kho mới được phép
                 * bật AllowNegativeStock.
                 */
                table.HasCheckConstraint(
                    "CK_Products_AllowNegativeStock_RequiresTracking",
                    "\"AllowNegativeStock\" = 0 OR " +
                    "\"TrackInventory\" = 1");

                /*
                 * Khi không cho phép tồn âm,
                 * StockQuantity phải từ 0 trở lên.
                 */
                table.HasCheckConstraint(
                    "CK_Products_NegativeStock_Rule",
                    "\"AllowNegativeStock\" = 1 OR " +
                    "\"StockQuantity\" >= 0");

                /*
                 * Sản phẩm không theo dõi kho không sử dụng
                 * ngưỡng cảnh báo tồn tối thiểu.
                 */
                table.HasCheckConstraint(
                    "CK_Products_MinimumStock_RequiresTracking",
                    "\"TrackInventory\" = 1 OR " +
                    "\"MinimumStock\" = 0");
            });

        builder.ConfigureAuditableEntity();

        builder.Property(product => product.CategoryId)
            .IsRequired();

        builder.Property(product => product.Code)
            .HasMaxLength(
                BusinessRules.Products.CodeMaxLength)
            .UseCollation("NOCASE")
            .IsRequired();

        builder.Property(product => product.Barcode)
            .HasMaxLength(
                BusinessRules.Products.BarcodeMaxLength);

        builder.Property(product => product.Name)
            .HasMaxLength(
                BusinessRules.Products.NameMaxLength)
            .UseCollation("NOCASE")
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(
                BusinessRules.Products.DescriptionMaxLength);

        builder.Property(product => product.UnitName)
            .HasMaxLength(
                BusinessRules.Products.UnitNameMaxLength)
            .IsRequired();

        builder.Property(product => product.ImagePath)
            .HasMaxLength(
                BusinessRules.Products.ImagePathMaxLength);

        /*
         * VND lưu bằng số nguyên long.
         *
         * Ví dụ:
         * 25.000 đồng được lưu là 25000.
         */
        builder.Property(product => product.CostPrice)
            .HasColumnType("INTEGER")
            .IsRequired();

        builder.Property(product => product.SalePrice)
            .HasColumnType("INTEGER")
            .IsRequired();

        builder.Property(product => product.StockQuantity)
            .IsRequired();

        builder.Property(product => product.MinimumStock)
            .IsRequired();

        builder.Property(product => product.TrackInventory)
            .IsRequired();

        builder.Property(product => product.AllowNegativeStock)
            .IsRequired();

        builder.Property(product => product.IsActive)
            .IsRequired();

        /*
         * Đây là các thuộc tính tính toán trong Domain,
         * không phải cột vật lý trong database.
         */
        builder.Ignore(product => product.ProfitPerUnit);

        builder.Ignore(product => product.IsOutOfStock);

        builder.Ignore(product => product.IsLowStock);

        /*
         * Mã sản phẩm là duy nhất và không phân biệt
         * chữ hoa/chữ thường nhờ NOCASE.
         */
        builder.HasIndex(product => product.Code)
            .IsUnique()
            .HasDatabaseName(
                "UX_Products_Code");

        /*
         * Barcode là optional.
         * Chỉ các bản ghi có barcode mới tham gia unique index.
         */
        builder.HasIndex(product => product.Barcode)
            .IsUnique()
            .HasFilter("\"Barcode\" IS NOT NULL")
            .HasDatabaseName(
                "UX_Products_Barcode");

        /*
         * Hỗ trợ tìm nhanh theo tên sản phẩm.
         */
        builder.HasIndex(product => product.Name)
            .HasDatabaseName(
                "IX_Products_Name");

        /*
         * Hỗ trợ màn hình:
         * danh mục → trạng thái → tên sản phẩm.
         */
        builder.HasIndex(
                product => new
                {
                    product.CategoryId,
                    product.IsActive,
                    product.Name
                })
            .HasDatabaseName(
                "IX_Products_Category_Active_Name");

        /*
         * Hỗ trợ lọc sản phẩm theo trạng thái kho.
         */
        builder.HasIndex(
                product => new
                {
                    product.TrackInventory,
                    product.IsActive,
                    product.StockQuantity
                })
            .HasDatabaseName(
                "IX_Products_Inventory_Active_Stock");
    }
}