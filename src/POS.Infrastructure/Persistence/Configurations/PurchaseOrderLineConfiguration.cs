using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLines", table =>
        {
            table.HasCheckConstraint(
                "CK_PurchaseOrderLines_OrderedQuantity_Range",
                $"\"OrderedQuantity\" > 0 AND \"OrderedQuantity\" <= {BusinessRules.PurchaseOrders.MaximumLineQuantity}");
            table.HasCheckConstraint(
                "CK_PurchaseOrderLines_ReceivedQuantity_Range",
                $"\"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"OrderedQuantity\"");
            table.HasCheckConstraint(
                "CK_PurchaseOrderLines_AgreedUnitCost_Range",
                $"\"AgreedUnitCost\" >= 0 AND \"AgreedUnitCost\" <= {BusinessRules.Products.MaximumPrice}");
        });

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedOnAdd();
        builder.Property(line => line.PurchaseOrderId).IsRequired();
        builder.Property(line => line.ProductId).IsRequired();
        builder.Property(line => line.ProductCode)
            .HasMaxLength(BusinessRules.Products.CodeMaxLength)
            .IsRequired();
        builder.Property(line => line.ProductName)
            .HasMaxLength(BusinessRules.Products.NameMaxLength)
            .IsRequired();
        builder.Property(line => line.UnitName)
            .HasMaxLength(BusinessRules.Products.UnitNameMaxLength)
            .IsRequired();
        builder.Property(line => line.OrderedQuantity).IsRequired();
        builder.Property(line => line.ReceivedQuantity)
            .HasDefaultValue(0)
            .IsRequired();
        builder.Property(line => line.AgreedUnitCost)
            .HasColumnType("INTEGER")
            .IsRequired();
        builder.Property(line => line.SortOrder).IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(line => line.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasIndex(line => new { line.PurchaseOrderId, line.ProductId })
            .IsUnique()
            .HasDatabaseName("UX_PurchaseOrderLines_Order_Product");
        builder.HasIndex(line => new { line.PurchaseOrderId, line.SortOrder })
            .HasDatabaseName("IX_PurchaseOrderLines_Order_SortOrder");
        builder.HasIndex(line => line.ProductId)
            .HasDatabaseName("IX_PurchaseOrderLines_Product");
    }
}
