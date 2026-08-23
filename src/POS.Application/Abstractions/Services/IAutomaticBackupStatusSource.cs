namespace POS.Application.Abstractions.Services;

public interface IAutomaticBackupStatusSource
{
    AutomaticBackupStatusSnapshot Current { get; }
    event EventHandler<AutomaticBackupStatusChangedEventArgs>? StatusChanged;
    void Publish(AutomaticBackupStatusSnapshot status);
}
