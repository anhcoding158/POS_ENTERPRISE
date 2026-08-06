using Microsoft.EntityFrameworkCore.Storage;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Adapter giữa transaction của EF Core và
/// IApplicationTransaction của tầng Application.
/// </summary>
public sealed class EfApplicationTransaction :
    IApplicationTransaction
{
    private IDbContextTransaction? _transaction;
    private readonly SqliteFailureClassifier _failureClassifier;

    public EfApplicationTransaction(
        IDbContextTransaction transaction,
        SqliteFailureClassifier? failureClassifier = null)
    {
        _transaction =
            transaction ??
            throw new ArgumentNullException(
                nameof(transaction));

        _failureClassifier = failureClassifier ?? new SqliteFailureClassifier();
    }

    public bool IsCompleted { get; private set; }

    public async Task CommitAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction =
            GetActiveTransaction();

        try
        {
            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            _failureClassifier.Classify(exception) is not null)
        {
            throw _failureClassifier.Translate(exception);
        }

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
        ObjectDisposedException.ThrowIf(
            _transaction is null,
            nameof(EfApplicationTransaction));

        if (IsCompleted)
        {
            throw new InvalidOperationException(
                "Transaction đã được commit hoặc rollback.");
        }

        return _transaction;
    }
}
