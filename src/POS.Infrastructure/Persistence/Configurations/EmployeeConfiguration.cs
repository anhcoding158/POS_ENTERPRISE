using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", table =>
        {
            table.HasCheckConstraint("CK_Employees_Code_Length",
                $"length(\"EmployeeCode\") >= {BusinessRules.Employees.EmployeeCodeMinLength} AND length(\"EmployeeCode\") <= {BusinessRules.Employees.EmployeeCodeMaxLength}");
            table.HasCheckConstraint("CK_Employees_NormalizedCode_Length",
                $"length(\"NormalizedEmployeeCode\") >= {BusinessRules.Employees.EmployeeCodeMinLength} AND length(\"NormalizedEmployeeCode\") <= {BusinessRules.Employees.EmployeeCodeMaxLength}");
            table.HasCheckConstraint("CK_Employees_FullName_Length",
                $"length(\"FullName\") >= 1 AND length(\"FullName\") <= {BusinessRules.Employees.FullNameMaxLength}");
        });

        builder.ConfigureAuditableEntity();
        builder.Property(employee => employee.EmployeeCode).HasMaxLength(BusinessRules.Employees.EmployeeCodeMaxLength).UseCollation("NOCASE").IsRequired();
        builder.Property(employee => employee.NormalizedEmployeeCode).HasMaxLength(BusinessRules.Employees.EmployeeCodeMaxLength).UseCollation("NOCASE").IsRequired();
        builder.Property(employee => employee.FullName).HasMaxLength(BusinessRules.Employees.FullNameMaxLength).UseCollation("NOCASE").IsRequired();
        builder.Property(employee => employee.PhoneNumber).HasMaxLength(BusinessRules.Employees.PhoneNumberMaxLength).UseCollation("NOCASE");
        builder.Property(employee => employee.EmailAddress).HasMaxLength(BusinessRules.Employees.EmailAddressMaxLength).UseCollation("NOCASE");
        builder.Property(employee => employee.IsActive).IsRequired();
        builder.HasIndex(employee => employee.NormalizedEmployeeCode).IsUnique().HasDatabaseName("UX_Employees_NormalizedEmployeeCode");
        builder.HasIndex(employee => new { employee.IsActive, employee.FullName }).HasDatabaseName("IX_Employees_Active_FullName");

        builder.HasOne(employee => employee.LoginAccount)
            .WithOne(user => user.Employee)
            .HasForeignKey<Employee>(employee => employee.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(employee => employee.UserId).IsUnique().HasDatabaseName("UX_Employees_UserId");
    }
}
