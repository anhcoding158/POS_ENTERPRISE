using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Lưu toàn bộ thay đổi của một use case
/// và tạo transaction EF Core.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly PosDbContext _dbContext;

    public EfUnitOfWork(PosDbContext dbContext)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<IApplicationTransaction>
        BeginTransactionAsync(
            CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "DbContext hiện đã có một transaction đang hoạt động.");
        }

        var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        return new EfApplicationTransaction(
            transaction);
    }
}