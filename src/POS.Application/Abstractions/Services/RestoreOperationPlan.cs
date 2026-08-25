namespace POS.Application.Abstractions.Services;

public sealed record RestoreOperationPlan
{
    public required int FormatVersion { get; init; }
    public required Guid OperationId { get; init; }
    public required string OperationTokenSha256Hex { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required DateTimeOffset LastUpdatedUtc { get; init; }
    public required RestoreOperationState State { get; init; }
    public required int ParentProcessId { get; init; }
    public required long ParentProcessStartTimeUtcTicks { get; init; }
    public string? ExpectedExecutablePath { get; init; }
    public required string ActiveDatabasePath { get; init; }
    public required long OriginalDatabaseByteLength { get; init; }
    public required string OriginalDatabaseSha256Hex { get; init; }
    public required string StagedCandidatePath { get; init; }
    public required long CandidateByteLength { get; init; }
    public required string CandidateSha256Hex { get; init; }
    public required RestoreSchemaCompatibility CandidateSchemaCompatibility { get; init; }
    public required int CandidateAppliedMigrationCount { get; init; }
    public string? CandidateLatestMigrationId { get; init; }
    public required string SafetyBackupPath { get; init; }
    public required long SafetyBackupByteLength { get; init; }
    public required string SafetyBackupSha256Hex { get; init; }
    public required string RollbackPath { get; init; }
    public required string FailedCandidatePath { get; init; }
    public required string OperationMarkerPath { get; init; }
    public required string ResultMarkerPath { get; init; }
    public string? FailureCode { get; init; }
}
