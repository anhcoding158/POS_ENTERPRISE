using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping User sang bảng Users trong SQLite.
/// </summary>
public sealed class UserConfiguration :
    IEntityTypeConfiguration<User>
{
    private static readonly
        ValueConverter<
            DateTimeOffset?,
            long?>
        NullableDateTimeOffsetConverter =
            new(
                value =>
                    value.HasValue
                        ? value.Value
                            .ToUniversalTime()
                            .ToUnixTimeMilliseconds()
                        : null,

                value =>
                    value.HasValue
                        ? DateTimeOffset
                            .FromUnixTimeMilliseconds(
                                value.Value)
                        : null);

    public void Configure(
        EntityTypeBuilder<User> builder)
    {
        builder.ToTable(
            "Users",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Users_Username_Length",
                    $"length(\"Username\") >= " +
                    $"{BusinessRules.Users.UsernameMinLength} " +
                    $"AND length(\"Username\") <= " +
                    $"{BusinessRules.Users.UsernameMaxLength}");

                table.HasCheckConstraint(
                    "CK_Users_NormalizedUsername_Length",
                    $"length(\"NormalizedUsername\") >= " +
                    $"{BusinessRules.Users.UsernameMinLength} " +
                    $"AND length(\"NormalizedUsername\") <= " +
                    $"{BusinessRules.Users.UsernameMaxLength}");

                table.HasCheckConstraint(
                    "CK_Users_PasswordHash_Length",
                    $"length(\"PasswordHash\") >= 1 " +
                    $"AND length(\"PasswordHash\") <= " +
                    $"{BusinessRules.Users.PasswordHashMaxLength}");

                table.HasCheckConstraint(
                    "CK_Users_FullName_Length",
                    $"length(\"FullName\") >= 1 " +
                    $"AND length(\"FullName\") <= " +
                    $"{BusinessRules.Users.FullNameMaxLength}");

                table.HasCheckConstraint(
                    "CK_Users_Role_Valid",
                    "\"Role\" IN (1, 2, 3, 4)");

                table.HasCheckConstraint(
                    "CK_Users_FailedLoginAttempts_NonNegative",
                    "\"FailedLoginAttempts\" >= 0");
            });

        builder.ConfigureAuditableEntity();

        builder.Property(
                user =>
                    user.Username)
            .HasMaxLength(
                BusinessRules.Users
                    .UsernameMaxLength)
            .UseCollation("NOCASE")
            .IsRequired();

        builder.Property(
                user =>
                    user.NormalizedUsername)
            .HasMaxLength(
                BusinessRules.Users
                    .UsernameMaxLength)
            .UseCollation("NOCASE")
            .IsRequired();

        builder.Property(
                user =>
                    user.PasswordHash)
            .HasMaxLength(
                BusinessRules.Users
                    .PasswordHashMaxLength)
            .IsRequired();

        builder.Property(
                user =>
                    user.FullName)
            .HasMaxLength(
                BusinessRules.Users
                    .FullNameMaxLength)
            .UseCollation("NOCASE")
            .IsRequired();

        builder.Property(
                user =>
                    user.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(
                user =>
                    user.IsActive)
            .IsRequired();

        builder.Property(
                user =>
                    user.FailedLoginAttempts)
            .IsRequired();

        builder.Property(
                user =>
                    user.LockedUntilUtc)
            .HasConversion(
                NullableDateTimeOffsetConverter)
            .HasColumnType("INTEGER");

        builder.Property(
                user =>
                    user.LastLoginAtUtc)
            .HasConversion(
                NullableDateTimeOffsetConverter)
            .HasColumnType("INTEGER");

        /*
         * Username chuẩn hóa là nguồn sự thật
         * để chống trùng tên đăng nhập.
         */
        builder.HasIndex(
                user =>
                    user.NormalizedUsername)
            .IsUnique()
            .HasDatabaseName(
                "UX_Users_NormalizedUsername");

        builder.HasIndex(
                user =>
                    new
                    {
                        user.IsActive,
                        user.Role,
                        user.FullName
                    })
            .HasDatabaseName(
                "IX_Users_Active_Role_FullName");

        builder.HasIndex(
                user =>
                    user.LockedUntilUtc)
            .HasDatabaseName(
                "IX_Users_LockedUntilUtc");
    }
}