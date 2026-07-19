using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping Category sang bảng Categories trong SQLite.
/// </summary>
public sealed class CategoryConfiguration :
    IEntityTypeConfiguration<Category>
{
    public void Configure(
        EntityTypeBuilder<Category> builder)
    {
        builder.ToTable(
            "Categories",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Categories_DisplayOrder_Range",
                    $"\"DisplayOrder\" >= 0 AND " +
                    $"\"DisplayOrder\" <= " +
                    $"{BusinessRules.Categories.MaximumDisplayOrder}");
            });

        builder.ConfigureAuditableEntity();

        builder.Property(category => category.Name)
            .HasMaxLength(
                BusinessRules.Categories.NameMaxLength)
            .UseCollation("NOCASE")
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(
                BusinessRules.Categories.DescriptionMaxLength);

        builder.Property(category => category.DisplayOrder)
            .IsRequired();

        builder.Property(category => category.IsActive)
            .IsRequired();

        /*
         * Không cho phép hai danh mục có cùng tên.
         *
         * NOCASE giúp mã ASCII và tên Latin thông thường
         * không phân biệt chữ hoa/chữ thường trong SQLite.
         */
        builder.HasIndex(category => category.Name)
            .IsUnique()
            .HasDatabaseName(
                "UX_Categories_Name");

        /*
         * Phục vụ màn hình lấy danh mục đang hoạt động
         * theo đúng thứ tự hiển thị.
         */
        builder.HasIndex(
                category => new
                {
                    category.IsActive,
                    category.DisplayOrder,
                    category.Name
                })
            .HasDatabaseName(
                "IX_Categories_Active_DisplayOrder_Name");

        builder.HasMany(category => category.Products)
            .WithOne(product => product.Category)
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        /*
         * Category.Products chỉ công khai IReadOnlyCollection.
         * EF Core thao tác trực tiếp với backing field _products.
         */
        builder.Navigation(category => category.Products)
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }
}