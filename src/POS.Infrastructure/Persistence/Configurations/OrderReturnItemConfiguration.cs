using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class OrderReturnItemConfiguration :
    IEntityTypeConfiguration<OrderReturnItem>
{
    public void Configure(EntityTypeBuilder<OrderReturnItem> builder)
    {
        builder.ToTable("OrderReturnItems", table =>
        {
            table.HasCheckConstraint("CK_OrderReturnItems_ReturnQuantity", "\"ReturnQuantity\" > 0");
            table.HasCheckConstraint("CK_OrderReturnItems_RestockQuantity", "\"RestockQuantity\" >= 0 AND \"RestockQuantity\" <= \"ReturnQuantity\"");
            table.HasCheckConstraint("CK_OrderReturnItems_RefundAmount", "\"RefundAmount\" > 0");
        });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.ProductCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.UnitName).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.RefundAmount).HasColumnType("INTEGER").IsRequired();
        builder.HasIndex(entity => entity.OrderItemId)
            .HasDatabaseName("IX_OrderReturnItems_OrderItemId");
        builder.HasOne(entity => entity.OrderItem).WithMany()
            .HasForeignKey(entity => entity.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Product).WithMany()
            .HasForeignKey(entity => entity.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
