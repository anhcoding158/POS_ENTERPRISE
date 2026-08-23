using POS.Application.Abstractions.Services;

namespace POS.Infrastructure.Support;

public sealed class BackupCoordinator : IBackupCoordinator, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public bool TryAcquire(out IBackupOperationLease? lease)
    {
        lease = null;
        if (_disposed || !_gate.Wait(0)) return false;
        lease = new Lease(_gate);
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        _gate.Dispose();
    }

    private sealed class Lease(SemaphoreSlim gate) : IBackupOperationLease
    {
        private SemaphoreSlim? _gate = gate;

        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
