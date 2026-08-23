namespace POS.Application.Abstractions.Services;

public sealed record AutomaticBackupState
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;
    public DateTimeOffset? LastVerifiedSuccessUtc { get; init; }
    public string? LastVerifiedArtifact { get; init; }
    public long? LastVerifiedByteLength { get; init; }
    public string? LastVerifiedSha256 { get; init; }
    public DateTimeOffset? LastAttemptUtc { get; init; }
    public AutomaticBackupStatus LastResult { get; init; } = AutomaticBackupStatus.StateMissing;
    public DateTimeOffset? NextAttemptUtc { get; init; }
    public string? LastRetentionWarning { get; init; }
}

public enum AutomaticBackupStateReadStatus { Missing, Valid, Corrupt, UnsupportedVersion }

public sealed record AutomaticBackupStateReadResult(
    AutomaticBackupStateReadStatus Status,
    AutomaticBackupState? State);
