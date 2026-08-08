using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using POS.Application.Abstractions.Services;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class DatabaseInitializerSafetyTests
{
    [Fact]
    public void
        DI_must_resolve_database_safety_service_as_singleton()
    {
        using var temporaryDirectory =
            new TemporaryDirectory();

        using var serviceProvider =
            CreateServiceProvider(
                temporaryDirectory.DatabasePath,
                useProductionStorageMonitor: true);

        var firstInstance =
            serviceProvider.GetRequiredService<
                SqliteDatabaseSafetyService>();

        var secondInstance =
            serviceProvider.GetRequiredService<
                SqliteDatabaseSafetyService>();

        Assert.Same(
            firstInstance,
            secondInstance);

        using var scope =
            serviceProvider.CreateScope();

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<
                    DatabaseInitializer>());
    }

    [Fact]
    public async Task
        Fresh_database_must_migrate_without_pre_migration_backup()
    {
        using var temporaryDirectory =
            new TemporaryDirectory();

        Assert.False(
            File.Exists(
                temporaryDirectory.DatabasePath));

        using var serviceProvider =
            CreateServiceProvider(
                temporaryDirectory.DatabasePath);

        await InitializeAsync(
            serviceProvider);

        Assert.True(
            File.Exists(
                temporaryDirectory.DatabasePath));

        var appliedMigrations =
            await GetAppliedMigrationsAsync(
                temporaryDirectory.DatabasePath);

        var availableMigrations =
            GetAvailableMigrations(
                temporaryDirectory.DatabasePath);

        Assert.Equal(
            availableMigrations,
            appliedMigrations);

        var integrity =
            SqliteDatabaseSafetyService.CheckIntegrity(
                temporaryDirectory.DatabasePath);

        Assert.True(
            integrity.IsSuccess);

        Assert.Empty(
            GetBackupFiles(
                temporaryDirectory.DatabasePath));
    }

    [Fact]
    public async Task
        Existing_database_with_pending_migrations_must_create_verified_backup()
    {
        using var temporaryDirectory =
            new TemporaryDirectory();

        var migrationState =
            await CreateDatabaseAtPreviousMigrationAsync(
                temporaryDirectory.DatabasePath);

        using var serviceProvider =
            CreateServiceProvider(
                temporaryDirectory.DatabasePath);

        await InitializeAsync(
            serviceProvider);

        var backupPath =
            Assert.Single(
                GetBackupFiles(
                    temporaryDirectory.DatabasePath));

        Assert.True(
            new FileInfo(
                    backupPath)
                .Length > 0);

        Assert.True(
            SqliteDatabaseSafetyService.CheckIntegrity(
                    backupPath)
                .IsSuccess);

        var backupMigrations =
            await GetAppliedMigrationsAsync(
                backupPath);

        Assert.Equal(
            migrationState.PreviousMigration,
            backupMigrations[^1]);

        Assert.DoesNotContain(
            migrationState.LatestMigration,
            backupMigrations);

        var sourceMigrations =
            await GetAppliedMigrationsAsync(
                temporaryDirectory.DatabasePath);

        Assert.Equal(
            migrationState.LatestMigration,
            sourceMigrations[^1]);
    }

    [Fact]
    public async Task
        Database_without_pending_migrations_must_not_create_backup()
    {
        using var temporaryDirectory =
            new TemporaryDirectory();

        using var serviceProvider =
            CreateServiceProvider(
                temporaryDirectory.DatabasePath);

        await InitializeAsync(
            serviceProvider);

        await InitializeAsync(
            serviceProvider);

        Assert.Empty(
            GetBackupFiles(
                temporaryDirectory.DatabasePath));
    }

    [Fact]
    public async Task
        Backup_failure_must_block_migration()
    {
        using var temporaryDirectory =
            new TemporaryDirectory();

        var migrationState =
            await CreateDatabaseAtPreviousMigrationAsync(
                temporaryDirectory.DatabasePath);

        File.WriteAllText(
            Path.Combine(
                temporaryDirectory.DirectoryPath,
                "backups"),
            "blocks backup directory creation");

        using var serviceProvider =
            CreateServiceProvider(
                temporaryDirectory.DatabasePath);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => InitializeAsync(
                serviceProvider));

        var appliedMigrations =
            await GetAppliedMigrationsAsync(
                temporaryDirectory.DatabasePath);

        Assert.Equal(
            migrationState.PreviousMigration,
            appliedMigrations[^1]);

        Assert.DoesNotContain(
            migrationState.LatestMigration,
            appliedMigrations);

        Assert.True(
            SqliteDatabaseSafetyService.CheckIntegrity(
                    temporaryDirectory.DatabasePath)
                .IsSuccess);
    }

    [Theory]
    [InlineData(StoragePreflightStatus.Allowed)]
    [InlineData(StoragePreflightStatus.AllowedWithWarning)]
    [InlineData(StoragePreflightStatus.MetricsUnavailable)]
    public async Task
        Proceeding_storage_statuses_run_preflight_before_backup_and_migration(
            StoragePreflightStatus status)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var migrationState = await CreateDatabaseAtPreviousMigrationAsync(
            temporaryDirectory.DatabasePath);
        var monitor = new RecordingStorageMonitor(status)
        {
            OnEvaluate = () => Assert.Empty(GetBackupFiles(
                temporaryDirectory.DatabasePath))
        };

        using var serviceProvider = CreateServiceProvider(
            temporaryDirectory.DatabasePath, monitor);

        await InitializeAsync(serviceProvider);

        Assert.Equal(["snapshot", "estimate", "evaluate"], monitor.Calls);
        Assert.Equal(1, monitor.SnapshotCount);
        Assert.Equal(1, monitor.EvaluateCount);
        Assert.True(monitor.LastCancellationToken.CanBeCanceled is false);
        Assert.Single(GetBackupFiles(temporaryDirectory.DatabasePath));
        Assert.Equal(migrationState.LatestMigration,
            (await GetAppliedMigrationsAsync(temporaryDirectory.DatabasePath))[^1]);
    }

    [Fact]
    public async Task
        Insufficient_storage_blocks_before_backup_and_migration()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var migrationState = await CreateDatabaseAtPreviousMigrationAsync(
            temporaryDirectory.DatabasePath);
        var monitor = new RecordingStorageMonitor(
            StoragePreflightStatus.Insufficient);
        using var serviceProvider = CreateServiceProvider(
            temporaryDirectory.DatabasePath, monitor);

        var exception = await Assert.ThrowsAsync<StoragePreflightException>(
            () => InitializeAsync(serviceProvider));

        Assert.Equal(StoragePreflightStatus.Insufficient,
            exception.Result.Status);
        Assert.False(exception.Result.CanProceed);
        Assert.Empty(GetBackupFiles(temporaryDirectory.DatabasePath));
        Assert.Equal(migrationState.PreviousMigration,
            (await GetAppliedMigrationsAsync(temporaryDirectory.DatabasePath))[^1]);
        Assert.Equal(["snapshot", "estimate", "evaluate"], monitor.Calls);
    }

    [Fact]
    public async Task
        No_pending_migration_skips_storage_preflight()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var monitor = new RecordingStorageMonitor(StoragePreflightStatus.Allowed);
        using var serviceProvider = CreateServiceProvider(
            temporaryDirectory.DatabasePath, monitor);

        await InitializeAsync(serviceProvider);
        monitor.Reset();
        await InitializeAsync(serviceProvider);

        Assert.Empty(monitor.Calls);
        Assert.Empty(GetBackupFiles(temporaryDirectory.DatabasePath));
    }

    [Fact]
    public async Task
        New_database_uses_zero_additional_bytes_and_creates_no_backup()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var monitor = new RecordingStorageMonitor(
            StoragePreflightStatus.Allowed,
            DatabaseStorageSnapshotStatus.DatabaseNotFound,
            totalStorageFootprintBytes: null);
        using var serviceProvider = CreateServiceProvider(
            temporaryDirectory.DatabasePath, monitor);

        await InitializeAsync(serviceProvider);

        Assert.Equal(0, monitor.LastRequest?.RequiredAdditionalBytes);
        Assert.Equal(0, monitor.EstimateCount);
        Assert.Empty(GetBackupFiles(temporaryDirectory.DatabasePath));
        Assert.True(File.Exists(temporaryDirectory.DatabasePath));
    }

    [Fact]
    public async Task
        Cancellation_from_storage_monitor_is_not_translated()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var migrationState = await CreateDatabaseAtPreviousMigrationAsync(
            temporaryDirectory.DatabasePath);
        var monitor = new RecordingStorageMonitor(StoragePreflightStatus.Allowed)
        {
            CancelDuringSnapshot = true
        };
        using var serviceProvider = CreateServiceProvider(
            temporaryDirectory.DatabasePath, monitor);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
                .InitializeAsync(cancellation.Token);
        });

        Assert.Equal(cancellation.Token, monitor.LastCancellationToken);
        Assert.Equal(0, monitor.EvaluateCount);
        Assert.Empty(GetBackupFiles(temporaryDirectory.DatabasePath));
        Assert.Equal(migrationState.PreviousMigration,
            (await GetAppliedMigrationsAsync(temporaryDirectory.DatabasePath))[^1]);
    }

    [Fact]
    public async Task
        Existing_database_with_unavailable_footprint_fails_safe()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var migrationState = await CreateDatabaseAtPreviousMigrationAsync(
            temporaryDirectory.DatabasePath);
        var monitor = new RecordingStorageMonitor(
            StoragePreflightStatus.Insufficient,
            DatabaseStorageSnapshotStatus.MetadataUnavailable,
            totalStorageFootprintBytes: null);
        using var serviceProvider = CreateServiceProvider(
            temporaryDirectory.DatabasePath, monitor);

        await Assert.ThrowsAsync<StoragePreflightException>(
            () => InitializeAsync(serviceProvider));

        Assert.Equal(long.MaxValue, monitor.LastEstimatedFootprintBytes);
        Assert.Equal(1, monitor.EstimateCount);
        Assert.Empty(GetBackupFiles(temporaryDirectory.DatabasePath));
        Assert.Equal(migrationState.PreviousMigration,
            (await GetAppliedMigrationsAsync(temporaryDirectory.DatabasePath))[^1]);
    }

    [Fact]
    public void Unknown_preflight_status_is_fail_closed()
    {
        var result = new StoragePreflightResult(
            (StoragePreflightStatus)int.MaxValue, 0, 1, 1, 1);

        Assert.False(result.CanProceed);
        Assert.Throws<ArgumentException>(() =>
            new StoragePreflightException(result));
    }

    [Fact]
    public async Task Unknown_preflight_status_blocks_startup_before_mutation()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var migrationState = await CreateDatabaseAtPreviousMigrationAsync(
            temporaryDirectory.DatabasePath);
        var monitor = new RecordingStorageMonitor(
            (StoragePreflightStatus)int.MaxValue);
        using var serviceProvider = CreateServiceProvider(
            temporaryDirectory.DatabasePath, monitor);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => InitializeAsync(serviceProvider));

        Assert.Empty(GetBackupFiles(temporaryDirectory.DatabasePath));
        Assert.Equal(migrationState.PreviousMigration,
            (await GetAppliedMigrationsAsync(temporaryDirectory.DatabasePath))[^1]);
    }

    private static ServiceProvider CreateServiceProvider(
        string databasePath,
        IDatabaseStorageMonitor? storageMonitor = null,
        bool useProductionStorageMonitor = false)
    {
        var configurationValues =
            new Dictionary<string, string?>
            {
                ["Infrastructure:DatabasePath"] =
                    databasePath,
                ["Infrastructure:ApplyMigrationsOnStartup"] =
                    bool.TrueString,
                ["Infrastructure:SeedDemoProductCatalog"] =
                    bool.FalseString,
                ["Infrastructure:SeedDefaultAdministrator"] =
                    bool.FalseString
            };

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    configurationValues)
                .Build();

        var services =
            new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructure(
            configuration);

        if (!useProductionStorageMonitor)
        {
            services.RemoveAll<IDatabaseStorageMonitor>();
            services.AddSingleton(storageMonitor ??
                new RecordingStorageMonitor(StoragePreflightStatus.Allowed));
        }

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    private static async Task InitializeAsync(
        ServiceProvider serviceProvider)
    {
        await using var scope =
            serviceProvider.CreateAsyncScope();

        var initializer =
            scope.ServiceProvider
                .GetRequiredService<
                    DatabaseInitializer>();

        await initializer.InitializeAsync();
    }

    private static async Task<MigrationState>
        CreateDatabaseAtPreviousMigrationAsync(
            string databasePath)
    {
        await using var context =
            CreateDbContext(
                databasePath);

        var migrations =
            context.Database
                .GetMigrations()
                .ToArray();

        Assert.True(
            migrations.Length >= 2,
            "Cần ít nhất hai migration thực tế để kiểm tra backup.");

        var previousMigration =
            migrations[^2];

        await context
            .GetService<IMigrator>()
            .MigrateAsync(
                previousMigration);

        return new MigrationState(
            previousMigration,
            migrations[^1]);
    }

    private static string[]
        GetAvailableMigrations(
            string databasePath)
    {
        using var context =
            CreateDbContext(
                databasePath);

        return context.Database
            .GetMigrations()
            .ToArray();
    }

    private static async Task<string[]>
        GetAppliedMigrationsAsync(
            string databasePath)
    {
        await using var context =
            CreateDbContext(
                databasePath);

        var migrations =
            await context.Database
                .GetAppliedMigrationsAsync();

        return migrations.ToArray();
    }

    private static PosDbContext CreateDbContext(
        string databasePath)
    {
        var connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                ForeignKeys = true,
                Pooling = false
            }
            .ToString();

        var options =
            new DbContextOptionsBuilder<
                    PosDbContext>()
                .UseSqlite(
                    connectionString)
                .Options;

        return new PosDbContext(
            options);
    }

    private static string[] GetBackupFiles(
        string databasePath)
    {
        var databaseDirectory =
            Path.GetDirectoryName(
                databasePath)!;

        var backupDirectory =
            Path.Combine(
                databaseDirectory,
                "backups",
                "pre-migration");

        return Directory.Exists(
                backupDirectory)
            ? Directory.GetFiles(
                backupDirectory,
                "*.db",
                SearchOption.TopDirectoryOnly)
            : [];
    }

    private sealed record MigrationState(
        string PreviousMigration,
        string LatestMigration);

    private sealed class RecordingStorageMonitor : IDatabaseStorageMonitor
    {
        private readonly StoragePreflightStatus _status;
        private readonly DatabaseStorageSnapshot _snapshot;

        public RecordingStorageMonitor(
            StoragePreflightStatus status,
            DatabaseStorageSnapshotStatus snapshotStatus =
                DatabaseStorageSnapshotStatus.Available,
            long? totalStorageFootprintBytes = 100)
        {
            _status = status;
            _snapshot = new DatabaseStorageSnapshot(
                snapshotStatus,
                status is StoragePreflightStatus.MetricsUnavailable
                    ? StorageWarningState.Unavailable
                    : StorageWarningState.Healthy,
                null,
                status is StoragePreflightStatus.MetricsUnavailable ? null : 100_000,
                status is StoragePreflightStatus.MetricsUnavailable ? null : 20_000,
                totalStorageFootprintBytes,
                totalStorageFootprintBytes.HasValue ? 0 : null,
                totalStorageFootprintBytes,
                DateTimeOffset.UnixEpoch,
                status is StoragePreflightStatus.MetricsUnavailable
                    ? StorageUnavailableReason.DriveMetadataUnavailable
                    : snapshotStatus is DatabaseStorageSnapshotStatus.DatabaseNotFound
                        ? StorageUnavailableReason.DatabaseNotFound
                        : StorageUnavailableReason.None);
        }

        public List<string> Calls { get; } = [];
        public int SnapshotCount { get; private set; }
        public int EstimateCount { get; private set; }
        public int EvaluateCount { get; private set; }
        public StoragePreflightRequest? LastRequest { get; private set; }
        public long? LastEstimatedFootprintBytes { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public bool CancelDuringSnapshot { get; init; }
        public Action? OnEvaluate { get; init; }

        public Task<DatabaseStorageSnapshot> GetSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            Calls.Add("snapshot");
            SnapshotCount++;
            LastCancellationToken = cancellationToken;
            if (CancelDuringSnapshot)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Task.FromResult(_snapshot);
        }

        public StoragePreflightResult EvaluatePreflight(
            DatabaseStorageSnapshot snapshot,
            StoragePreflightRequest request)
        {
            Calls.Add("evaluate");
            EvaluateCount++;
            LastRequest = request;
            OnEvaluate?.Invoke();
            return new StoragePreflightResult(
                _status,
                request.RequiredAdditionalBytes,
                1_000,
                request.RequiredAdditionalBytes + 1_000,
                _status is StoragePreflightStatus.MetricsUnavailable ? null : 20_000);
        }

        public long EstimatePreMigrationBackupBytes(
            long sqliteStorageFootprintBytes)
        {
            Calls.Add("estimate");
            EstimateCount++;
            LastEstimatedFootprintBytes = sqliteStorageFootprintBytes;
            return 1_234;
        }

        public void Reset()
        {
            Calls.Clear();
            SnapshotCount = 0;
            EstimateCount = 0;
            EvaluateCount = 0;
            LastRequest = null;
            LastEstimatedFootprintBytes = null;
        }
    }

    private sealed class TemporaryDirectory :
        IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "POS-DatabaseInitializerSafetyTests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                DirectoryPath);
        }

        public string DirectoryPath { get; }

        public string DatabasePath =>
            Path.Combine(
                DirectoryPath,
                "test.db");

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(
                    DirectoryPath))
            {
                Directory.Delete(
                    DirectoryPath,
                    recursive: true);
            }
        }
    }
}
