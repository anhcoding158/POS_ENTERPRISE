namespace POS.Application.Abstractions.Services;

public enum DatabaseStorageSnapshotStatus
{
    Available,
    DatabaseNotFound,
    MetadataUnavailable
}

public enum StorageWarningState
{
    Healthy,
    Warning,
    Insufficient,
    Unavailable
}

public enum StorageUnavailableReason
{
    None,
    InvalidDatabasePath,
    DatabaseNotFound,
    ReparsePointRejected,
    FileMetadataUnavailable,
    DriveMetadataUnavailable,
    FootprintOverflow
}

public sealed record DatabaseStorageSnapshot(
    DatabaseStorageSnapshotStatus Status,
    StorageWarningState WarningState,
    string? VolumeRoot,
    long? TotalCapacityBytes,
    long? AvailableFreeBytes,
    long? MainDatabaseBytes,
    long? SidecarBytes,
    long? TotalStorageFootprintBytes,
    DateTimeOffset CapturedAtUtc,
    StorageUnavailableReason Reason);
