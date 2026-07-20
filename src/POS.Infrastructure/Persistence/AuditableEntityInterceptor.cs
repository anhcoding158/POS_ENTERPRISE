using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POS.Domain.Common;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Làm mới concurrency token trước mỗi lần lưu
/// AuditableEntity.
///
/// Token là shadow property của EF Core nên Domain
/// không phụ thuộc persistence technology.
/// </summary>
public sealed class AuditableEntityInterceptor :
    SaveChangesInterceptor
{
    public const string ConcurrencyTokenPropertyName =
        "ConcurrencyToken";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        RefreshConcurrencyTokens(
            eventData.Context);

        return base.SavingChanges(
            eventData,
            result);
    }

    public override ValueTask<InterceptionResult<int>>
        SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
    {
        RefreshConcurrencyTokens(
            eventData.Context);

        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    private static void RefreshConcurrencyTokens(
        DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        var entries =
            dbContext.ChangeTracker
                .Entries<AuditableEntity>()
                .Where(
                    entry =>
                        entry.State is
                            EntityState.Added or
                            EntityState.Modified);

        foreach (var entry in entries)
        {
            /*
             * OriginalValue vẫn giữ token đọc từ database.
             * CurrentValue được đổi thành GUID mới.
             *
             * EF sẽ phát sinh dạng:
             *
             * UPDATE ...
             * SET ConcurrencyToken = newToken
             * WHERE Id = id
             *   AND ConcurrencyToken = originalToken
             */
            entry.Property<Guid>(
                    ConcurrencyTokenPropertyName)
                .CurrentValue =
                    Guid.NewGuid();
        }
    }
}