using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Services;
using POS.Domain.Entities;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Support;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ManualBackupServiceTests
{
    [Fact]
    public void Busy_is_distinct_typed_non_success_status()
    {
        var result = ManualBackupResult.Failure(ManualBackupStatus.Busy);
        Assert.Equal(ManualBackupStatus.Busy, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Busy_gate_returns_immediately_without_destination_or_source_side_effect()
    {
        var testDirectory = CreateTestDirectory();
        var contextDirectory = CreateTestDirectory();
        await using var context = await CreateSourceContextAsync(contextDirectory, "context");
        using var coordinator = new BackupCoordinator();
        Assert.True(coordinator.TryAcquire(out var held));
        var destination = Path.Combine(testDirectory, "must-not-exist");
        var service = CreateService(context, Path.Combine(testDirectory, "missing-source.db"),
            new FixedClock(DateTimeOffset.UtcNow), coordinator);

        var result = await service.BackupAsync(new ManualBackupRequest(destination));

        Assert.Equal(ManualBackupStatus.Busy, result.Status);
        Assert.False(Directory.Exists(destination));
        held!.Dispose();
        await context.DisposeAsync();
        DeleteTestDirectory(contextDirectory);
        DeleteTestDirectory(testDirectory);
    }

    [Fact]
    public async Task Expected_failure_releases_gate_for_next_operation()
    {
        var testDirectory = CreateTestDirectory();
        await using var context = await CreateSourceContextAsync(testDirectory, "release");
        using var coordinator = new BackupCoordinator();
        var service = CreateService(context, Path.Combine(testDirectory, "missing.db"),
            new FixedClock(DateTimeOffset.UtcNow), coordinator);
        var result = await service.BackupAsync(new ManualBackupRequest(testDirectory));
        Assert.Equal(ManualBackupStatus.SourceUnavailable, result.Status);
        Assert.True(coordinator.TryAcquire(out var lease));
        lease!.Dispose();
        await context.DisposeAsync();
        DeleteTestDirectory(testDirectory);
    }

    [Fact]
    public async Task Manual_backup_success_must_create_verified_copy_with_hash_and_size()
    {
        var testDirectory = CreateTestDirectory();
        await using var sourceContext = await CreateSourceContextAsync(testDirectory, "backup-source");
        var sourcePath = Path.Combine(testDirectory, "source.db");
        var sourceInfoBefore = new FileInfo(sourcePath);
        var sourceHashBefore = ComputeSha256(sourcePath);

        var service = CreateService(sourceContext, sourcePath, new FixedClock(
            new DateTimeOffset(2026, 8, 9, 8, 30, 45, TimeSpan.Zero)));

        var destination = Path.Combine(testDirectory, "destination");
        Directory.CreateDirectory(destination);

        var result = await service.BackupAsync(new ManualBackupRequest(destination));

        Assert.True(result.IsSuccess, result.Status.ToString());
        var backupFilePath = result.BackupFilePath;
        if (backupFilePath is null)
        {
            throw new InvalidOperationException("A successful backup must include a file path.");
        }

        Assert.True(Path.IsPathFullyQualified(backupFilePath));
        Assert.True(File.Exists(backupFilePath));
        Assert.Equal(result.BackupFileSizeBytes, new FileInfo(backupFilePath).Length);
        Assert.Equal(result.Sha256Hex, ComputeSha256(backupFilePath));
        Assert.NotNull(result.CompletedAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 8, 9, 8, 30, 45, TimeSpan.Zero), result.CompletedAtUtc);

        await using (var backupContext = CreateContext(backupFilePath))
        {
            Assert.False(backupContext.Database.HasPendingModelChanges());
            Assert.Equal("backup-source", await backupContext.Categories.Select(x => x.Name).SingleAsync());
            Assert.True(SqliteDatabaseSafetyService.CheckIntegrity(backupFilePath).IsSuccess);
        }

        Assert.Equal(sourceInfoBefore.Length, new FileInfo(sourcePath).Length);
        Assert.Equal(sourceHashBefore, ComputeSha256(sourcePath));
        await sourceContext.DisposeAsync();
        DeleteTestDirectory(testDirectory);
    }

    [Fact]
    public async Task Manual_backup_must_include_committed_wal_marker()
    {
        var testDirectory = CreateTestDirectory();
        await using var sourceContext = await CreateWalSourceContextAsync(testDirectory, "committed-wal-marker");
        var sourcePath = Path.Combine(testDirectory, "wal-source.db");
        var service = CreateService(sourceContext, sourcePath, new FixedClock(
            new DateTimeOffset(2026, 8, 9, 8, 30, 45, TimeSpan.Zero)));

        var destination = Path.Combine(testDirectory, "destination");
        Directory.CreateDirectory(destination);

        var result = await service.BackupAsync(new ManualBackupRequest(destination));

        Assert.True(result.IsSuccess, result.Status.ToString());
        Assert.False(File.Exists(result.BackupFilePath + "-wal"));
        Assert.False(File.Exists(result.BackupFilePath + "-shm"));

        await using var backupContext = CreateContext(result.BackupFilePath!);
        Assert.Equal("committed-wal-marker", await backupContext.Categories.Select(x => x.Name).SingleAsync());
        await sourceContext.DisposeAsync();
        DeleteTestDirectory(testDirectory);
    }

    [Fact]
    public async Task Existing_destination_must_not_be_overwritten_and_second_backup_uses_unique_file()
    {
        var testDirectory = CreateTestDirectory();
        await using var sourceContext = await CreateSourceContextAsync(testDirectory, "first");
        var sourcePath = Path.Combine(testDirectory, "source.db");
        var service = CreateService(sourceContext, sourcePath, new FixedClock(
            new DateTimeOffset(2026, 8, 9, 8, 30, 45, TimeSpan.Zero)));

        var destination = Path.Combine(testDirectory, "destination");
        Directory.CreateDirectory(destination);

        var first = await service.BackupAsync(new ManualBackupRequest(destination));
        var second = await service.BackupAsync(new ManualBackupRequest(destination));

        Assert.True(first.IsSuccess, first.Status.ToString());
        Assert.True(second.IsSuccess, second.Status.ToString());
        Assert.NotEqual(first.BackupFilePath, second.BackupFilePath);
        Assert.True(File.Exists(first.BackupFilePath));
        Assert.True(File.Exists(second.BackupFilePath));
        await sourceContext.DisposeAsync();
        DeleteTestDirectory(testDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative")]
    public async Task Invalid_destination_must_return_failure(string destination)
    {
        var testDirectory = CreateTestDirectory();
        await using var sourceContext = await CreateSourceContextAsync(testDirectory, "invalid-destination");
        var sourcePath = Path.Combine(testDirectory, "source.db");
        var service = CreateService(sourceContext, sourcePath, new FixedClock(
            new DateTimeOffset(2026, 8, 9, 8, 30, 45, TimeSpan.Zero)));

        var result = await service.BackupAsync(new ManualBackupRequest(destination));

        Assert.False(result.IsSuccess);
        Assert.Equal(ManualBackupStatus.InvalidDestination, result.Status);
        await sourceContext.DisposeAsync();
        DeleteTestDirectory(testDirectory);
    }

    [Fact]
    public async Task Missing_source_must_return_failure_without_output()
    {
        var testDirectory = CreateTestDirectory();
        var sourcePath = Path.Combine(testDirectory, "missing.db");
        var contextDirectory = CreateTestDirectory();
        await using var context = await CreateSourceContextAsync(contextDirectory, "missing-source");
        var service = CreateService(context, sourcePath, new FixedClock(
            new DateTimeOffset(2026, 8, 9, 8, 30, 45, TimeSpan.Zero)));
        var destination = Path.Combine(testDirectory, "destination");
        Directory.CreateDirectory(destination);

        var result = await service.BackupAsync(new ManualBackupRequest(destination));

        Assert.False(result.IsSuccess);
        Assert.Equal(ManualBackupStatus.SourceUnavailable, result.Status);
        await context.DisposeAsync();
        DeleteTestDirectory(contextDirectory);
        DeleteTestDirectory(testDirectory);
    }

    [Fact]
    public async Task Failed_backup_must_not_leave_partial_output()
    {
        var testDirectory = CreateTestDirectory();
        var sourcePath = Path.Combine(testDirectory, "corrupt.db");
        await File.WriteAllBytesAsync(sourcePath, [0x00, 0x01, 0x02, 0x03]);
        await using var context = CreateContext(sourcePath);
        var service = CreateService(context, sourcePath, new FixedClock(
            new DateTimeOffset(2026, 8, 9, 8, 30, 45, TimeSpan.Zero)));

        var destination = Path.Combine(testDirectory, "destination");
        Directory.CreateDirectory(destination);

        var result = await service.BackupAsync(new ManualBackupRequest(destination));

        Assert.False(result.IsSuccess);
        Assert.Empty(Directory.GetFiles(destination));
        await context.DisposeAsync();
        DeleteTestDirectory(testDirectory);
    }

    private static ManualBackupService CreateService(
        PosDbContext dbContext,
        string sourcePath,
        IClock clock,
        IBackupCoordinator? coordinator = null)
    {
        var options = Options.Create(new InfrastructureOptions
        {
            DatabasePath = sourcePath,
            DatabaseTimeoutSeconds = 1
        });

        return new ManualBackupService(options, dbContext, clock, coordinator ?? new BackupCoordinator());
    }

    private static async Task<PosDbContext> CreateSourceContextAsync(
        string testDirectory,
        string categoryName)
    {
        var databasePath = Path.Combine(testDirectory, "source.db");
        var context = CreateContext(databasePath);
        await context.Database.EnsureCreatedAsync();

        context.Categories.Add(new Category(categoryName, 1, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await context.Database.ExecuteSqlRawAsync("PRAGMA wal_autocheckpoint=0;");
        return context;
    }

    private static async Task<PosDbContext> CreateWalSourceContextAsync(
        string testDirectory,
        string categoryName)
    {
        var databasePath = Path.Combine(testDirectory, "wal-source.db");
        var context = CreateContext(databasePath);
        await context.Database.EnsureCreatedAsync();
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await context.Database.ExecuteSqlRawAsync("PRAGMA wal_autocheckpoint=0;");
        context.Categories.Add(new Category(categoryName, 1, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        return context;
    }

    private static PosDbContext CreateContext(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString();

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new PosDbContext(options);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string CreateTestDirectory()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "POS-Enterprise-ManualBackup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        return testDirectory;
    }

    private static void DeleteTestDirectory(string testDirectory)
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
