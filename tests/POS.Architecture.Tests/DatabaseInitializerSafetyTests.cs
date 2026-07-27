using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                temporaryDirectory.DatabasePath);

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
            serviceProvider
                .GetRequiredService<
                    SqliteDatabaseSafetyService>()
                .CheckIntegrity(
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

        var safetyService =
            serviceProvider.GetRequiredService<
                SqliteDatabaseSafetyService>();

        Assert.True(
            safetyService
                .CheckIntegrity(
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
            serviceProvider
                .GetRequiredService<
                    SqliteDatabaseSafetyService>()
                .CheckIntegrity(
                    temporaryDirectory.DatabasePath)
                .IsSuccess);
    }

    private static ServiceProvider CreateServiceProvider(
        string databasePath)
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
