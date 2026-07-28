using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class HeldSaleLineConfiguration : IEntityTypeConfiguration<HeldSaleLine>
{
    public void Configure(EntityTypeBuilder<HeldSaleLine> builder)
    {
        builder.ToTable("HeldSaleLines", table =>
        {
            table.HasCheckConstraint("CK_HeldSaleLines_Quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_HeldSaleLines_UnitPrice", "\"UnitPriceSnapshot\" >= 0");
            table.HasCheckConstraint("CK_HeldSaleLines_LineTotal",
                "\"LineTotalSnapshot\" >= 0 AND \"LineTotalSnapshot\" = \"UnitPriceSnapshot\" * \"Quantity\"");
            table.HasCheckConstraint("CK_HeldSaleLines_Code", "length(trim(\"ProductCodeSnapshot\")) > 0");
            table.HasCheckConstraint("CK_HeldSaleLines_Name", "length(trim(\"ProductNameSnapshot\")) > 0");
            table.HasCheckConstraint("CK_HeldSaleLines_SortOrder", "\"SortOrder\" >= 0");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedOnAdd();
        builder.Property(value => value.ProductCodeSnapshot)
            .HasMaxLength(BusinessRules.Products.CodeMaxLength).IsRequired();
        builder.Property(value => value.BarcodeSnapshot)
            .HasMaxLength(BusinessRules.Products.BarcodeMaxLength);
        builder.Property(value => value.ProductNameSnapshot)
            .HasMaxLength(BusinessRules.Products.NameMaxLength).IsRequired();
        builder.Property(value => value.UnitPriceSnapshot).HasColumnType("INTEGER").IsRequired();
        builder.Property(value => value.LineTotalSnapshot).HasColumnType("INTEGER").IsRequired();
        builder.Property(value => value.LineNotesSnapshot)
            .HasMaxLength(BusinessRules.Orders.NotesMaxLength);
        builder.HasIndex(value => new { value.HeldSaleId, value.SortOrder }).IsUnique()
            .HasDatabaseName("UX_HeldSaleLines_HeldSale_SortOrder");
        builder.HasOne(value => value.Product).WithMany()
            .HasForeignKey(value => value.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
