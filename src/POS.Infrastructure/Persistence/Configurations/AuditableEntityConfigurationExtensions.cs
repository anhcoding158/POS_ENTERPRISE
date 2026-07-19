using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Common;

namespace POS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình EF Core dùng chung cho các entity
/// kế thừa AuditableEntity.
/// </summary>
internal static class AuditableEntityConfigurationExtensions
{
    /*
     * SQLite không hỗ trợ tự nhiên đầy đủ DateTimeOffset.
     *
     * Ta lưu thời gian UTC thành Unix milliseconds dạng INTEGER
     * để có thể lọc, sắp xếp và tạo index chính xác.
     */
    private static readonly ValueConverter<DateTimeOffset, long>
        DateTimeOffsetToUnixMillisecondsConverter =
            new(
                value =>
                    value
                        .ToUniversalTime()
                        .ToUnixTimeMilliseconds(),

                value =>
                    DateTimeOffset
                        .FromUnixTimeMilliseconds(value));

    public static void ConfigureAuditableEntity<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id)
            .ValueGeneratedOnAdd();

        builder.Property(entity => entity.CreatedAtUtc)
            .HasConversion(
                DateTimeOffsetToUnixMillisecondsConverter)
            .HasColumnType("INTEGER")
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc)
            .HasConversion(
                DateTimeOffsetToUnixMillisecondsConverter)
            .HasColumnType("INTEGER")
            .IsRequired()
            .IsConcurrencyToken();
    }
}