using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Services;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Support;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class RestoreArtifactInspectorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative.db")]
    public async Task Blank_or_relative_path_is_invalid(string? path)
    {
        using var fixture = new Fixture();
        Assert.Equal(RestoreArtifactStatus.InvalidPath,
            (await fixture.Inspector().InspectAsync(path)).Status);
    }

    [Fact]
    public async Task Raw_traversal_is_invalid()
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Root, "child", "..", "artifact.db");
        Assert.Equal(RestoreArtifactStatus.InvalidPath,
            (await fixture.Inspector().InspectAsync(path)).Status);
    }

    [Fact]
    public async Task Missing_wrong_extension_and_directory_are_rejected()
    {
        using var fixture = new Fixture();
        var missing = Path.Combine(fixture.Root, "missing.db");
        var wrong = Path.Combine(fixture.Root, "artifact.txt");
        await File.WriteAllTextAsync(wrong, "x");
        var directory = Path.Combine(fixture.Root, "directory.db");
        Directory.CreateDirectory(directory);

        Assert.Equal(RestoreArtifactStatus.SourceUnavailable,
            (await fixture.Inspector().InspectAsync(missing)).Status);
        Assert.Equal(RestoreArtifactStatus.InvalidArtifact,
            (await fixture.Inspector().InspectAsync(wrong)).Status);
        Assert.Equal(RestoreArtifactStatus.InvalidArtifact,
            (await fixture.Inspector().InspectAsync(directory)).Status);
    }

    [Fact]
    public async Task Unc_path_is_rejected_before_file_access()
    {
        using var fixture = new Fixture();
        Assert.Equal(RestoreArtifactStatus.NetworkPathUnsupported,
            (await fixture.Inspector().InspectAsync("\\\\server\\share\\artifact.db")).Status);
    }

    [Fact]
    public async Task Active_database_conflict_is_case_insensitive()
    {
        using var fixture = new Fixture();
        var upper = fixture.ActiveDatabase.ToUpperInvariant();
        Assert.Equal(RestoreArtifactStatus.ActiveDatabaseConflict,
            (await fixture.Inspector().InspectAsync(upper)).Status);
    }

    [Fact]
    public async Task File_and_ancestor_reparse_points_are_rejected()
    {
        using var fixture = new Fixture();
        var targetDirectory = Path.Combine(fixture.Root, "target-directory");
        Directory.CreateDirectory(targetDirectory);
        await fixture.CreateCurrentAsync(Path.Combine("target-directory", "target.db"));
        var link = Path.Combine(fixture.Root, "link.db");
        CreateJunction(link, targetDirectory);
        var realDirectory = Path.Combine(fixture.Root, "real");
        Directory.CreateDirectory(realDirectory);
        var nested = await fixture.CreateCurrentAsync(Path.Combine("real", "nested.db"));
        var linkedDirectory = Path.Combine(fixture.Root, "linked");
        CreateJunction(linkedDirectory, realDirectory);
        try
        {
            Assert.Equal(RestoreArtifactStatus.UnsafeReparsePath,
                (await fixture.Inspector().InspectAsync(link)).Status);
            Assert.Equal(RestoreArtifactStatus.UnsafeReparsePath,
                (await fixture.Inspector().InspectAsync(Path.Combine(linkedDirectory, Path.GetFileName(nested)))).Status);
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
            if (Directory.Exists(linkedDirectory)) Directory.Delete(linkedDirectory);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not sqlite")]
    public async Task Empty_or_invalid_header_is_invalid_artifact(string content)
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Root, "invalid.db");
        await File.WriteAllTextAsync(path, content);
        Assert.Equal(RestoreArtifactStatus.InvalidArtifact,
            (await fixture.Inspector().InspectAsync(path)).Status);
    }

    [Fact]
    public async Task Truncated_database_is_rejected()
    {
        using var fixture = new Fixture();
        var path = await fixture.CreateCurrentAsync("truncated.db");
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            stream.SetLength(100);
        Assert.Contains((await fixture.Inspector().InspectAsync(path)).Status,
            new[] { RestoreArtifactStatus.InvalidArtifact, RestoreArtifactStatus.IntegrityCheckFailed });
    }

    [Fact]
    public async Task Exclusively_locked_source_is_typed_source_locked()
    {
        using var fixture = new Fixture();
        var path = await fixture.CreateCurrentAsync("locked.db");
        await using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(RestoreArtifactStatus.SourceLocked,
            (await fixture.Inspector().InspectAsync(path)).Status);
    }

    [Fact]
    public async Task Full_integrity_failure_is_typed()
    {
        using var fixture = new Fixture();
        var path = await fixture.CreateCurrentAsync("integrity.db");
        await ExecuteAsync(path, "PRAGMA writable_schema=ON; UPDATE sqlite_master SET rootpage=999999 WHERE name='Products';");
        Assert.Equal(RestoreArtifactStatus.IntegrityCheckFailed,
            (await fixture.Inspector().InspectAsync(path)).Status);
    }

    [Fact]
    public async Task Current_delete_journal_database_is_valid_legacy_and_creates_no_sidecars()
    {
        using var fixture = new Fixture();
        var path = await fixture.CreateCurrentAsync("pos-enterprise-pre-migration-current.db");
        await ExecuteAsync(path, "PRAGMA journal_mode=DELETE;");
        var result = await fixture.Inspector().InspectAsync(path);
        Assert.Equal(RestoreArtifactStatus.ValidLegacyUnattested, result.Status);
        Assert.Equal(RestoreArtifactKind.Manual, result.ArtifactKind);
        Assert.Equal(RestoreSchemaCompatibility.Current, result.SchemaCompatibility);
        Assert.True(result.IsRestorable);
        Assert.True(result.ByteLength > 0);
        Assert.Matches("^[0-9A-F]{64}$", result.Sha256Hex!);
        Assert.False(File.Exists(path + "-wal"));
        Assert.False(File.Exists(path + "-shm"));
        Assert.False(File.Exists(path + "-journal"));
    }

    [Fact]
    public async Task Precancelled_inspection_is_cancelled()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(RestoreArtifactStatus.Cancelled,
            (await fixture.Inspector().InspectAsync(Path.Combine(fixture.Root, "missing.db"), cancellation.Token)).Status);
    }

    [Fact]
    public async Task Current_and_older_prefix_are_compatible()
    {
        using var fixture = new Fixture();
        var current = await fixture.CreateCurrentAsync("current.db");
        var older = await fixture.CreateOlderAsync("older.db");
        var currentResult = await fixture.Inspector().InspectAsync(current);
        var olderResult = await fixture.Inspector().InspectAsync(older);
        Assert.Equal(RestoreSchemaCompatibility.Current, currentResult.SchemaCompatibility);
        Assert.Equal(RestoreSchemaCompatibility.OlderCompatible, olderResult.SchemaCompatibility);
        Assert.True(currentResult.IsRestorable);
        Assert.True(olderResult.IsRestorable);
        Assert.True(olderResult.AppliedMigrationCount < currentResult.AppliedMigrationCount);
    }

    [Fact]
    public async Task Newer_unknown_gap_and_duplicate_histories_fail_closed()
    {
        using var fixture = new Fixture();
        var newer = await fixture.CreateCurrentAsync("newer.db");
        await ExecuteAsync(newer, "INSERT INTO __EFMigrationsHistory VALUES ('99999999999999_Future','10.0.0');");
        Assert.Equal(RestoreArtifactStatus.UnsupportedNewerSchema,
            (await fixture.Inspector().InspectAsync(newer)).Status);

        var unknown = await fixture.CreateCurrentAsync("unknown.db");
        await ExecuteAsync(unknown, "UPDATE __EFMigrationsHistory SET MigrationId='20000101000000_Foreign' WHERE rowid=2;");
        Assert.Equal(RestoreArtifactStatus.UnknownMigrationHistory,
            (await fixture.Inspector().InspectAsync(unknown)).Status);

        var gap = await fixture.CreateCurrentAsync("gap.db");
        await ExecuteAsync(gap, "DELETE FROM __EFMigrationsHistory WHERE rowid=2;");
        Assert.Equal(RestoreArtifactStatus.UnknownMigrationHistory,
            (await fixture.Inspector().InspectAsync(gap)).Status);

        var duplicate = await fixture.CreateCurrentAsync("duplicate.db");
        await ExecuteAsync(duplicate, "ALTER TABLE __EFMigrationsHistory RENAME TO old_history; CREATE TABLE __EFMigrationsHistory(MigrationId TEXT, ProductVersion TEXT); INSERT INTO __EFMigrationsHistory SELECT * FROM old_history; INSERT INTO __EFMigrationsHistory SELECT * FROM old_history LIMIT 1;");
        Assert.Equal(RestoreArtifactStatus.UnknownMigrationHistory,
            (await fixture.Inspector().InspectAsync(duplicate)).Status);
    }

    [Fact]
    public async Task Missing_history_foreign_database_and_missing_fingerprint_fail_closed()
    {
        using var fixture = new Fixture();
        var missing = Path.Combine(fixture.Root, "missing-history.db");
        await ExecuteAsync(missing, "CREATE TABLE Categories(Id INTEGER); CREATE TABLE Products(Id INTEGER);");
        Assert.Equal(RestoreArtifactStatus.MissingMigrationHistory,
            (await fixture.Inspector().InspectAsync(missing)).Status);

        var foreign = Path.Combine(fixture.Root, "foreign.db");
        await ExecuteAsync(foreign, "CREATE TABLE ForeignData(Id INTEGER); CREATE TABLE __EFMigrationsHistory(MigrationId TEXT PRIMARY KEY, ProductVersion TEXT); INSERT INTO __EFMigrationsHistory VALUES ('20260719062115_InitialProductCatalog','10.0.0');");
        Assert.Equal(RestoreArtifactStatus.InvalidArtifact,
            (await fixture.Inspector().InspectAsync(foreign)).Status);

        var noFingerprint = await fixture.CreateCurrentAsync("no-fingerprint.db");
        await ExecuteAsync(noFingerprint, "PRAGMA foreign_keys=OFF; DROP TABLE Products;");
        Assert.Equal(RestoreArtifactStatus.InvalidArtifact,
            (await fixture.Inspector().InspectAsync(noFingerprint)).Status);
    }

    [Fact]
    public async Task Automatic_latest_matching_state_is_attested()
    {
        using var fixture = new Fixture();
        var path = await fixture.CreateCurrentAsync(Path.Combine("automatic-backups", "pos-enterprise-automatic-20260823-120000000.db"));
        await fixture.WriteStateAsync(path);
        var result = await fixture.Inspector().InspectAsync(path);
        Assert.Equal(RestoreArtifactStatus.Valid, result.Status);
        Assert.Equal(RestoreArtifactProvenance.AutomaticStateAttested, result.Provenance);
    }

    [Fact]
    public async Task Automatic_artifact_that_is_not_latest_is_legacy_unattested()
    {
        using var fixture = new Fixture();
        var latest = await fixture.CreateCurrentAsync(Path.Combine("automatic-backups", "pos-enterprise-automatic-20260823-120000010.db"));
        var older = await fixture.CreateCurrentAsync(Path.Combine("automatic-backups", "pos-enterprise-automatic-20260822-120000010.db"));
        await fixture.WriteStateAsync(latest);
        var result = await fixture.Inspector().InspectAsync(older);
        Assert.Equal(RestoreArtifactStatus.ValidLegacyUnattested, result.Status);
        Assert.Equal(RestoreArtifactProvenance.LegacyUnattested, result.Provenance);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Automatic_latest_state_length_or_hash_mismatch_is_checksum_mismatch(bool lengthMismatch)
    {
        using var fixture = new Fixture();
        var path = await fixture.CreateCurrentAsync(Path.Combine("automatic-backups", "pos-enterprise-automatic-20260823-120000001.db"));
        await fixture.WriteStateAsync(path, lengthMismatch ? 1 : 0, lengthMismatch ? null : new string('A', 64));
        Assert.Equal(RestoreArtifactStatus.ChecksumMismatch,
            (await fixture.Inspector().InspectAsync(path)).Status);
    }

    [Theory]
    [InlineData("pos-enterprise-automatic-20260823-120000002.db", RestoreArtifactKind.Automatic)]
    [InlineData("pos-enterprise-pre-migration-20260823-120000002.db", RestoreArtifactKind.Manual)]
    [InlineData("pos-enterprise-pre-restore-20260823-120000002.db", RestoreArtifactKind.PreRestore)]
    [InlineData("legacy.db", RestoreArtifactKind.LegacyOrUnknown)]
    public async Task Valid_unattested_artifacts_are_legacy_with_filename_only_kind(
        string name, RestoreArtifactKind kind)
    {
        using var fixture = new Fixture();
        var path = await fixture.CreateCurrentAsync(name);
        var result = await fixture.Inspector().InspectAsync(path);
        Assert.Equal(RestoreArtifactStatus.ValidLegacyUnattested, result.Status);
        Assert.Equal(RestoreArtifactProvenance.LegacyUnattested, result.Provenance);
        Assert.Equal(kind, result.ArtifactKind);
    }

    [Fact]
    public async Task Pos_filename_does_not_make_foreign_payload_valid()
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Root, "pos-enterprise-automatic-20260823-120000003.db");
        await File.WriteAllTextAsync(path, "foreign");
        Assert.Equal(RestoreArtifactStatus.InvalidArtifact,
            (await fixture.Inspector().InspectAsync(path)).Status);
    }

    [Fact]
    public async Task Source_change_during_state_observation_is_detected()
    {
        using var fixture = new Fixture();
        var path = await fixture.CreateCurrentAsync(Path.Combine("automatic-backups", "pos-enterprise-automatic-20260823-120000004.db"));
        var state = Fixture.StateFor(path);
        var blocking = new BlockingStateStore(state);
        var task = fixture.Inspector(blocking).InspectAsync(path);
        await blocking.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            await stream.WriteAsync(new byte[] { 0 });
        blocking.Release.TrySetResult(true);
        Assert.Equal(RestoreArtifactStatus.SourceChangedDuringInspection,
            (await task.WaitAsync(TimeSpan.FromSeconds(20))).Status);
    }

    [Fact]
    public void Production_di_resolves_inspector_and_application_contract_is_clean()
    {
        using var fixture = new Fixture();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Infrastructure:DatabasePath"] = fixture.ActiveDatabase }).Build();
        var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.Same(provider.GetRequiredService<IRestoreArtifactInspector>(),
            provider.GetRequiredService<IRestoreArtifactInspector>());
        var references = typeof(IRestoreArtifactInspector).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("POS.Infrastructure", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("PresentationFramework", references);
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(),
            "src", "POS.Infrastructure", "Support", "RestoreArtifactInspector.cs"));
        Assert.DoesNotContain("HasPendingModelChanges", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "POS.Enterprise.slnx"))) return current.FullName;
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static async Task ExecuteAsync(string path, string sql)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private, Pooling = false
        }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static void CreateJunction(string link, string target)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "POS-RestoreInspector-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            ActiveDatabase = Path.Combine(Root, "active", "active.db");
            AutomaticRoot = Path.Combine(Root, "automatic-backups");
            Directory.CreateDirectory(AutomaticRoot);
        }

        public string Root { get; }
        public string ActiveDatabase { get; }
        public string AutomaticRoot { get; }

        public RestoreArtifactInspector Inspector(IAutomaticBackupStateStore? state = null)
        {
            var paths = new AutomaticBackupPathProvider(AutomaticRoot);
            return new RestoreArtifactInspector(Options.Create(new InfrastructureOptions
            {
                DatabasePath = ActiveDatabase,
                DatabaseTimeoutSeconds = 1
            }), state ?? new AutomaticBackupStateStore(paths), paths);
        }

        public async Task<string> CreateCurrentAsync(string relative)
        {
            var path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var context = Context(path);
            await context.Database.MigrateAsync();
            return path;
        }

        public async Task<string> CreateOlderAsync(string relative)
        {
            var path = Path.Combine(Root, relative);
            await using var context = Context(path);
            var first = context.Database.GetMigrations().First();
            await context.GetService<IMigrator>().MigrateAsync(first);
            return path;
        }

        public async Task WriteStateAsync(string path, long lengthDelta = 0, string? hash = null)
        {
            var store = new AutomaticBackupStateStore(new AutomaticBackupPathProvider(AutomaticRoot));
            await store.WriteAsync(StateFor(path) with
            {
                LastVerifiedByteLength = new FileInfo(path).Length + lengthDelta,
                LastVerifiedSha256 = hash ?? Hash(path)
            });
        }

        public static AutomaticBackupState StateFor(string path) => new()
        {
            LastVerifiedSuccessUtc = DateTimeOffset.UtcNow,
            LastVerifiedArtifact = Path.GetFileName(path),
            LastVerifiedByteLength = new FileInfo(path).Length,
            LastVerifiedSha256 = Hash(path),
            LastAttemptUtc = DateTimeOffset.UtcNow,
            LastResult = AutomaticBackupStatus.Success,
            NextAttemptUtc = DateTimeOffset.UtcNow.AddDays(1)
        };

        private static PosDbContext Context(string path) => new(
            new DbContextOptionsBuilder<PosDbContext>().UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private, Pooling = false
            }.ToString()).Options);

        private static string Hash(string path) =>
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class BlockingStateStore(AutomaticBackupState state) : IAutomaticBackupStateStore
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<AutomaticBackupStateReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            return new(AutomaticBackupStateReadStatus.Valid, state);
        }
        public Task WriteAsync(AutomaticBackupState value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
