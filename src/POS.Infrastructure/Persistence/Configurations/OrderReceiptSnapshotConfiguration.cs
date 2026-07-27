using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping bản chụp hóa đơn gốc bất biến.
/// </summary>
public sealed class
    OrderReceiptSnapshotConfiguration :
        IEntityTypeConfiguration<
            OrderReceiptSnapshot>
{
    private static readonly
        ValueConverter<DateTimeOffset, long>
        DateTimeOffsetConverter =
            new(
                value =>
                    value
                        .ToUniversalTime()
                        .ToUnixTimeMilliseconds(),

                value =>
                    DateTimeOffset
                        .FromUnixTimeMilliseconds(
                            value));

    public void Configure(
        EntityTypeBuilder<
            OrderReceiptSnapshot> builder)
    {
        builder.ToTable(
            "OrderReceiptSnapshots",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_OrderReceiptSnapshots_SnapshotVersion",
                    "\"SnapshotVersion\" > 0");

                table.HasCheckConstraint(
                    "CK_OrderReceiptSnapshots_PayloadJson_NotEmpty",
                    "length(trim(\"PayloadJson\")) > 0");
            });

        builder.HasKey(
            snapshot =>
                snapshot.OrderId);

        builder.Property(
                snapshot =>
                    snapshot.OrderId)
            .ValueGeneratedNever()
            .HasColumnType(
                "INTEGER");

        builder.Property(
                snapshot =>
                    snapshot.SnapshotVersion)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.Property(
                snapshot =>
                    snapshot.PayloadJson)
            .HasColumnType(
                "TEXT")
            .IsRequired();

        builder.Property(
                snapshot =>
                    snapshot.CreatedAtUtc)
            .HasConversion(
                DateTimeOffsetConverter)
            .HasColumnType(
                "INTEGER")
            .IsRequired();

        builder.HasOne<Order>()
            .WithOne()
            .HasForeignKey<
                OrderReceiptSnapshot>(
                    snapshot =>
                        snapshot.OrderId)
            .OnDelete(
                DeleteBehavior.Restrict)
            .IsRequired();
    }
}
