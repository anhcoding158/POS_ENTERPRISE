namespace POS.Application.Abstractions.Services;

public interface IDatabaseStorageMonitor
{
    Task<DatabaseStorageSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);

    StoragePreflightResult EvaluatePreflight(
        DatabaseStorageSnapshot snapshot,
        StoragePreflightRequest request);

    long EstimatePreMigrationBackupBytes(long sqliteStorageFootprintBytes);
}
