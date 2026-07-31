using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class PaymentIntentManualResolutionConfiguration :
    IEntityTypeConfiguration<PaymentIntentManualResolution>
{
    public void Configure(EntityTypeBuilder<PaymentIntentManualResolution> builder)
    {
        var utcConverter = new ValueConverter<DateTimeOffset, long>(
            value => value.ToUniversalTime().ToUnixTimeMilliseconds(),
            value => DateTimeOffset.FromUnixTimeMilliseconds(value));
        builder.ToTable("PaymentIntentManualResolutions", table =>
        {
            table.HasCheckConstraint("CK_PaymentIntentManualResolutions_Type", "\"ResolutionType\" IN (1,2,3)");
            table.HasCheckConstraint("CK_PaymentIntentManualResolutions_Shape",
                "(\"ResolutionType\" = 1 AND \"LinkedOrderId\" IS NOT NULL) OR " +
                "(\"ResolutionType\" = 2 AND \"LinkedOrderId\" IS NULL) OR " +
                "(\"ResolutionType\" = 3 AND \"LinkedOrderId\" IS NULL AND length(trim(\"ExternalReference\")) > 0)");
        });
        builder.Property(x => x.ResolutionType).HasConversion<int>();
        builder.Property(x => x.ResolvedAtUtc).HasConversion(utcConverter);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ExternalReference).HasMaxLength(200);
        builder.HasIndex(x => x.PaymentIntentId).IsUnique();
        builder.HasIndex(x => x.LinkedOrderId).IsUnique().HasFilter("\"LinkedOrderId\" IS NOT NULL");
        builder.HasOne(x => x.PaymentIntent).WithMany().HasForeignKey(x => x.PaymentIntentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResolvedByUser).WithMany().HasForeignKey(x => x.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LinkedOrder).WithMany().HasForeignKey(x => x.LinkedOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
