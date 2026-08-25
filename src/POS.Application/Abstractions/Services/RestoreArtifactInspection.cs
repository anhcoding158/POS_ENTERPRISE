namespace POS.Application.Abstractions.Services;

public enum RestoreArtifactStatus
{
    Valid,
    ValidLegacyUnattested,
    Cancelled,
    InvalidPath,
    InvalidArtifact,
    SourceUnavailable,
    SourceLocked,
    SourceChangedDuringInspection,
    ActiveDatabaseConflict,
    ChecksumMismatch,
    IntegrityCheckFailed,
    MissingMigrationHistory,
    UnsupportedOlderSchema,
    UnsupportedNewerSchema,
    UnknownMigrationHistory,
    UnsafeReparsePath,
    NetworkPathUnsupported,
    UnexpectedFailure
}

public enum RestoreArtifactKind { Manual, Automatic, PreRestore, LegacyOrUnknown }
public enum RestoreArtifactProvenance { AutomaticStateAttested, LegacyUnattested }
public enum RestoreSchemaCompatibility { Current, OlderCompatible, UnsupportedOlder, UnsupportedNewer, Unknown }

public sealed record RestoreArtifactInspection(
    RestoreArtifactStatus Status,
    RestoreArtifactKind ArtifactKind,
    RestoreArtifactProvenance Provenance,
    RestoreSchemaCompatibility SchemaCompatibility,
    string SafeDisplayFileName,
    long? ByteLength,
    string? Sha256Hex,
    int AppliedMigrationCount,
    string? LatestAppliedMigrationId,
    string? ExpectedLatestCompiledMigrationId,
    string MessageKey)
{
    public bool IsRestorable => Status is RestoreArtifactStatus.Valid or
        RestoreArtifactStatus.ValidLegacyUnattested;
}
