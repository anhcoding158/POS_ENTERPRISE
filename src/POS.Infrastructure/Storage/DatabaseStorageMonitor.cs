using System.IO;
using System.Security;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Services;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Storage;

internal sealed class DatabaseStorageMonitor : IDatabaseStorageMonitor
{
    private static readonly string[] SidecarSuffixes = ["-wal", "-shm", "-journal"];
    private readonly InfrastructureOptions _infrastructure;
    private readonly DatabaseStorageOptions _options;
    private readonly IStorageMetadataProvider _metadata;
    private readonly TimeProvider _timeProvider;

    public DatabaseStorageMonitor(
        IOptions<InfrastructureOptions> infrastructure,
        IOptions<DatabaseStorageOptions> options,
        IStorageMetadataProvider metadata,
        TimeProvider timeProvider)
        : this(infrastructure.Value, options.Value, metadata, timeProvider)
    {
    }

    internal DatabaseStorageMonitor(
        InfrastructureOptions infrastructure,
        DatabaseStorageOptions options,
        IStorageMetadataProvider metadata,
        TimeProvider timeProvider)
    {
        _infrastructure = infrastructure ?? throw new ArgumentNullException(nameof(infrastructure));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options.Validate();
    }

    public Task<DatabaseStorageSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var capturedAt = _timeProvider.GetUtcNow();

        string databasePath;
        try
        {
            databasePath = DatabasePathResolver
                .ResolveDatabasePathWithoutCreatingDirectory(_infrastructure.DatabasePath);
        }
        catch (Exception exception) when (IsMetadataException(exception))
        {
            return Task.FromResult(Unavailable(
                capturedAt, StorageUnavailableReason.InvalidDatabasePath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        StorageVolumeMetadata? volume = null;
        try
        {
            volume = _metadata.GetVolumeMetadata(databasePath);
        }
        catch (Exception exception) when (IsMetadataException(exception))
        {
            // File metadata may still provide a useful database-size snapshot.
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (HasReparsePointInExistingParents(databasePath))
            {
                return Task.FromResult(Unavailable(
                    capturedAt,
                    StorageUnavailableReason.ReparsePointRejected,
                    volume));
            }

            var main = ReadStableMetadata(databasePath, cancellationToken);
            if (!main.Exists)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HasReparsePointInExistingParents(databasePath))
                {
                    return Task.FromResult(Unavailable(
                        capturedAt,
                        StorageUnavailableReason.ReparsePointRejected,
                        volume));
                }

                return Task.FromResult(new DatabaseStorageSnapshot(
                    DatabaseStorageSnapshotStatus.DatabaseNotFound,
                    Classify(volume?.AvailableFreeBytes, volume?.TotalCapacityBytes),
                    volume?.VolumeRoot,
                    volume?.TotalCapacityBytes,
                    volume?.AvailableFreeBytes,
                    null,
                    null,
                    null,
                    capturedAt,
                    StorageUnavailableReason.DatabaseNotFound));
            }

            if (main.IsDirectory || main.IsReparsePoint || main.Length is null)
            {
                return Task.FromResult(Unavailable(
                    capturedAt,
                    main.IsReparsePoint
                        ? StorageUnavailableReason.ReparsePointRejected
                        : StorageUnavailableReason.FileMetadataUnavailable,
                    volume));
            }

            long sidecarBytes = 0;
            foreach (var suffix in SidecarSuffixes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sidecar = ReadStableMetadata(databasePath + suffix, cancellationToken);
                if (!sidecar.Exists)
                {
                    continue;
                }

                if (sidecar.IsDirectory || sidecar.IsReparsePoint || sidecar.Length is null)
                {
                    return Task.FromResult(Unavailable(
                        capturedAt,
                        sidecar.IsReparsePoint
                            ? StorageUnavailableReason.ReparsePointRejected
                            : StorageUnavailableReason.FileMetadataUnavailable,
                        volume));
                }

                sidecarBytes = checked(sidecarBytes + sidecar.Length.Value);
            }

            var footprint = checked(main.Length.Value + sidecarBytes);
            cancellationToken.ThrowIfCancellationRequested();
            if (HasReparsePointInExistingParents(databasePath))
            {
                return Task.FromResult(Unavailable(
                    capturedAt,
                    StorageUnavailableReason.ReparsePointRejected,
                    volume));
            }

            var warning = Classify(
                volume?.AvailableFreeBytes,
                volume?.TotalCapacityBytes);
            var status = volume?.AvailableFreeBytes is null
                ? DatabaseStorageSnapshotStatus.MetadataUnavailable
                : DatabaseStorageSnapshotStatus.Available;
            var reason = volume?.AvailableFreeBytes is null
                ? StorageUnavailableReason.DriveMetadataUnavailable
                : StorageUnavailableReason.None;

            return Task.FromResult(new DatabaseStorageSnapshot(
                status,
                warning,
                volume?.VolumeRoot,
                volume?.TotalCapacityBytes,
                volume?.AvailableFreeBytes,
                main.Length,
                sidecarBytes,
                footprint,
                capturedAt,
                reason));
        }
        catch (OverflowException)
        {
            return Task.FromResult(Unavailable(
                capturedAt, StorageUnavailableReason.FootprintOverflow, volume));
        }
        catch (Exception exception) when (IsMetadataException(exception))
        {
            return Task.FromResult(Unavailable(
                capturedAt, StorageUnavailableReason.FileMetadataUnavailable, volume));
        }
    }

