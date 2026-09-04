using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers", table =>
        {
            table.HasCheckConstraint("CK_Suppliers_Code_Length",
                $"length(\"Code\") >= {BusinessRules.Suppliers.CodeMinLength} AND length(\"Code\") <= {BusinessRules.Suppliers.CodeMaxLength}");
            table.HasCheckConstraint("CK_Suppliers_NormalizedCode_Length",
                $"length(\"NormalizedCode\") >= {BusinessRules.Suppliers.CodeMinLength} AND length(\"NormalizedCode\") <= {BusinessRules.Suppliers.CodeMaxLength}");
            table.HasCheckConstraint("CK_Suppliers_Name_Length",
                $"length(\"Name\") >= 1 AND length(\"Name\") <= {BusinessRules.Suppliers.NameMaxLength}");
        });

        builder.ConfigureAuditableEntity();
        builder.Property(supplier => supplier.Code)
            .HasMaxLength(BusinessRules.Suppliers.CodeMaxLength)
            .IsRequired();
        builder.Property(supplier => supplier.NormalizedCode)
            .HasMaxLength(BusinessRules.Suppliers.CodeMaxLength)
            .UseCollation("NOCASE")
            .IsRequired();
        builder.Property(supplier => supplier.Name)
            .HasMaxLength(BusinessRules.Suppliers.NameMaxLength)
            .IsRequired();
        builder.Property(supplier => supplier.TaxCode).HasMaxLength(BusinessRules.Suppliers.TaxCodeMaxLength);
        builder.Property(supplier => supplier.ContactName).HasMaxLength(BusinessRules.Suppliers.ContactNameMaxLength);
        builder.Property(supplier => supplier.PhoneNumber).HasMaxLength(BusinessRules.Suppliers.PhoneNumberMaxLength);
        builder.Property(supplier => supplier.EmailAddress).HasMaxLength(BusinessRules.Suppliers.EmailAddressMaxLength);
        builder.Property(supplier => supplier.Address).HasMaxLength(BusinessRules.Suppliers.AddressMaxLength);
        builder.Property(supplier => supplier.Notes).HasMaxLength(BusinessRules.Suppliers.NotesMaxLength);
        builder.Property(supplier => supplier.IsActive).IsRequired();
        builder.HasIndex(supplier => supplier.NormalizedCode)
            .IsUnique()
            .HasDatabaseName("UX_Suppliers_NormalizedCode");
        builder.HasIndex(supplier => new { supplier.IsActive, supplier.Name, supplier.Code })
            .HasDatabaseName("IX_Suppliers_Active_Name_Code");
    }
}
