using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping dòng sản phẩm đã được đóng băng trong hóa đơn.
/// </summary>
public sealed class OrderItemConfiguration :
    IEntityTypeConfiguration<OrderItem>
{
    public void Configure(
        EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable(
            "OrderItems",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_OrderItems_OrderId_Positive",
                    "\"OrderId\" > 0");

                table.HasCheckConstraint(
                    "CK_OrderItems_ProductId_Positive",
                    "\"ProductId\" > 0");

                table.HasCheckConstraint(
                    "CK_OrderItems_Quantity_Range",
                    "\"Quantity\" > 0 AND " +
                    $"\"Quantity\" <= " +
                    $"{BusinessRules.Orders.MaximumLineQuantity}");

                table.HasCheckConstraint(
                    "CK_OrderItems_UnitCostPrice_Range",
                    "\"UnitCostPrice\" >= 0 AND " +
                    $"\"UnitCostPrice\" <= " +
                    $"{BusinessRules.Products.MaximumPrice}");

                table.HasCheckConstraint(
                    "CK_OrderItems_UnitSalePrice_Range",
                    "\"UnitSalePrice\" >= 0 AND " +
                    $"\"UnitSalePrice\" <= " +
                    $"{BusinessRules.Products.MaximumPrice}");

                table.HasCheckConstraint(
                    "CK_OrderItems_LineDiscount_NonNegative",
                    "\"LineDiscountAmount\" >= 0");

                table.HasCheckConstraint(
                    "CK_OrderItems_Status_Valid",
                    $"\"Status\" IN (" +
                    $"{(int)OrderItemStatus.Active}, " +
                    $"{(int)OrderItemStatus.Cancelled}, " +
                    $"{(int)OrderItemStatus.PartiallyRefunded}, " +
                    $"{(int)OrderItemStatus.Refunded})");

                table.HasCheckConstraint(
                    "CK_OrderItems_RefundedQuantity_Range",
                    "\"RefundedQuantity\" >= 0 AND " +
                    "\"RefundedQuantity\" <= \"Quantity\"");
            });

        builder.ConfigureAuditableEntity();

        builder.Property(
                item =>
                    item.OrderId)
            .IsRequired();

        builder.Property(
                item =>
                    item.ProductId)
            .IsRequired();

        builder.Property(
                item =>
                    item.ProductCode)
            .HasMaxLength(
                BusinessRules.Products
                    .CodeMaxLength)
            .UseCollation(
                "NOCASE")
            .IsRequired();

        builder.Property(
                item =>
                    item.ProductName)
            .HasMaxLength(
                BusinessRules.Products
                    .NameMaxLength)
            .IsRequired();

        builder.Property(
                item =>
                    item.UnitName)
            .HasMaxLength(
                BusinessRules.Products
                    .UnitNameMaxLength)
            .IsRequired();

        builder.Property(
                item =>
                    item.Quantity)
            .IsRequired();

        builder.Property(
                item =>
                    item.UnitCostPrice)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Property(
                item =>
                    item.UnitSalePrice)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Property(
                item =>
                    item.LineDiscountAmount)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Property(
                item =>
                    item.Notes)
            .HasMaxLength(
                BusinessRules.Orders
                    .NotesMaxLength);

        builder.Property(
                item =>
                    item.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(
                item =>
                    item.RefundedQuantity)
            .IsRequired();

        builder.Ignore(
            item =>
                item.ModifierAmountPerUnit);

        builder.Ignore(
            item =>
                item.FinalUnitPrice);

        builder.Ignore(
            item =>
                item.GrossAmount);

        builder.Ignore(
            item =>
                item.NetAmount);

        builder.Ignore(
            item =>
                item.CostAmount);

        builder.Ignore(
            item =>
                item.GrossProfit);

        builder.Ignore(
            item =>
                item.RemainingRefundableQuantity);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(
                item =>
                    item.ProductId)
            .OnDelete(
                DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasMany(
                item =>
                    item.Modifiers)
            .WithOne(
                modifier =>
                    modifier.OrderItem)
            .HasForeignKey(
                modifier =>
                    modifier.OrderItemId)
            .OnDelete(
                DeleteBehavior.Cascade)
            .IsRequired();

        builder.Navigation(
                item =>
                    item.Modifiers)
            .HasField(
                "_modifiers")
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);

        builder.HasIndex(
                item =>
                    item.OrderId)
            .HasDatabaseName(
                "IX_OrderItems_OrderId");

        builder.HasIndex(
                item =>
                    new
                    {
                        item.ProductId,
                        item.CreatedAtUtc
                    })
            .HasDatabaseName(
                "IX_OrderItems_Product_CreatedAtUtc");
    }
}
