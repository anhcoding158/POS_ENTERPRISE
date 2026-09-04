using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    private static readonly ValueConverter<DateOnly, string> DateOnlyConverter =
        new(
            value => value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            value => DateOnly.ParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    private static readonly ValueConverter<DateOnly?, string?> NullableDateOnlyConverter =
        new(
            value => value.HasValue
                ? value.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                : null,
            value => string.IsNullOrWhiteSpace(value)
                ? null
                : DateOnly.ParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders", table =>
        {
            table.HasCheckConstraint(
                "CK_PurchaseOrders_Status_Valid",
                $"\"Status\" IN ({(int)PurchaseOrderStatus.Draft}, {(int)PurchaseOrderStatus.Ordered}, {(int)PurchaseOrderStatus.Cancelled})");
            table.HasCheckConstraint(
                "CK_PurchaseOrders_OrderNumber_Length",
                $"length(\"OrderNumber\") >= 1 AND length(\"OrderNumber\") <= {BusinessRules.PurchaseOrders.CodeMaxLength}");
            table.HasCheckConstraint(
                "CK_PurchaseOrders_ExpectedDate_Range",
                "\"ExpectedDeliveryDate\" IS NULL OR \"ExpectedDeliveryDate\" >= \"OrderDate\"");
            table.HasCheckConstraint(
                "CK_PurchaseOrders_CancelledAtUtc_State",
                $"(\"Status\" = {(int)PurchaseOrderStatus.Cancelled} AND \"CancelledAtUtc\" IS NOT NULL AND \"CancellationReason\" IS NOT NULL) OR " +
                $"(\"Status\" <> {(int)PurchaseOrderStatus.Cancelled} AND \"CancelledAtUtc\" IS NULL AND \"CancellationReason\" IS NULL)");
            table.HasCheckConstraint(
                "CK_PurchaseOrders_OrderedAtUtc_State",
                $"(\"Status\" = {(int)PurchaseOrderStatus.Draft} AND \"OrderedAtUtc\" IS NULL AND \"OrderedByUserId\" IS NULL) OR " +
                $"(\"Status\" = {(int)PurchaseOrderStatus.Ordered} AND \"OrderedAtUtc\" IS NOT NULL AND \"OrderedByUserId\" IS NOT NULL) OR " +
                $"\"Status\" = {(int)PurchaseOrderStatus.Cancelled}");
        });

        builder.ConfigureAuditableEntity();

        builder.Property(order => order.OrderNumber)
            .HasMaxLength(BusinessRules.PurchaseOrders.CodeMaxLength)
            .UseCollation("NOCASE")
            .IsRequired();
        builder.Property(order => order.NormalizedOrderNumber)
            .HasMaxLength(BusinessRules.PurchaseOrders.CodeMaxLength)
            .UseCollation("NOCASE")
            .IsRequired();
        builder.Property(order => order.SupplierId).IsRequired();
        builder.Property(order => order.SupplierCode)
            .HasMaxLength(BusinessRules.Suppliers.CodeMaxLength)
            .IsRequired();
        builder.Property(order => order.SupplierName)
            .HasMaxLength(BusinessRules.Suppliers.NameMaxLength)
            .IsRequired();
        builder.Property(order => order.SupplierTaxCode)
            .HasMaxLength(BusinessRules.Suppliers.TaxCodeMaxLength);
        builder.Property(order => order.OrderDate)
            .HasConversion(DateOnlyConverter)
            .HasColumnType("TEXT")
            .IsRequired();
        builder.Property(order => order.ExpectedDeliveryDate)
            .HasConversion(NullableDateOnlyConverter)
            .HasColumnType("TEXT");
        builder.Property(order => order.Notes)
            .HasMaxLength(BusinessRules.PurchaseOrders.NotesMaxLength);
        builder.Property(order => order.Status)
            .HasConversion<int>()
            .HasColumnType("INTEGER")
            .IsRequired();
        builder.Property(order => order.OrderedAtUtc)
            .HasConversion(NullableDateTimeOffsetConverter)
            .HasColumnType("INTEGER");
        builder.Property(order => order.CancelledAtUtc)
            .HasConversion(NullableDateTimeOffsetConverter)
            .HasColumnType("INTEGER");
        builder.Property(order => order.CancellationReason)
            .HasMaxLength(BusinessRules.PurchaseOrders.CancellationReasonMaxLength);

        builder.Ignore(order => order.GrandTotal);

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(order => order.SupplierId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(order => order.OrderedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(order => order.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Lines)
            .WithOne(line => line.PurchaseOrder)
            .HasForeignKey(line => line.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.Navigation(order => order.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(order => order.NormalizedOrderNumber)
            .IsUnique()
            .HasDatabaseName("UX_PurchaseOrders_NormalizedOrderNumber");
        builder.HasIndex(order => new { order.SupplierId, order.Status, order.OrderDate })
            .HasDatabaseName("IX_PurchaseOrders_Supplier_Status_OrderDate");
        builder.HasIndex(order => new { order.Status, order.ExpectedDeliveryDate })
            .HasDatabaseName("IX_PurchaseOrders_Status_ExpectedDeliveryDate");
    }

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter =
        new(
            value => value.HasValue ? value.Value.ToUniversalTime().ToUnixTimeMilliseconds() : null,
            value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);
}
