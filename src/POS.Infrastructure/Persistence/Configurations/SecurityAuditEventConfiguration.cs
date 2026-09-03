using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

public sealed class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("SecurityAuditEvents", table =>
        {
            table.HasCheckConstraint("CK_SecurityAuditEvents_Action_Valid", "\"Action\" >= 1 AND \"Action\" <= 16");
            table.HasCheckConstraint("CK_SecurityAuditEvents_Result_Length", "length(\"Result\") >= 1 AND length(\"Result\") <= 100");
        });

        builder.ConfigureAuditableEntity();
        builder.Property(audit => audit.ActorUserId).HasColumnType("INTEGER");
        builder.Property(audit => audit.TargetEmployeeId).HasColumnType("INTEGER");
        builder.Property(audit => audit.TargetUserId).HasColumnType("INTEGER");
        builder.Property(audit => audit.Action).HasConversion<int>().IsRequired();
        builder.Property(audit => audit.Result).HasMaxLength(100).IsRequired();
        builder.Property(audit => audit.OperationId).HasConversion<string>().HasMaxLength(36).IsRequired();
        builder.Property(audit => audit.ActorDisplayNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(audit => audit.TargetDisplayNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(audit => audit.BusinessArea).HasMaxLength(120).IsRequired();
        builder.Property(audit => audit.TargetType).HasMaxLength(80).IsRequired();
        builder.Property(audit => audit.TerminalId).HasMaxLength(80).IsRequired();
        builder.Property(audit => audit.BeforeValuesJson).HasMaxLength(64000).IsRequired();
        builder.Property(audit => audit.AfterValuesJson).HasMaxLength(64000).IsRequired();
        builder.HasIndex(audit => new { audit.TargetEmployeeId, audit.CreatedAtUtc }).HasDatabaseName("IX_SecurityAuditEvents_TargetEmployee_Created");
        builder.HasIndex(audit => new { audit.TargetUserId, audit.CreatedAtUtc }).HasDatabaseName("IX_SecurityAuditEvents_TargetUser_Created");
        builder.HasIndex(audit => new { audit.CreatedAtUtc, audit.Id }).HasDatabaseName("IX_SecurityAuditEvents_Created_Id");
        builder.HasIndex(audit => new { audit.BusinessArea, audit.Action, audit.CreatedAtUtc }).HasDatabaseName("IX_SecurityAuditEvents_Area_Action_Created");
        builder.HasOne<User>().WithMany().HasForeignKey(audit => audit.ActorUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany().HasForeignKey(audit => audit.TargetUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Employee>().WithMany().HasForeignKey(audit => audit.TargetEmployeeId).OnDelete(DeleteBehavior.SetNull);
    }
}
