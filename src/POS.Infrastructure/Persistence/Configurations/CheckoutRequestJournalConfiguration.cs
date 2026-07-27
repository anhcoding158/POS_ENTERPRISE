using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class CheckoutRequestJournalConfiguration :
    IEntityTypeConfiguration<CheckoutRequestJournal>
{
    private static readonly ValueConverter<DateTimeOffset?, long?> NullableUtcConverter = new(
        value => value.HasValue ? value.Value.ToUniversalTime().ToUnixTimeMilliseconds() : null,
        value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);

    public void Configure(EntityTypeBuilder<CheckoutRequestJournal> builder)
    {
        builder.ToTable("CheckoutRequestJournals", table =>
        {
            table.HasCheckConstraint("CK_CheckoutRequestJournals_RequestFingerprint",
                "length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" NOT GLOB '*[^0-9A-F]*'");
            table.HasCheckConstraint("CK_CheckoutRequestJournals_QuoteFingerprint",
                "length(\"PreparedQuoteFingerprint\") = 64 AND \"PreparedQuoteFingerprint\" NOT GLOB '*[^0-9A-F]*'");
            table.HasCheckConstraint("CK_CheckoutRequestJournals_Json",
                "length(trim(\"CanonicalRequestJson\")) > 0 AND length(trim(\"PreparedQuoteJson\")) > 0");
            table.HasCheckConstraint("CK_CheckoutRequestJournals_Status", "\"Status\" IN (1, 2, 3)");
            table.HasCheckConstraint("CK_CheckoutRequestJournals_StateShape",
                "(\"Status\" = 1 AND \"OrderId\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"AcknowledgedAtUtc\" IS NULL AND \"AbandonedAtUtc\" IS NULL AND \"AbandonedByUserId\" IS NULL) OR " +
                "(\"Status\" = 2 AND \"OrderId\" IS NOT NULL AND \"CompletedAtUtc\" IS NOT NULL AND \"AbandonedAtUtc\" IS NULL AND \"AbandonedByUserId\" IS NULL) OR " +
                "(\"Status\" = 3 AND \"OrderId\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"AcknowledgedAtUtc\" IS NULL AND \"AbandonedAtUtc\" IS NOT NULL AND \"AbandonedByUserId\" IS NOT NULL)");
        });
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.ClientRequestId).IsRequired();
        builder.Property(x => x.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.CanonicalRequestJson).IsRequired();
        builder.Property(x => x.PreparedQuoteFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.PreparedQuoteJson).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.CompletedAtUtc).HasConversion(NullableUtcConverter);
        builder.Property(x => x.AcknowledgedAtUtc).HasConversion(NullableUtcConverter);
        builder.Property(x => x.AbandonedAtUtc).HasConversion(NullableUtcConverter);
        builder.HasIndex(x => x.ClientRequestId).IsUnique().HasDatabaseName("UX_CheckoutRequestJournals_ClientRequestId");
        builder.HasIndex(x => x.OrderId).IsUnique().HasFilter("\"OrderId\" IS NOT NULL")
            .HasDatabaseName("UX_CheckoutRequestJournals_OrderId");
        builder.HasIndex(x => new { x.Status, x.AcknowledgedAtUtc })
            .HasDatabaseName("IX_CheckoutRequestJournals_Status_AcknowledgedAtUtc");
        builder.HasIndex(x => new { x.PreparedByUserId, x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_CheckoutRequestJournals_User_Status_CreatedAtUtc");
        builder.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_CheckoutRequestJournals_CreatedAtUtc");
        builder.HasOne(x => x.PreparedByUser).WithMany().HasForeignKey(x => x.PreparedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AbandonedByUser).WithMany().HasForeignKey(x => x.AbandonedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
