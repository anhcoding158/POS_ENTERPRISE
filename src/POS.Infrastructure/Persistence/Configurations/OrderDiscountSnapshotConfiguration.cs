using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Services;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class OrderDiscountSnapshotConfiguration :
    IEntityTypeConfiguration<OrderDiscountSnapshot>
{
    public void Configure(EntityTypeBuilder<OrderDiscountSnapshot> builder)
    {
        var time = new ValueConverter<DateTimeOffset, long>(
            value => value.ToUniversalTime().ToUnixTimeMilliseconds(),
            value => DateTimeOffset.FromUnixTimeMilliseconds(value));
        builder.ToTable("OrderDiscountSnapshots", table =>
        {
            table.HasCheckConstraint("CK_OrderDiscountSnapshots_Type",
                $"\"Type\" IN ({(int)SalesDiscountType.FixedAmount}, {(int)SalesDiscountType.Percentage})");
            table.HasCheckConstraint("CK_OrderDiscountSnapshots_RequestedValue", "\"RequestedValue\" > 0");
            table.HasCheckConstraint("CK_OrderDiscountSnapshots_ResolvedAmount", "\"ResolvedAmount\" > 0");
            table.HasCheckConstraint("CK_OrderDiscountSnapshots_AppliedByUserId", "\"AppliedByUserId\" > 0");
            table.HasCheckConstraint("CK_OrderDiscountSnapshots_Reason", "length(trim(\"Reason\")) > 0");
        });
        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.RequestedValue).HasColumnType("INTEGER").IsRequired();
        builder.Property(x => x.ResolvedAmount).HasColumnType("INTEGER").IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(SalesDiscountCalculator.MaximumReasonLength).IsRequired();
        builder.Property(x => x.AppliedAtUtc).HasConversion(time).HasColumnType("INTEGER").IsRequired();
        builder.HasIndex(x => x.OrderId).IsUnique().HasDatabaseName("UX_OrderDiscountSnapshots_OrderId");
        builder.HasOne(x => x.Order).WithOne(x => x.DiscountSnapshot)
            .HasForeignKey<OrderDiscountSnapshot>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(x => x.AppliedByUser).WithMany()
            .HasForeignKey(x => x.AppliedByUserId)
            .OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
