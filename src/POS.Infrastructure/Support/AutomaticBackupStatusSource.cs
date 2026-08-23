using POS.Application.Abstractions.Services;

namespace POS.Infrastructure.Support;

public sealed class AutomaticBackupStatusSource : IAutomaticBackupStatusSource
{
    private AutomaticBackupStatusSnapshot _current = new(AutomaticBackupStatus.StateMissing);
    public AutomaticBackupStatusSnapshot Current => Volatile.Read(ref _current);
    public event EventHandler<AutomaticBackupStatusChangedEventArgs>? StatusChanged;

    public void Publish(AutomaticBackupStatusSnapshot status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Volatile.Write(ref _current, status);
        var handlers = StatusChanged;
        if (handlers is null) return;
        foreach (EventHandler<AutomaticBackupStatusChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try { handler(this, new AutomaticBackupStatusChangedEventArgs(status)); }
            catch { /* A stale UI subscriber must not stop backup processing. */ }
        }
    }
}