    public StoragePreflightResult EvaluatePreflight(
        DatabaseStorageSnapshot snapshot,
        StoragePreflightRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);

        if (snapshot.Reason == StorageUnavailableReason.FootprintOverflow)
        {
            return Result(StoragePreflightStatus.Insufficient, request, null,
                snapshot.AvailableFreeBytes);
        }

        if (snapshot.AvailableFreeBytes is null)
        {
            return Result(StoragePreflightStatus.MetricsUnavailable, request, null, null);
        }

        var additionOverflowed = !TryAdd(
            request.RequiredAdditionalBytes,
            _options.ReservedHeadroomBytes,
            out var requiredFree);
        if (additionOverflowed || snapshot.AvailableFreeBytes.Value < requiredFree)
        {
            return Result(StoragePreflightStatus.Insufficient, request,
                requiredFree, snapshot.AvailableFreeBytes);
        }

        var projectedFree = snapshot.AvailableFreeBytes.Value - request.RequiredAdditionalBytes;
        var projectedState = Classify(projectedFree, snapshot.TotalCapacityBytes);
        var status = projectedState == StorageWarningState.Healthy
            ? StoragePreflightStatus.Allowed
            : StoragePreflightStatus.AllowedWithWarning;
        return Result(status, request, requiredFree, snapshot.AvailableFreeBytes);
    }

    public long EstimatePreMigrationBackupBytes(long sqliteStorageFootprintBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sqliteStorageFootprintBytes);
        var percentagePadding = decimal.Ceiling(
            sqliteStorageFootprintBytes *
            _options.BackupEstimatePaddingPercentage / 100m);
        var padding = Math.Max(
            _options.BackupEstimateMinimumPaddingBytes,
            percentagePadding >= long.MaxValue ? long.MaxValue : (long)percentagePadding);
        return SaturatingAdd(sqliteStorageFootprintBytes, padding);
    }

    private StorageWarningState Classify(long? available, long? total)
    {
        if (available is null)
        {
            return StorageWarningState.Unavailable;
        }

        if (available.Value < _options.ReservedHeadroomBytes)
        {
            return StorageWarningState.Insufficient;
        }

        var percentageWarning = total is > 0 &&
            (decimal)available.Value * 100m / total.Value <=
            _options.WarningFreePercentage;
        return available.Value <= _options.WarningFreeBytes || percentageWarning
            ? StorageWarningState.Warning
            : StorageWarningState.Healthy;
    }

    private bool HasReparsePointInExistingParents(string databasePath)
    {
        var parent = Path.GetDirectoryName(databasePath);
        var root = Path.GetPathRoot(databasePath);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(root))
        {
            throw new IOException("Database parent is unavailable.");
        }

        var relative = Path.GetRelativePath(root, parent);
        var current = Path.GetFullPath(root);
        if (!string.Equals(relative, ".", StringComparison.Ordinal))
        {
            foreach (var component in relative.Split(
                         [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, component);
                var metadata = _metadata.GetPathMetadata(current);
                if (!metadata.Exists)
                {
                    break;
                }

                if (!metadata.IsDirectory || metadata.IsReparsePoint)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private StoragePathMetadata ReadStableMetadata(
        string path,
        CancellationToken cancellationToken)
    {
        var first = _metadata.GetPathMetadata(path);
        cancellationToken.ThrowIfCancellationRequested();
        var second = _metadata.GetPathMetadata(path);
        if (first != second)
        {
            throw new IOException("Storage metadata changed during the bounded snapshot.");
        }

        return first;
    }

    private DatabaseStorageSnapshot Unavailable(
        DateTimeOffset capturedAt,
        StorageUnavailableReason reason,
        StorageVolumeMetadata? volume = null) =>
        new(
            DatabaseStorageSnapshotStatus.MetadataUnavailable,
            volume?.AvailableFreeBytes is null
                ? StorageWarningState.Unavailable
                : Classify(volume.Value.AvailableFreeBytes, volume.Value.TotalCapacityBytes),
            volume?.VolumeRoot,
            volume?.TotalCapacityBytes,
            volume?.AvailableFreeBytes,
            null,
            null,
            null,
            capturedAt,
            reason);

    private StoragePreflightResult Result(
        StoragePreflightStatus status,
        StoragePreflightRequest request,
        long? requiredFree,
        long? available) =>
        new(status, request.RequiredAdditionalBytes,
            _options.ReservedHeadroomBytes, requiredFree, available);

    private static long SaturatingAdd(long first, long second) =>
        first > long.MaxValue - second ? long.MaxValue : first + second;

    private static bool TryAdd(long first, long second, out long result)
    {
        if (first > long.MaxValue - second)
        {
            result = long.MaxValue;
            return false;
        }

        result = first + second;
        return true;
    }

    private static bool IsMetadataException(Exception exception) => exception is
        IOException or UnauthorizedAccessException or ArgumentException or
        NotSupportedException or SecurityException;
}
