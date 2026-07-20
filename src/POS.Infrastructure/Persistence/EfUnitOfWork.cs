using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Lưu toàn bộ thay đổi của một use case,
/// quản lý transaction và chuyển exception persistence
/// thành exception độc lập với provider.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private const int SqliteConstraintErrorCode = 19;

    private const int
        SqliteConstraintPrimaryKeyExtendedCode = 1555;

    private const int
        SqliteConstraintUniqueExtendedCode = 2067;

    private readonly PosDbContext _dbContext;

    public EfUnitOfWork(
        PosDbContext dbContext)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConflictException(
                PersistenceConflictKind.Concurrency,
                "Dữ liệu đã được một thao tác khác thay đổi.",
                target: null,
                exception);
        }
        catch (DbUpdateException exception)
        {
            if (!TryGetUniqueConstraintTarget(
                    exception,
                    out var target))
            {
                throw;
            }

            throw new PersistenceConflictException(
                PersistenceConflictKind.UniqueConstraint,
                "Dữ liệu bị trùng với một bản ghi đã tồn tại.",
                target,
                exception);
        }
    }

    public async Task<IApplicationTransaction>
        BeginTransactionAsync(
            CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "DbContext hiện đã có một transaction " +
                "đang hoạt động.");
        }

        var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        return new EfApplicationTransaction(
            transaction);
    }

    private static bool TryGetUniqueConstraintTarget(
        DbUpdateException exception,
        out string? target)
    {
        target = null;

        var sqliteException =
            FindSqliteException(exception);

        if (sqliteException is null)
        {
            return false;
        }

        if (sqliteException.SqliteErrorCode !=
            SqliteConstraintErrorCode)
        {
            return false;
        }

        if (sqliteException.SqliteExtendedErrorCode is not
            (
                SqliteConstraintUniqueExtendedCode or
                SqliteConstraintPrimaryKeyExtendedCode
            ))
        {
            return false;
        }

        var message =
            sqliteException.Message;

        if (message.Contains(
                "Products.Code",
                StringComparison.OrdinalIgnoreCase))
        {
            target =
                PersistenceConflictTargets.ProductCode;

            return true;
        }

        if (message.Contains(
                "Products.Barcode",
                StringComparison.OrdinalIgnoreCase))
        {
            target =
                PersistenceConflictTargets.ProductBarcode;

            return true;
        }

        if (message.Contains(
                "Categories.Name",
                StringComparison.OrdinalIgnoreCase))
        {
            target =
                PersistenceConflictTargets.CategoryName;

            return true;
        }

        /*
         * Vẫn xác định đây là unique constraint,
         * nhưng chưa biết chính xác property nghiệp vụ.
         */
        return true;
    }

    private static SqliteException? FindSqliteException(
        Exception exception)
    {
        Exception? currentException =
            exception;

        while (currentException is not null)
        {
            if (currentException is
                SqliteException sqliteException)
            {
                return sqliteException;
            }

            currentException =
                currentException.InnerException;
        }

        return null;
    }
}