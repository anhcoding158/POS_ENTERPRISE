using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using POS.Domain.Common;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình EF Core dùng chung cho các entity
/// kế thừa AuditableEntity.
/// </summary>
internal static class AuditableEntityConfigurationExtensions
{
    /*
     * SQLite không hỗ trợ đầy đủ việc so sánh và sắp xếp
     * DateTimeOffset.
     *
     * Thời gian UTC được lưu thành Unix milliseconds INTEGER.
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

        builder.HasKey(
            entity =>
                entity.Id);

        builder.Property(
                entity =>
                    entity.Id)
            .ValueGeneratedOnAdd();

        builder.Property(
                entity =>
                    entity.CreatedAtUtc)
            .HasConversion(
                DateTimeOffsetToUnixMillisecondsConverter)
            .HasColumnType("INTEGER")
            .IsRequired();

        /*
         * UpdatedAtUtc chỉ còn là dữ liệu audit.
         * Không dùng timestamp millisecond làm concurrency token nữa.
         */
        builder.Property(
                entity =>
                    entity.UpdatedAtUtc)
            .HasConversion(
                DateTimeOffsetToUnixMillisecondsConverter)
            .HasColumnType("INTEGER")
            .IsRequired();

        /*
         * GUID do AuditableEntityInterceptor sinh ra.
         *
         * Đây là shadow property:
         * tồn tại trong EF model/database nhưng không nằm
         * trong Domain entity.
         */
        builder.Property<Guid>(
                AuditableEntityInterceptor
                    .ConcurrencyTokenPropertyName)
            .HasColumnType("TEXT")
            .IsRequired()
            .ValueGeneratedNever()
            .IsConcurrencyToken();
    }
}