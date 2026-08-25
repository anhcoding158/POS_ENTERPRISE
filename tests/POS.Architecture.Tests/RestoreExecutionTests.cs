using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Services;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Support;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class RestoreExecutionTests
{
    [Fact]
    public async Task Current_and_older_artifacts_prepare_with_verified_safety_backup_and_private_stage()
    {
        using var fixture = await Fixture.CreateAsync();
        foreach (var candidate in new[] { fixture.CurrentCandidate, fixture.OlderCandidate })
        {
            var result = await fixture.PrepareAsync(candidate);
            Assert.True(result.IsPrepared);
            Assert.Equal(RestoreExecutionStatus.Success, result.Status);
            Assert.StartsWith("pos-enterprise-pre-restore-", result.SafetyBackupIdentifier,
                StringComparison.Ordinal);
            Assert.True(result.SafetyBackupByteLength > 0);
            Assert.Matches("^[0-9A-F]{64}$", result.SafetyBackupSha256Hex!);
            var plan = await fixture.ReadPlanAsync(result);
            Assert.Equal(RestoreOperationState.Prepared, plan.State);
            Assert.Equal(Path.GetDirectoryName(plan.ActiveDatabasePath),
                Directory.GetParent(fixture.Store.OperationsRoot)!.FullName);
            Assert.Equal(Path.GetPathRoot(plan.ActiveDatabasePath),
                Path.GetPathRoot(plan.StagedCandidatePath));
            Assert.True(File.Exists(plan.StagedCandidatePath));
            Assert.True(File.Exists(plan.SafetyBackupPath));
            Assert.Equal(plan.CandidateSha256Hex, Hash(plan.StagedCandidatePath));
            Assert.Equal("ok", await IntegrityAsync(plan.SafetyBackupPath));
        }
    }

    [Fact]
    public async Task Preparation_checkpoints_wal_before_committing_original_file_hash()
    {
        using var fixture = await Fixture.CreateAsync();
        await using var active = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = fixture.ActiveDatabase,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString());
        await active.OpenAsync();
        await using (var command = active.CreateCommand())
        {
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA user_version=334;";
            await command.ExecuteNonQueryAsync();
        }
        Assert.True(File.Exists(fixture.ActiveDatabase + "-wal"));

        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        Assert.True(prepared.IsPrepared);
        var plan = await fixture.ReadPlanAsync(prepared);
        await active.DisposeAsync();
        Assert.Equal(plan.OriginalDatabaseSha256Hex, Hash(fixture.ActiveDatabase));
        Assert.True(!File.Exists(fixture.ActiveDatabase + "-wal") ||
                    new FileInfo(fixture.ActiveDatabase + "-wal").Length == 0);
    }

    [Fact]
    public async Task Invalid_and_active_artifacts_are_rejected_before_safety_backup()
    {
        using var fixture = await Fixture.CreateAsync();
        var invalid = Path.Combine(fixture.Root, "invalid.db");
        await File.WriteAllTextAsync(invalid, "not sqlite");
        foreach (var path in new[] { invalid, fixture.ActiveDatabase })
        {
            var result = await fixture.PrepareAsync(path);
            Assert.Equal(RestoreExecutionStatus.ArtifactValidationFailed, result.Status);
            Assert.Empty(fixture.SafetyBackups());
            Assert.False(Directory.Exists(fixture.Store.OperationsRoot));
        }
    }

    [Fact]
    public async Task Busy_coordinator_and_precancellation_do_not_mutate_database_or_commit_plan()
    {
        using var fixture = await Fixture.CreateAsync();
        var before = Hash(fixture.ActiveDatabase);
        Assert.True(fixture.Coordinator.TryAcquire(out var held));
        await using (held!)
        {
            var busy = await fixture.PrepareAsync(fixture.CurrentCandidate);
            Assert.Equal(RestoreExecutionStatus.DatabaseBusy, busy.Status);
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await fixture.PrepareAsync(fixture.CurrentCandidate, cancellation.Token);
        Assert.Equal(RestoreExecutionStatus.Cancelled, cancelled.Status);
        Assert.Equal(before, Hash(fixture.ActiveDatabase));
        Assert.False(Directory.Exists(fixture.Store.OperationsRoot));
    }

    [Fact]
    public async Task Safety_backup_failure_keeps_active_database_and_commits_no_plan()
    {
        using var fixture = await Fixture.CreateAsync();
        var before = Hash(fixture.ActiveDatabase);
        RestorePreparationResult result;
        await using (var held = new FileStream(fixture.ActiveDatabase, FileMode.Open,
            FileAccess.ReadWrite, FileShare.None))
            result = await fixture.PrepareAsync(fixture.CurrentCandidate);
        Assert.Equal(RestoreExecutionStatus.PreRestoreBackupFailed, result.Status);
        Assert.Equal(before, Hash(fixture.ActiveDatabase));
        Assert.False(Directory.Exists(fixture.Store.OperationsRoot));
    }

    [Fact]
    public async Task Worker_rejects_wrong_token_operation_id_and_plan_path_without_mutation()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var before = Hash(fixture.ActiveDatabase);
        var worker = fixture.Worker(new FakeRuntime());
        Assert.Equal(RestoreExecutionStatus.InvalidOperationToken,
            (await worker.ExecuteAsync(prepared.OpaquePlanPath!, prepared.OperationId, "wrong")).Status);
        Assert.Equal(RestoreExecutionStatus.InvalidPlan,
            (await worker.ExecuteAsync(prepared.OpaquePlanPath!, Guid.NewGuid(),
                prepared.OneTimeOperationToken!)).Status);
        Assert.Equal(RestoreExecutionStatus.InvalidPlan,
            (await worker.ExecuteAsync(Path.Combine(fixture.Root, "..", "operation.json"),
                prepared.OperationId, prepared.OneTimeOperationToken!)).Status);
        Assert.Equal(before, Hash(fixture.ActiveDatabase));
    }

    [Fact]
    public async Task Cancellation_after_prepared_is_deferred_until_safe_terminal_state()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await fixture.Worker(new FakeRuntime()).ExecuteAsync(
            prepared.OpaquePlanPath!, prepared.OperationId,
            prepared.OneTimeOperationToken!, cancellation.Token);
        Assert.Equal(RestoreExecutionStatus.Success, result.Status);
        Assert.Equal(RestoreOperationState.Verified, result.OperationState);
    }

    [Fact]
    public async Task Plan_is_strict_token_is_random_and_raw_token_is_not_persisted()
    {
        using var fixture = await Fixture.CreateAsync();
        var first = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var second = await fixture.PrepareAsync(fixture.CurrentCandidate);
        Assert.NotEqual(first.OneTimeOperationToken, second.OneTimeOperationToken);
        Assert.True(Convert.FromBase64String(first.OneTimeOperationToken!).Length >= 32);
        var json = await File.ReadAllTextAsync(first.OpaquePlanPath!);
        Assert.DoesNotContain(first.OneTimeOperationToken!, json, StringComparison.Ordinal);
        Assert.Contains(RestoreOperationStore.HashToken(first.OneTimeOperationToken!), json,
            StringComparison.Ordinal);
        await Assert.ThrowsAsync<RestoreOperationStoreException>(() => fixture.Store.ReadAndValidateAsync(
            first.OpaquePlanPath!, first.OperationId, "wrong-token", CancellationToken.None));
        await Assert.ThrowsAsync<RestoreOperationStoreException>(() => fixture.Store.ReadAndValidateAsync(
            first.OpaquePlanPath!, Guid.NewGuid(), first.OneTimeOperationToken!, CancellationToken.None));
    }

    [Fact]
    public async Task Operation_store_round_trips_atomically_and_rejects_illegal_transition_and_corrupt_json()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var plan = await fixture.ReadPlanAsync(prepared);
        Assert.Equal(plan, await fixture.ReadPlanAsync(prepared));
        await Assert.ThrowsAsync<RestoreOperationStoreException>(() => fixture.Store.TransitionAsync(
            plan, RestoreOperationState.Verified, null, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(prepared.OpaquePlanPath!)!, "*.tmp"));
        await File.AppendAllTextAsync(prepared.OpaquePlanPath!, "{unknown}");
        await Assert.ThrowsAsync<RestoreOperationStoreException>(() => fixture.ReadPlanAsync(prepared));
    }

    [Fact]
    public async Task Worker_atomically_replaces_database_preserves_original_and_is_idempotent()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var plan = await fixture.ReadPlanAsync(prepared);
        var originalHash = Hash(fixture.ActiveDatabase);
        var worker = fixture.Worker(new FakeRuntime());
        var result = await worker.ExecuteAsync(prepared.OpaquePlanPath!, prepared.OperationId,
            prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.Success, result.Status);
        Assert.True(result.RestartRequired);
        Assert.Equal(plan.CandidateSha256Hex, Hash(fixture.ActiveDatabase));
        Assert.Equal(originalHash, Hash(plan.RollbackPath));
        Assert.True(File.Exists(plan.SafetyBackupPath));
        Assert.Equal(RestoreExecutionStatus.Success,
            (await worker.ExecuteAsync(prepared.OpaquePlanPath!, prepared.OperationId,
                prepared.OneTimeOperationToken!)).Status);
    }

    [Fact]
    public async Task Parent_identity_mismatch_and_timeout_cause_no_database_mutation_or_process_kill()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var plan = await fixture.ReadPlanAsync(prepared);
        var original = Hash(fixture.ActiveDatabase);
        var mismatchRuntime = new FakeRuntime
        {
            Identity = new(plan.ParentProcessStartTimeUtcTicks + 1, plan.ExpectedExecutablePath)
        };
        var mismatch = await fixture.Worker(mismatchRuntime).ExecuteAsync(
            prepared.OpaquePlanPath!, prepared.OperationId, prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.ParentProcessMismatch, mismatch.Status);
        Assert.Equal(original, Hash(fixture.ActiveDatabase));
        Assert.Equal(0, mismatchRuntime.ReplaceCalls);

        using var fixture2 = await Fixture.CreateAsync();
        var prepared2 = await fixture2.PrepareAsync(fixture2.CurrentCandidate);
        var plan2 = await fixture2.ReadPlanAsync(prepared2);
        var timeoutRuntime = new FakeRuntime
        {
            Identity = new(plan2.ParentProcessStartTimeUtcTicks, plan2.ExpectedExecutablePath),
            WaitResult = false
        };
        var timeout = await fixture2.Worker(timeoutRuntime).ExecuteAsync(
            prepared2.OpaquePlanPath!, prepared2.OperationId, prepared2.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.ParentExitTimeout, timeout.Status);
        Assert.Equal(0, timeoutRuntime.ReplaceCalls);
    }

    [Fact]
    public async Task Regular_wal_shm_are_cleaned_and_foreign_siblings_preserved()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var foreign = Path.Combine(Path.GetDirectoryName(fixture.ActiveDatabase)!, "foreign.txt");
        await File.WriteAllTextAsync(foreign, "sentinel");
        DeleteSidecars(fixture.ActiveDatabase);
        await File.WriteAllTextAsync(fixture.ActiveDatabase + "-wal", "stale");
        await File.WriteAllTextAsync(fixture.ActiveDatabase + "-shm", "stale");
        var result = await fixture.Worker(new FakeRuntime()).ExecuteAsync(
            prepared.OpaquePlanPath!, prepared.OperationId, prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.Success, result.Status);
        Assert.False(File.Exists(fixture.ActiveDatabase + "-wal"));
        Assert.False(File.Exists(fixture.ActiveDatabase + "-shm"));
        Assert.Equal("sentinel", await File.ReadAllTextAsync(foreign));
    }

    [Theory]
    [InlineData("-wal")]
    [InlineData("-shm")]
    public async Task Unsafe_sidecar_directory_is_rejected_and_preserved(string suffix)
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        DeleteSidecars(fixture.ActiveDatabase);
        Directory.CreateDirectory(fixture.ActiveDatabase + suffix);
        var result = await fixture.Worker(new FakeRuntime()).ExecuteAsync(
            prepared.OpaquePlanPath!, prepared.OperationId, prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.UnsafeSidecar, result.Status);
        Assert.True(Directory.Exists(fixture.ActiveDatabase + suffix));
    }

    [Fact]
    public async Task Locked_active_database_returns_busy_without_replacement()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        await using var held = new FileStream(fixture.ActiveDatabase, FileMode.Open,
            FileAccess.ReadWrite, FileShare.None);
        var runtime = new FakeRuntime();
        var result = await fixture.Worker(runtime).ExecuteAsync(
            prepared.OpaquePlanPath!, prepared.OperationId, prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.DatabaseBusy, result.Status);
        Assert.Equal(0, runtime.ReplaceCalls);
    }

    [Fact]
    public async Task Candidate_change_before_replacement_fails_closed()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var plan = await fixture.ReadPlanAsync(prepared);
        await using (var stream = new FileStream(plan.StagedCandidatePath, FileMode.Append,
            FileAccess.Write, FileShare.None))
            await stream.WriteAsync(new byte[] { 0x00 });
        var result = await fixture.Worker(new FakeRuntime()).ExecuteAsync(
            prepared.OpaquePlanPath!, prepared.OperationId, prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.SourceChanged, result.Status);
        Assert.Equal(plan.OriginalDatabaseSha256Hex, Hash(fixture.ActiveDatabase));
    }

    [Fact]
    public async Task Post_install_failure_rolls_back_exact_original_and_preserves_failed_candidate()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var plan = await fixture.ReadPlanAsync(prepared);
        var runtime = new FakeRuntime { ForcePostInstallVerificationFailure = true };
        var result = await fixture.Worker(runtime).ExecuteAsync(
            prepared.OpaquePlanPath!, prepared.OperationId, prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.RollbackSucceeded, result.Status);
        Assert.True(result.RollbackAttempted);
        Assert.True(result.RollbackCompleted);
        Assert.Equal(plan.OriginalDatabaseSha256Hex, Hash(fixture.ActiveDatabase));
        Assert.Equal(plan.CandidateSha256Hex, Hash(plan.FailedCandidatePath));
        Assert.True(File.Exists(plan.SafetyBackupPath));
        Assert.Equal(RestoreExecutionStatus.RollbackSucceeded,
            (await fixture.Worker(runtime).ExecuteAsync(prepared.OpaquePlanPath!,
                prepared.OperationId, prepared.OneTimeOperationToken!)).Status);
    }

    [Fact]
    public async Task Rollback_failure_is_durable_and_blocks_further_mutation()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var runtime = new FakeRuntime
        {
            ForcePostInstallVerificationFailure = true,
            FailReplaceCall = 2
        };
        var worker = fixture.Worker(runtime);
        var result = await worker.ExecuteAsync(prepared.OpaquePlanPath!, prepared.OperationId,
            prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.RollbackFailed, result.Status);
        var calls = runtime.ReplaceCalls;
        var resumed = await worker.ExecuteAsync(prepared.OpaquePlanPath!, prepared.OperationId,
            prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.RollbackFailed, resumed.Status);
        Assert.Equal(calls, runtime.ReplaceCalls);
        Assert.Equal(RestoreOperationState.RollbackFailed,
            (await fixture.ReadPlanAsync(prepared)).State);
    }

    [Fact]
    public async Task Replacement_failure_before_swap_keeps_original()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var plan = await fixture.ReadPlanAsync(prepared);
        var runtime = new FakeRuntime { FailReplaceCall = 1 };
        var result = await fixture.Worker(runtime).ExecuteAsync(
            prepared.OpaquePlanPath!, prepared.OperationId, prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.ReplacementFailed, result.Status);
        Assert.Equal(plan.OriginalDatabaseSha256Hex, Hash(fixture.ActiveDatabase));
        Assert.False(File.Exists(plan.RollbackPath));
    }

    [Fact]
    public async Task Replacement_started_after_swap_recovers_by_hash_and_verifies()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var plan = await fixture.ReadPlanAsync(prepared);
        plan = await fixture.Store.TransitionAsync(plan,
            RestoreOperationState.WaitingForParentExit, null, CancellationToken.None);
        plan = await fixture.Store.TransitionAsync(plan,
            RestoreOperationState.ReplacementStarted, null, CancellationToken.None);
        File.Replace(plan.StagedCandidatePath, plan.ActiveDatabasePath, plan.RollbackPath,
            ignoreMetadataErrors: true);
        var result = await fixture.Worker(new FakeRuntime()).ExecuteAsync(
            prepared.OpaquePlanPath!, prepared.OperationId, prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.Success, result.Status);
        Assert.Equal(RestoreOperationState.Verified, result.OperationState);
    }

    [Fact]
    public async Task Ambiguous_replacement_state_fails_closed()
    {
        using var fixture = await Fixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(fixture.CurrentCandidate);
        var plan = await fixture.ReadPlanAsync(prepared);
        plan = await fixture.Store.TransitionAsync(plan,
            RestoreOperationState.WaitingForParentExit, null, CancellationToken.None);
        await fixture.Store.TransitionAsync(plan,
            RestoreOperationState.ReplacementStarted, null, CancellationToken.None);
        File.Delete(plan.StagedCandidatePath);
        var result = await fixture.Worker(new FakeRuntime()).ExecuteAsync(
            prepared.OpaquePlanPath!, prepared.OperationId, prepared.OneTimeOperationToken!);
        Assert.Equal(RestoreExecutionStatus.RecoveryRequired, result.Status);
    }

    [Fact]
    public async Task Public_inspector_rejects_active_while_internal_mode_only_bypasses_that_conflict()
    {
        using var fixture = await Fixture.CreateAsync();
        Assert.Equal(RestoreArtifactStatus.ActiveDatabaseConflict,
            (await fixture.Inspector.InspectAsync(fixture.ActiveDatabase)).Status);
        Assert.True((await fixture.Inspector.InspectInternalAsync(fixture.ActiveDatabase,
            RestoreArtifactInspectionMode.WorkerActiveDatabaseVerification)).IsRestorable);
        var corrupt = Path.Combine(fixture.Root, "corrupt.db");
        await File.WriteAllTextAsync(corrupt, "bad");
        Assert.Equal(RestoreArtifactStatus.InvalidArtifact,
            (await fixture.Inspector.InspectInternalAsync(corrupt,
                RestoreArtifactInspectionMode.WorkerActiveDatabaseVerification)).Status);
    }

    [Fact]
    public async Task Production_di_validates_and_resolves_restore_services_without_filesystem_side_effects()
    {
        using var fixture = await Fixture.CreateAsync();
        var unused = Path.Combine(fixture.Root, "di", "not-created.db");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Infrastructure:DatabasePath"] = unused }).Build();
        var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.NotNull(provider.GetRequiredService<IRestorePreparationService>());
        Assert.NotNull(provider.GetRequiredService<RestoreOperationStore>());
        Assert.NotNull(provider.GetRequiredService<RestoreWorkerService>());
        Assert.False(File.Exists(unused));
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(unused)!, "restore-operations")));
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void DeleteSidecars(string path)
    {
        foreach (var sidecar in new[] { path + "-wal", path + "-shm" })
            if (File.Exists(sidecar)) File.Delete(sidecar);
    }

    private static async Task<string> IntegrityAsync(string path)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private, Pooling = false
        }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(string root)
        {
            Root = root;
            ActiveDatabase = Path.Combine(root, "database", "active.db");
            CurrentCandidate = Path.Combine(root, "sources", "current.db");
            OlderCandidate = Path.Combine(root, "sources", "older.db");
            var options = Microsoft.Extensions.Options.Options.Create(new InfrastructureOptions
            {
                DatabasePath = ActiveDatabase,
                DatabaseTimeoutSeconds = 1
            });
            var automaticPaths = new AutomaticBackupPathProvider(Path.Combine(root, "automatic-backups"));
            Inspector = new RestoreArtifactInspector(options,
                new AutomaticBackupStateStore(automaticPaths), automaticPaths);
            Coordinator = new BackupCoordinator();
            Store = new RestoreOperationStore(options);
            Preparation = new RestorePreparationService(options, Inspector, Inspector, Coordinator, Store);
            Options = options;
        }

        public string Root { get; }
        public string ActiveDatabase { get; }
        public string CurrentCandidate { get; }
        public string OlderCandidate { get; }
        public RestoreArtifactInspector Inspector { get; }
        public BackupCoordinator Coordinator { get; }
        public RestoreOperationStore Store { get; }
        public RestorePreparationService Preparation { get; }
        public IOptions<InfrastructureOptions> Options { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "POS-RestoreExecution-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fixture = new Fixture(root);
            await MigrateAsync(fixture.ActiveDatabase);
            await MigrateAsync(fixture.CurrentCandidate);
            await ExecuteAsync(fixture.CurrentCandidate, "PRAGMA user_version=333;");
            await using var older = Context(fixture.OlderCandidate);
            var firstMigration = older.Database.GetMigrations().First();
            await older.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
                .MigrateAsync(firstMigration);
            return fixture;
        }

        public RestoreWorkerService Worker(FakeRuntime runtime) =>
            new(Options, Store, Inspector, runtime, TimeSpan.FromMilliseconds(100));

        public Task<RestorePreparationResult> PrepareAsync(
            string path, CancellationToken cancellationToken = default)
        {
            using var process = Process.GetCurrentProcess();
            return Preparation.PrepareAsync(new(path, process.Id,
                new DateTimeOffset(process.StartTime.ToUniversalTime())), cancellationToken);
        }

        public Task<RestoreOperationPlan> ReadPlanAsync(RestorePreparationResult result) =>
            Store.ReadAndValidateAsync(result.OpaquePlanPath!, result.OperationId,
                result.OneTimeOperationToken!, CancellationToken.None);

        public string[] SafetyBackups()
        {
            var path = Path.Combine(Path.GetDirectoryName(ActiveDatabase)!, "backups", "pre-restore");
            return Directory.Exists(path) ? Directory.GetFiles(path) : [];
        }

        public void Dispose()
        {
            Coordinator.Dispose();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }

        private static async Task MigrateAsync(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var context = Context(path);
            await context.Database.MigrateAsync();
        }

        private static PosDbContext Context(string path) => new(
            new DbContextOptionsBuilder<PosDbContext>().UseSqlite(
                new SqliteConnectionStringBuilder
                {
                    DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Private, Pooling = false
                }.ToString()).Options);

        private static async Task ExecuteAsync(string path, string sql)
        {
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path, Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Private, Pooling = false
            }.ToString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class FakeRuntime : IRestoreWorkerRuntime
    {
        public RestoreProcessIdentity? Identity { get; init; }
        public bool WaitResult { get; init; } = true;
        public int FailReplaceCall { get; init; }
        public bool ForcePostInstallVerificationFailure { get; init; }
        public int ReplaceCalls { get; private set; }
        public RestoreProcessIdentity? GetProcessIdentity(int processId) => Identity;
        public Task<bool> WaitForExitAsync(int processId, TimeSpan timeout,
            CancellationToken cancellationToken) => Task.FromResult(WaitResult);
        public void Replace(string source, string destination, string backup)
        {
            ReplaceCalls++;
            if (ReplaceCalls == FailReplaceCall) throw new IOException("Injected replace failure.");
            File.Replace(source, destination, backup, ignoreMetadataErrors: true);
        }
    }
}
