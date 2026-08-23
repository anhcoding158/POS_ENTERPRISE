namespace POS.Application.Abstractions.Services;

public interface IBackupCoordinator
{
    bool TryAcquire(out IBackupOperationLease? lease);
}

public interface IBackupOperationLease : IDisposable, IAsyncDisposable
{
}
