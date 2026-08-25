namespace POS.Application.Abstractions.Services;

public enum RestoreExecutionStatus
{
    Success,
    Cancelled,
    InvalidPlan,
    InvalidOperationToken,
    ArtifactValidationFailed,
    DatabaseBusy,
    PreRestoreBackupFailed,
    StagingFailed,
    SourceChanged,
    ParentProcessMismatch,
    ParentExitTimeout,
    UnsafeDatabasePath,
    UnsafeSidecar,
    ReplacementFailed,
    PostRestoreVerificationFailed,
    RollbackSucceeded,
    RollbackFailed,
    RecoveryRequired,
    RestartRequired,
    UnexpectedFailure
}

public enum RestoreOperationState
{
    Prepared,
    WaitingForParentExit,
    ReplacementStarted,
    CandidateInstalled,
    Verified,
    RollbackStarted,
    RolledBack,
    RollbackFailed
}

public sealed record RestoreExecutionResult(
    RestoreExecutionStatus Status,
    string MessageKey,
    Guid OperationId,
    RestoreOperationState? OperationState,
    long? CandidateByteLength = null,
    string? CandidateSha256Hex = null,
    long? SafetyBackupByteLength = null,
    string? SafetyBackupSha256Hex = null,
    bool RollbackAttempted = false,
    bool RollbackCompleted = false,
    bool RestartRequired = false);

public sealed record RestorePreparationRequest(
    string SelectedArtifactPath,
    int ParentProcessId,
    DateTimeOffset ParentProcessStartTimeUtc);

public sealed record RestorePreparationResult(
    RestoreExecutionStatus Status,
    string MessageKey,
    Guid OperationId,
    RestoreOperationState? OperationState,
    string? OpaquePlanPath,
    string? OneTimeOperationToken,
    string? SafetyBackupIdentifier,
    long? SafetyBackupByteLength,
    string? SafetyBackupSha256Hex)
{
    public bool IsPrepared => Status == RestoreExecutionStatus.Success &&
        OperationState == RestoreOperationState.Prepared &&
        OperationId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(OpaquePlanPath) &&
        !string.IsNullOrWhiteSpace(OneTimeOperationToken);
}
