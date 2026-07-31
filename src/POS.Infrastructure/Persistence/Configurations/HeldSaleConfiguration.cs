using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class HeldSaleConfiguration : IEntityTypeConfiguration<HeldSale>
{
    private static readonly ValueConverter<DateTimeOffset?, long?> NullableTime = new(
        value => value.HasValue ? value.Value.ToUniversalTime().ToUnixTimeMilliseconds() : null,
        value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);

    public void Configure(EntityTypeBuilder<HeldSale> builder)
    {
        builder.ToTable("HeldSales", table =>
        {
            table.HasCheckConstraint("CK_HeldSales_Status",
                $"\"Status\" IN ({(int)HeldSaleStatus.Active}, {(int)HeldSaleStatus.Completed}, {(int)HeldSaleStatus.Cancelled})");
            table.HasCheckConstraint("CK_HeldSales_Fingerprint",
                "length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" NOT GLOB '*[^0-9A-F]*'");
            table.HasCheckConstraint("CK_HeldSales_State",
                $"(\"Status\" = {(int)HeldSaleStatus.Active} AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR " +
                $"(\"Status\" = {(int)HeldSaleStatus.Completed} AND \"CompletedAtUtc\" IS NOT NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NOT NULL) OR " +
                $"(\"Status\" = {(int)HeldSaleStatus.Cancelled} AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NOT NULL AND \"CompletedOrderId\" IS NULL)");
            table.HasCheckConstraint("CK_HeldSales_CreatedBy", "\"CreatedByUserId\" > 0");
            table.HasCheckConstraint("CK_HeldSales_DiscountAmount",
                "\"ResolvedDiscountAmountSnapshot\" >= 0 AND \"ResolvedDiscountAmountSnapshot\" <= \"SubtotalSnapshot\"");
            table.HasCheckConstraint("CK_HeldSales_TotalEquation",
                "\"TotalSnapshot\" = \"SubtotalSnapshot\" - \"ResolvedDiscountAmountSnapshot\"");
        });
        builder.ConfigureAuditableEntity();
        builder.Property(value => value.ClientRequestId).IsRequired();
        builder.Property(value => value.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(value => value.DisplayCode).HasMaxLength(BusinessRules.HeldSales.DisplayCodeMaxLength)
            .UseCollation("NOCASE").IsRequired();
        builder.Property(value => value.Label).HasMaxLength(BusinessRules.HeldSales.LabelMaxLength).IsRequired();
        builder.Property(value => value.Notes).HasMaxLength(BusinessRules.HeldSales.NotesMaxLength);
        builder.Property(value => value.Status).HasConversion<int>().IsRequired();
        builder.Property(value => value.CompletedAtUtc).HasConversion(NullableTime).HasColumnType("INTEGER");
        builder.Property(value => value.CancelledAtUtc).HasConversion(NullableTime).HasColumnType("INTEGER");
        builder.Property(value => value.DiscountType).HasConversion<int>().IsRequired();
        builder.Property(value => value.RequestedDiscountValue).HasColumnType("INTEGER").IsRequired();
        builder.Property(value => value.DiscountReason).HasMaxLength(200);
        builder.Property(value => value.ResolvedDiscountAmountSnapshot).HasColumnType("INTEGER").IsRequired();
        builder.Property(value => value.SubtotalSnapshot).HasColumnType("INTEGER").IsRequired();
        builder.Property(value => value.TotalSnapshot).HasColumnType("INTEGER").IsRequired();
        builder.HasIndex(value => value.ClientRequestId).IsUnique()
            .HasDatabaseName("UX_HeldSales_ClientRequestId");
        builder.HasIndex(value => value.DisplayCode).IsUnique()
            .HasDatabaseName("UX_HeldSales_DisplayCode");
        builder.HasIndex(value => value.CompletedOrderId).IsUnique()
            .HasFilter("\"CompletedOrderId\" IS NOT NULL")
            .HasDatabaseName("UX_HeldSales_CompletedOrderId");
        builder.HasIndex(value => new { value.Status, value.UpdatedAtUtc })
            .HasDatabaseName("IX_HeldSales_Status_UpdatedAtUtc");
        builder.HasIndex(value => new { value.CreatedByUserId, value.Status, value.UpdatedAtUtc })
            .HasDatabaseName("IX_HeldSales_CreatedBy_Status_UpdatedAtUtc");
        builder.HasOne(value => value.CreatedByUser).WithMany()
            .HasForeignKey(value => value.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.CompletedOrder).WithMany()
            .HasForeignKey(value => value.CompletedOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(value => value.Lines).WithOne(value => value.HeldSale)
            .HasForeignKey(value => value.HeldSaleId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(value => value.Lines).HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
