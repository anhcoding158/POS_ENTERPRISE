namespace POS.Application.Abstractions.Services;

public enum AutomaticBackupStatus
{
    NotDue,
    Running,
    Success,
    DeferredBusy,
    Failed,
    SuccessWithRetentionWarning,
    StateMissing,
    StateCorrupt,
    StateRecovered,
    Cancelled
}

public sealed record AutomaticBackupResult(
    AutomaticBackupStatus Status,
    DateTimeOffset AttemptedAtUtc,
    DateTimeOffset? CompletedAtUtc = null,
    string? ArtifactIdentifier = null,
    long? ByteLength = null,
    string? Sha256 = null,
    string? Warning = null)
{
    public bool IsVerifiedSuccess => Status is AutomaticBackupStatus.Success or
        AutomaticBackupStatus.SuccessWithRetentionWarning;
}

public sealed record AutomaticBackupStatusSnapshot(
    AutomaticBackupStatus Status,
    DateTimeOffset? LastVerifiedSuccessUtc = null,
    string? ArtifactIdentifier = null,
    string? Warning = null);

public sealed class AutomaticBackupStatusChangedEventArgs(AutomaticBackupStatusSnapshot status) : EventArgs
{
    public AutomaticBackupStatusSnapshot Status { get; } = status;
}
