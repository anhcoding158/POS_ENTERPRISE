using Microsoft.EntityFrameworkCore.Storage;
using POS.Application.Abstractions.Persistence;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Adapter giữa transaction của EF Core và
/// IApplicationTransaction của tầng Application.
/// </summary>
public sealed class EfApplicationTransaction :
    IApplicationTransaction
{
    private IDbContextTransaction? _transaction;

    public EfApplicationTransaction(
        IDbContextTransaction transaction)
    {
        _transaction =
            transaction ??
            throw new ArgumentNullException(
                nameof(transaction));
    }

    public bool IsCompleted { get; private set; }

    public async Task CommitAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction =
            GetActiveTransaction();

        await transaction.CommitAsync(
            cancellationToken);

        IsCompleted = true;
    }

    public async Task RollbackAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction =
            GetActiveTransaction();

        await transaction.RollbackAsync(
            cancellationToken);

        IsCompleted = true;
    }

    public async ValueTask DisposeAsync()
    {
        var transaction = _transaction;

        if (transaction is null)
        {
            GC.SuppressFinalize(this);
            return;
        }

        _transaction = null;

        try
        {
            /*
             * Nếu use case thoát ra mà chưa commit,
             * transaction được rollback để không lưu dữ liệu dở dang.
             */
            if (!IsCompleted)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);
            }
        }
        finally
        {
            IsCompleted = true;

            await transaction.DisposeAsync();

            GC.SuppressFinalize(this);
        }
    }

    private IDbContextTransaction
        GetActiveTransaction()
    {
        if (_transaction is null)
        {
            throw new ObjectDisposedException(
                nameof(EfApplicationTransaction));
        }

        if (IsCompleted)
        {
            throw new InvalidOperationException(
                "Transaction đã được commit hoặc rollback.");
        }

        return _transaction;
    }
}