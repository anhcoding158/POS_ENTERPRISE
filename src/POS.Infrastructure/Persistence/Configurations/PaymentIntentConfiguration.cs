using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    private static readonly ValueConverter<DateTimeOffset?, long?> NullableUtcConverter = new(
        value => value.HasValue ? value.Value.ToUniversalTime().ToUnixTimeMilliseconds() : null,
        value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);

    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        builder.ToTable("PaymentIntents", table =>
        {
            table.HasCheckConstraint("CK_PaymentIntents_Provider", "\"Provider\" = 1");
            table.HasCheckConstraint("CK_PaymentIntents_Status", "\"Status\" IN (1,2,3,4,5,6)");
            table.HasCheckConstraint("CK_PaymentIntents_Amount", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_PaymentIntents_Currency", "\"Currency\" = 'VND'");
            table.HasCheckConstraint("CK_PaymentIntents_PayloadHash",
                "length(\"PayloadHash\") = 64 AND \"PayloadHash\" NOT GLOB '*[^0-9A-F]*'");
            table.HasCheckConstraint("CK_PaymentIntents_QuoteFingerprint",
                "length(\"QuoteFingerprint\") = 64 AND \"QuoteFingerprint\" NOT GLOB '*[^0-9A-F]*'");
            table.HasCheckConstraint("CK_PaymentIntents_StateShape",
                "(\"Status\" = 1 AND \"PresentedAtUtc\" IS NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR " +
                "(\"Status\" = 2 AND \"PresentedAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR " +
                "(\"Status\" = 3 AND \"PresentedAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR " +
                "(\"Status\" = 4 AND \"ConfirmedAtUtc\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"CompletedAtUtc\" IS NOT NULL AND \"CompletedOrderId\" IS NOT NULL AND \"CancelledAtUtc\" IS NULL) OR " +
                "(\"Status\" = 5 AND \"CancelledAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR " +
                "(\"Status\" = 6 AND \"ExpiredAtUtc\" IS NOT NULL AND \"ExpirationReason\" IS NOT NULL AND \"CompletedAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL AND \"CancelledAtUtc\" IS NULL)");
        });
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.DisplayCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Provider).HasConversion<int>().IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.TransferContent).HasMaxLength(99).IsRequired();
        builder.Property(x => x.PayloadText).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.PayloadHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.BankCodeSnapshot).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AccountNumberSnapshot).HasMaxLength(32).IsRequired();
        builder.Property(x => x.AccountNameSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(x => x.QuoteFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.CheckoutRequestJson).HasMaxLength(16_384).IsRequired();
        builder.Property(x => x.ExpirationReason).HasMaxLength(200);
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken().IsRequired();
        builder.Property(x => x.PresentedAtUtc).HasConversion(NullableUtcConverter);
        builder.Property(x => x.ConfirmedAtUtc).HasConversion(NullableUtcConverter);
        builder.Property(x => x.CompletedAtUtc).HasConversion(NullableUtcConverter);
        builder.Property(x => x.CancelledAtUtc).HasConversion(NullableUtcConverter);
        builder.Property(x => x.ExpiredAtUtc).HasConversion(NullableUtcConverter);
        builder.Property(x => x.ExpiresAtUtc).HasConversion(NullableUtcConverter);
        builder.HasIndex(x => x.ClientRequestId).IsUnique();
        builder.HasIndex(x => x.DisplayCode).IsUnique();
        builder.HasIndex(x => x.CompletedOrderId).IsUnique().HasFilter("\"CompletedOrderId\" IS NOT NULL");
        builder.HasIndex(x => new { x.Status, x.UpdatedAtUtc });
        builder.HasIndex(x => new { x.CreatedByUserId, x.Status, x.UpdatedAtUtc });
        builder.HasIndex(x => x.PayloadHash);
        builder.HasIndex(x => x.QuoteFingerprint);
        builder.HasIndex(x => x.HeldSaleId)
            .IsUnique()
            .HasFilter("\"HeldSaleId\" IS NOT NULL AND \"Status\" IN (1,2,3)")
            .HasDatabaseName("UX_PaymentIntents_Active_HeldSaleOwner");
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ConfirmedByUser).WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CompletedOrder).WithMany().HasForeignKey(x => x.CompletedOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.HeldSale).WithMany().HasForeignKey(x => x.HeldSaleId).OnDelete(DeleteBehavior.Restrict);
    }
}
