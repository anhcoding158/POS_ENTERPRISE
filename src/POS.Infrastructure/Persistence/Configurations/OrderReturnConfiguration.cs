using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class OrderReturnConfiguration :
    IEntityTypeConfiguration<OrderReturn>
{
    public void Configure(EntityTypeBuilder<OrderReturn> builder)
    {
        builder.ToTable("OrderReturns", table =>
        {
            table.HasCheckConstraint("CK_OrderReturns_TotalRefundAmount", "\"TotalRefundAmount\" > 0");
            table.HasCheckConstraint("CK_OrderReturns_OrderId", "\"OrderId\" > 0");
            table.HasCheckConstraint("CK_OrderReturns_ProcessedByUserId", "\"ProcessedByUserId\" > 0");
            table.HasCheckConstraint(
                "CK_OrderReturns_RequestFingerprint",
                "length(\"RequestFingerprint\") = 64 AND " +
                "\"RequestFingerprint\" NOT GLOB '*[^0-9A-F]*'");
        });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.ClientRequestId).IsRequired();
        builder.Property(entity => entity.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(entity => entity.Reason).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.RefundMethod).HasConversion<int>().IsRequired();
        builder.Property(entity => entity.RefundReference).HasMaxLength(200);
        builder.Property(entity => entity.TotalRefundAmount).HasColumnType("INTEGER").IsRequired();
        builder.Property(entity => entity.CreatedAtUtc)
            .HasConversion(new ValueConverter<DateTimeOffset, long>(
                value => value.ToUniversalTime().ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value)))
            .HasColumnType("INTEGER").IsRequired();
        builder.HasIndex(entity => entity.ClientRequestId).IsUnique()
            .HasDatabaseName("UX_OrderReturns_ClientRequestId");
        builder.HasIndex(entity => new { entity.OrderId, entity.CreatedAtUtc })
            .HasDatabaseName("IX_OrderReturns_Order_CreatedAtUtc");
        builder.HasOne(entity => entity.Order).WithMany()
            .HasForeignKey(entity => entity.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ProcessedByUser).WithMany()
            .HasForeignKey(entity => entity.ProcessedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entity => entity.Items).WithOne(entity => entity.OrderReturn)
            .HasForeignKey(entity => entity.OrderReturnId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entity => entity.Items).HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
