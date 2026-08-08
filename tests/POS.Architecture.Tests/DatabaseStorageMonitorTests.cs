using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Services;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Storage;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class DatabaseStorageMonitorTests
{
    private const string DatabasePath = @"C:\store\data\pos.db";
    private static readonly DateTimeOffset CapturedAt =
        new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Healthy_snapshot_uses_available_and_total_capacity()
    {
        var monitor = Monitor(Metadata(mainBytes: 100, available: 20_000, total: 100_000));
        var snapshot = await monitor.GetSnapshotAsync();
        Assert.Equal(DatabaseStorageSnapshotStatus.Available, snapshot.Status);
        Assert.Equal(StorageWarningState.Healthy, snapshot.WarningState);
        Assert.Equal(CapturedAt, snapshot.CapturedAtUtc);
    }

    [Theory]
    [InlineData(5_000, 100_000)]
    [InlineData(20_000, 200_000)]
    public async Task Either_warning_threshold_is_conservative(long available, long total)
    {
        var monitor = Monitor(Metadata(100, available, total), Options());
        Assert.Equal(StorageWarningState.Warning,
            (await monitor.GetSnapshotAsync()).WarningState);
    }

    [Fact]
    public async Task Exactly_at_absolute_warning_threshold_is_warning()
    {
        var options = Options();
        var snapshot = await Monitor(Metadata(100, options.WarningFreeBytes, 1_000_000), options)
            .GetSnapshotAsync();
        Assert.Equal(StorageWarningState.Warning, snapshot.WarningState);
    }

    [Fact]
    public async Task Exactly_at_percentage_warning_threshold_is_warning()
    {
        var snapshot = await Monitor(Metadata(100, 10_000, 100_000)).GetSnapshotAsync();
        Assert.Equal(StorageWarningState.Warning, snapshot.WarningState);
    }

    [Theory]
    [InlineData(999, StorageWarningState.Insufficient)]
    [InlineData(1000, StorageWarningState.Warning)]
    public async Task Reserved_headroom_boundary_is_strict(
        long available, StorageWarningState expected)
    {
        var snapshot = await Monitor(Metadata(100, available, 100_000)).GetSnapshotAsync();
        Assert.Equal(expected, snapshot.WarningState);
    }

    [Theory]
    [InlineData(20_000, 1_000, StoragePreflightStatus.Allowed, true)]
    [InlineData(6_000, 1_000, StoragePreflightStatus.AllowedWithWarning, true)]
    [InlineData(1_999, 1_000, StoragePreflightStatus.Insufficient, false)]
    [InlineData(2_000, 1_000, StoragePreflightStatus.AllowedWithWarning, true)]
    public async Task Operation_preflight_has_typed_boundary_semantics(
        long available, long required, StoragePreflightStatus expected, bool canProceed)
    {
        var monitor = Monitor(Metadata(100, available, 100_000));
        var snapshot = await monitor.GetSnapshotAsync();
        var result = monitor.EvaluatePreflight(snapshot, new StoragePreflightRequest(required));
        Assert.Equal(expected, result.Status);
        Assert.Equal(canProceed, result.CanProceed);
    }

    [Fact]
    public void Negative_required_bytes_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StoragePreflightRequest(-1));
    }

    [Theory]
    [InlineData(StoragePreflightStatus.Allowed, true)]
    [InlineData(StoragePreflightStatus.AllowedWithWarning, true)]
    [InlineData(StoragePreflightStatus.MetricsUnavailable, true)]
    [InlineData(StoragePreflightStatus.Insufficient, false)]
    public void CanProceed_is_derived_from_typed_status(
        StoragePreflightStatus status,
        bool expected)
    {
        var result = new StoragePreflightResult(status, 0, 1, 1, 1);
        Assert.Equal(expected, result.CanProceed);
    }

    [Fact]
    public async Task Metrics_unavailable_can_proceed_by_approved_policy()
    {
        var metadata = Metadata(100, 20_000, 100_000);
        metadata.ThrowOnVolume = true;
        var monitor = Monitor(metadata);
        var result = monitor.EvaluatePreflight(
            await monitor.GetSnapshotAsync(), new StoragePreflightRequest(500));
        Assert.Equal(StoragePreflightStatus.MetricsUnavailable, result.Status);
        Assert.True(result.CanProceed);
    }

    [Theory]
    [InlineData(1_000, 268_436_456)]
    [InlineData(3_000_000_000, 3_300_000_000)]
    [InlineData(2_684_354_561, 2_952_790_018)]
    public void Backup_estimate_uses_minimum_or_ceiling_percentage(
        long footprint, long expected)
    {
        Assert.Equal(expected, Monitor(Metadata()).EstimatePreMigrationBackupBytes(footprint));
    }

    [Fact]
    public async Task Overflow_saturates_and_preflight_fails_safe()
    {
        var monitor = Monitor(Metadata());
        Assert.Equal(long.MaxValue, monitor.EstimatePreMigrationBackupBytes(long.MaxValue));
        var snapshot = Snapshot(available: long.MaxValue);
        var result = monitor.EvaluatePreflight(
            snapshot, new StoragePreflightRequest(long.MaxValue));
        Assert.Equal(StoragePreflightStatus.Insufficient, result.Status);
        Assert.False(result.CanProceed);
    }

    [Fact]
    public void Exact_long_max_required_boundary_does_not_look_like_overflow()
    {
        var monitor = Monitor(Metadata());
        var result = monitor.EvaluatePreflight(
            Snapshot(long.MaxValue),
            new StoragePreflightRequest(long.MaxValue - Options().ReservedHeadroomBytes));
        Assert.True(result.CanProceed);
        Assert.Equal(long.MaxValue, result.RequiredFreeBytes);
    }

    [Fact]
    public async Task Absolute_warning_works_without_total_capacity()
    {
        var snapshot = await Monitor(Metadata(100, 5_000, null)).GetSnapshotAsync();
        Assert.Equal(StorageWarningState.Warning, snapshot.WarningState);
        Assert.Null(snapshot.TotalCapacityBytes);
    }

    [Fact]
    public async Task Unavailable_free_space_returns_typed_unavailable()
    {
        var metadata = Metadata(100, 20_000, 100_000);
        metadata.AvailableFreeBytes = null;
        var snapshot = await Monitor(metadata).GetSnapshotAsync();
        Assert.Equal(DatabaseStorageSnapshotStatus.MetadataUnavailable, snapshot.Status);
        Assert.Equal(StorageWarningState.Unavailable, snapshot.WarningState);
        Assert.Equal(StorageUnavailableReason.DriveMetadataUnavailable, snapshot.Reason);
    }

    [Fact]
    public async Task Snapshot_sums_main_and_three_exact_sidecars()
    {
        var metadata = Metadata(100, 20_000, 100_000);
        metadata.SetFile(DatabasePath + "-wal", 20);
        metadata.SetFile(DatabasePath + "-shm", 30);
        metadata.SetFile(DatabasePath + "-journal", 40);
        var snapshot = await Monitor(metadata).GetSnapshotAsync();
        Assert.Equal(100, snapshot.MainDatabaseBytes);
        Assert.Equal(90, snapshot.SidecarBytes);
        Assert.Equal(190, snapshot.TotalStorageFootprintBytes);
        Assert.Equal(2, metadata.Calls[DatabasePath]);
        Assert.Equal(2, metadata.Calls[DatabasePath + "-wal"]);
        Assert.Equal(2, metadata.Calls[DatabasePath + "-shm"]);
        Assert.Equal(2, metadata.Calls[DatabasePath + "-journal"]);
        Assert.DoesNotContain(metadata.RequestedPaths, path => path.Contains('*'));
    }

    [Fact]
    public async Task Missing_sidecars_are_zero_not_failure()
    {
        var snapshot = await Monitor(Metadata(123, 20_000, 100_000)).GetSnapshotAsync();
        Assert.Equal(0, snapshot.SidecarBytes);
        Assert.Equal(123, snapshot.TotalStorageFootprintBytes);
    }

    [Fact]
    public async Task Missing_main_has_nullable_sizes_and_keeps_volume_metrics()
    {
        var metadata = Metadata(null, 20_000, 100_000);
        var snapshot = await Monitor(metadata).GetSnapshotAsync();
        Assert.Equal(DatabaseStorageSnapshotStatus.DatabaseNotFound, snapshot.Status);
        Assert.Null(snapshot.MainDatabaseBytes);
        Assert.Null(snapshot.SidecarBytes);
        Assert.Null(snapshot.TotalStorageFootprintBytes);
        Assert.Equal(20_000, snapshot.AvailableFreeBytes);
    }

    [Fact]
    public async Task Missing_main_does_not_create_parent_or_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "POS-Storage-PurePath", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "missing", "pos.db");
        try
        {
            var metadata = new FakeMetadata(path, null, 20_000, 100_000);
            var snapshot = await Monitor(metadata, databasePath: path).GetSnapshotAsync();
            Assert.Equal(DatabaseStorageSnapshotStatus.DatabaseNotFound, snapshot.Status);
            Assert.False(Directory.Exists(root));
            Assert.False(File.Exists(path));
        }
        finally
        {
            Assert.False(Directory.Exists(root));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Invalid_path_returns_typed_unavailable(string path)
    {
        var snapshot = await Monitor(Metadata(), databasePath: path).GetSnapshotAsync();
        Assert.Equal(StorageUnavailableReason.InvalidDatabasePath, snapshot.Reason);
    }

    [Fact]
    public void Pure_and_existing_resolution_share_relative_semantics()
    {
        const string relative = "data/r24a-relative.db";
        var pure = DatabasePathResolver.ResolveDatabasePathWithoutCreatingDirectory(relative);
        Assert.Equal(Path.Combine(RepositoryLocator.Root, "data", "r24a-relative.db"), pure);
        Assert.False(File.Exists(pure));
    }

    [Fact]
    public void Relative_escape_is_still_rejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DatabasePathResolver.ResolveDatabasePathWithoutCreatingDirectory("../escape.db"));
    }

    [Theory]
    [InlineData("main")]
    [InlineData("parent")]
    [InlineData("sidecar")]
    public async Task Reparse_points_are_rejected(string target)
    {
        var metadata = Metadata(100, 20_000, 100_000);
        if (target == "main") metadata.SetReparse(DatabasePath, isDirectory: false);
        if (target == "parent") metadata.SetReparse(@"C:\store", true);
        if (target == "sidecar") metadata.SetReparse(DatabasePath + "-wal", false);
        var snapshot = await Monitor(metadata).GetSnapshotAsync();
        Assert.Equal(DatabaseStorageSnapshotStatus.MetadataUnavailable, snapshot.Status);
        Assert.Equal(StorageUnavailableReason.ReparsePointRejected, snapshot.Reason);
    }

    [Fact]
    public async Task Parent_reparse_race_is_revalidated_before_snapshot_returns()
    {
        var metadata = Metadata(100, 20_000, 100_000);
        metadata.SetDirectory(@"C:\store");
        metadata.SetDirectory(@"C:\store\data");
        metadata.ChangeToReparseOnSecondRead = @"C:\store";

        var snapshot = await Monitor(metadata).GetSnapshotAsync();

        Assert.Equal(DatabaseStorageSnapshotStatus.MetadataUnavailable, snapshot.Status);
        Assert.Equal(StorageUnavailableReason.ReparsePointRejected, snapshot.Reason);
    }

    [Fact]
    public void System_metadata_provider_does_not_use_ambiguous_exists_probes()
    {
        var source = File.ReadAllText(RepositoryLocator.GetPath(
            "src", "POS.Infrastructure", "Storage", "SystemStorageMetadataProvider.cs"));
        Assert.DoesNotContain("File.Exists", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Exists", source, StringComparison.Ordinal);
        Assert.Contains("File.GetAttributes", source, StringComparison.Ordinal);
        Assert.Contains("FileNotFoundException", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("file")]
    [InlineData("drive")]
    [InlineData("race")]
    public async Task Metadata_failures_and_bounded_race_do_not_escape(string failure)
    {
        var metadata = Metadata(100, 20_000, 100_000);
        if (failure == "file") metadata.ThrowOnPath = DatabasePath;
        if (failure == "drive") metadata.ThrowOnVolume = true;
        if (failure == "race") metadata.ChangeOnSecondRead = DatabasePath + "-wal";
        DatabaseStorageSnapshot? snapshot = null;
        var exception = await Record.ExceptionAsync(async () =>
            snapshot = await Monitor(metadata).GetSnapshotAsync());
        Assert.Null(exception);
        Assert.NotNull(snapshot);
        Assert.Equal(DatabaseStorageSnapshotStatus.MetadataUnavailable, snapshot.Status);
    }

    [Fact]
    public async Task Cancellation_between_metadata_reads_is_observed()
    {
        using var cancellation = new CancellationTokenSource();
        var metadata = Metadata(100, 20_000, 100_000);
        metadata.CancelOnPath = DatabasePath;
        metadata.CancellationSource = cancellation;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Monitor(metadata).GetSnapshotAsync(cancellation.Token));
        Assert.Equal(1, metadata.Calls[DatabasePath]);
    }

    [Fact]
    public void Options_defaults_binding_and_validation_match_approved_policy()
    {
        var defaults = new DatabaseStorageOptions();
        Assert.Equal(5_368_709_120, defaults.WarningFreeBytes);
        Assert.Equal(10m, defaults.WarningFreePercentage);
        Assert.Equal(536_870_912, defaults.ReservedHeadroomBytes);
        Assert.Equal(268_435_456, defaults.BackupEstimateMinimumPaddingBytes);
        Assert.Equal(10m, defaults.BackupEstimatePaddingPercentage);
        defaults.Validate();

        Assert.Throws<InvalidOperationException>(() =>
            new DatabaseStorageOptions { WarningFreeBytes = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new DatabaseStorageOptions { WarningFreePercentage = 101 }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new DatabaseStorageOptions { ReservedHeadroomBytes = 6_000_000_000 }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new DatabaseStorageOptions { BackupEstimatePaddingPercentage = -1 }.Validate());

        var configuration = new ConfigurationBuilder().AddJsonFile(
            RepositoryLocator.GetPath("src", "POS.Wpf", "appsettings.json")).Build();
        var bound = new DatabaseStorageOptions();
        configuration.GetSection(DatabaseStorageOptions.SectionName).Bind(bound);
        Assert.Equal(defaults.WarningFreeBytes, bound.WarningFreeBytes);
        Assert.Equal(defaults.WarningFreePercentage, bound.WarningFreePercentage);
        Assert.Equal(defaults.ReservedHeadroomBytes, bound.ReservedHeadroomBytes);
        Assert.Equal(defaults.BackupEstimateMinimumPaddingBytes,
            bound.BackupEstimateMinimumPaddingBytes);
        Assert.Equal(defaults.BackupEstimatePaddingPercentage,
            bound.BackupEstimatePaddingPercentage);
    }

    [Fact]
    public void Di_registers_monitor_and_metadata_provider_once_as_singletons()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Infrastructure:DatabasePath"] = DatabasePath,
                ["Infrastructure:ApplyMigrationsOnStartup"] = "false"
            }).Build());
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Infrastructure:DatabasePath"] = DatabasePath,
                ["Infrastructure:ApplyMigrationsOnStartup"] = "false"
            }).Build());

        var monitor = Assert.Single(services, d => d.ServiceType == typeof(IDatabaseStorageMonitor));
        var metadata = Assert.Single(services, d => d.ServiceType == typeof(IStorageMetadataProvider));
        Assert.Equal(ServiceLifetime.Singleton, monitor.Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, metadata.Lifetime);

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IDatabaseStorageMonitor>();
        var second = provider.GetRequiredService<IDatabaseStorageMonitor>();
        Assert.Same(first, second);
    }

    [Fact]
    public void Application_contract_has_no_outer_or_filesystem_dependencies()
    {
        var project = File.ReadAllText(RepositoryLocator.GetPath(
            "src", "POS.Application", "POS.Application.csproj"));
        Assert.DoesNotContain("POS.Infrastructure", project, StringComparison.Ordinal);
        Assert.DoesNotContain("POS.Wpf", project, StringComparison.Ordinal);
        Assert.DoesNotContain("EntityFrameworkCore", project, StringComparison.Ordinal);
        foreach (var file in new[]
        {
            "IDatabaseStorageMonitor.cs",
            "DatabaseStorageSnapshot.cs",
            "StoragePreflightResult.cs"
        })
        {
            var source = File.ReadAllText(RepositoryLocator.GetPath(
                "src", "POS.Application", "Abstractions", "Services", file));
            Assert.DoesNotContain("System.IO", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FileInfo", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DriveInfo", source, StringComparison.Ordinal);
        }
    }

    private static DatabaseStorageMonitor Monitor(
        FakeMetadata metadata,
        DatabaseStorageOptions? options = null,
        string databasePath = DatabasePath) =>
        new(new InfrastructureOptions { DatabasePath = databasePath },
            options ?? Options(), metadata, new FixedTimeProvider(CapturedAt));

    private static DatabaseStorageOptions Options() => new()
    {
        WarningFreeBytes = 5_000,
        WarningFreePercentage = 10,
        ReservedHeadroomBytes = 1_000,
        BackupEstimateMinimumPaddingBytes = 268_435_456,
        BackupEstimatePaddingPercentage = 10
    };

    private static FakeMetadata Metadata(
        long? mainBytes = 100,
        long? available = 20_000,
        long? total = 100_000) =>
        new(DatabasePath, mainBytes, available, total);

    private static DatabaseStorageSnapshot Snapshot(long? available) => new(
        DatabaseStorageSnapshotStatus.Available,
        StorageWarningState.Healthy,
        @"C:\",
        long.MaxValue,
        available,
        1,
        0,
        1,
        CapturedAt,
        StorageUnavailableReason.None);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeMetadata : IStorageMetadataProvider
    {
        private readonly Dictionary<string, StoragePathMetadata> _paths =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly string _databasePath;
        private readonly long? _totalCapacityBytes;

        public FakeMetadata(string databasePath, long? mainBytes, long? available, long? total)
        {
            _databasePath = Path.GetFullPath(databasePath);
            AvailableFreeBytes = available;
            _totalCapacityBytes = total;
            if (mainBytes.HasValue) SetFile(_databasePath, mainBytes.Value);
        }

        public long? AvailableFreeBytes { get; set; }
        public bool ThrowOnVolume { get; set; }
        public string? ThrowOnPath { get; set; }
        public string? ChangeOnSecondRead { get; set; }
        public string? ChangeToReparseOnSecondRead { get; set; }
        public string? CancelOnPath { get; set; }
        public CancellationTokenSource? CancellationSource { get; set; }
        public Dictionary<string, int> Calls { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RequestedPaths { get; } = [];

        public void SetFile(string path, long length) =>
            _paths[Path.GetFullPath(path)] = new(true, false, false, length);

        public void SetReparse(string path, bool isDirectory) =>
            _paths[Path.GetFullPath(path)] = new(true, isDirectory, true, null);

        public void SetDirectory(string path) =>
            _paths[Path.GetFullPath(path)] = new(true, true, false, null);

        public StoragePathMetadata GetPathMetadata(string path)
        {
            path = Path.GetFullPath(path);
            RequestedPaths.Add(path);
            Calls[path] = Calls.GetValueOrDefault(path) + 1;
            if (string.Equals(path, ThrowOnPath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("canary-path-detail");
            if (string.Equals(path, CancelOnPath, StringComparison.OrdinalIgnoreCase))
                CancellationSource?.Cancel();
            if (string.Equals(path, ChangeOnSecondRead, StringComparison.OrdinalIgnoreCase) &&
                Calls[path] == 2)
                return new StoragePathMetadata(true, false, false, 1);
            if (string.Equals(path, ChangeToReparseOnSecondRead,
                    StringComparison.OrdinalIgnoreCase) && Calls[path] == 2)
                return new StoragePathMetadata(true, true, true, null);
            return _paths.GetValueOrDefault(path);
        }

        public StorageVolumeMetadata GetVolumeMetadata(string path)
        {
            if (ThrowOnVolume) throw new IOException("canary-volume-detail");
            return new StorageVolumeMetadata(
                Path.GetPathRoot(_databasePath)!, _totalCapacityBytes, AvailableFreeBytes);
        }
    }
}
