using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class OrderReturnBalanceConfiguration :
    IEntityTypeConfiguration<OrderReturnBalance>
{
    public void Configure(EntityTypeBuilder<OrderReturnBalance> builder)
    {
        builder.ToTable("OrderReturnBalances", table =>
        {
            table.HasCheckConstraint("CK_OrderReturnBalances_ReturnedQuantity", "\"ReturnedQuantity\" >= 0");
            table.HasCheckConstraint("CK_OrderReturnBalances_RefundedAmount", "\"RefundedAmount\" >= 0");
        });
        builder.HasKey(entity => entity.OrderItemId);
        builder.Property(entity => entity.RefundedAmount).HasColumnType("INTEGER").IsRequired();
        builder.Property(entity => entity.ConcurrencyToken).IsConcurrencyToken().IsRequired();
        builder.HasOne(entity => entity.OrderItem).WithOne()
            .HasForeignKey<OrderReturnBalance>(entity => entity.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
