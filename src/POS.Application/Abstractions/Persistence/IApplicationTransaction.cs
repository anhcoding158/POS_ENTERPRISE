namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Transaction độc lập với EF Core.
///
/// Application không được tham chiếu
/// IDbContextTransaction của Infrastructure.
/// </summary>
public interface IApplicationTransaction : IAsyncDisposable
{
    bool IsCompleted { get; }

    Task CommitAsync(
        CancellationToken cancellationToken = default);

    Task RollbackAsync(
        CancellationToken cancellationToken = default);
}