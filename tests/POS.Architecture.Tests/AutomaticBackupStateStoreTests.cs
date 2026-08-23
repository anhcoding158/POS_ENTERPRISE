using System.Text.Json;
using POS.Application.Abstractions.Services;
using POS.Infrastructure.Support;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AutomaticBackupStateStoreTests
{
    [Fact]
    public async Task Missing_state_is_typed_and_not_success()
    {
        using var directory = new TempDirectory();
        var store = CreateStore(directory.Path);
        var result = await store.ReadAsync();
        Assert.Equal(AutomaticBackupStateReadStatus.Missing, result.Status);
        Assert.Null(result.State);
    }

    [Fact]
    public async Task Valid_state_round_trips_via_final_file_without_temp()
    {
        using var directory = new TempDirectory();
        var paths = new AutomaticBackupPathProvider(directory.Path);
        var store = new AutomaticBackupStateStore(paths);
        var state = ValidState();
        await store.WriteAsync(state);
        var result = await store.ReadAsync();
        Assert.Equal(AutomaticBackupStateReadStatus.Valid, result.Status);
        Assert.Equal(state, result.State);
        Assert.Single(Directory.GetFiles(directory.Path));
        Assert.DoesNotContain("secret", await File.ReadAllTextAsync(paths.StatePath), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("{bad")]
    [InlineData("{\"formatVersion\":99}")]
    [InlineData("{\"formatVersion\":1,\"lastVerifiedSuccessUtc\":\"2026-08-15T00:00:00+07:00\",\"lastVerifiedArtifact\":\"pos-enterprise-automatic-20260815-000000000.db\",\"lastVerifiedByteLength\":1,\"lastVerifiedSha256\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}")]
    [InlineData("{\"formatVersion\":1,\"lastVerifiedSuccessUtc\":\"2026-08-15T00:00:00Z\",\"lastVerifiedArtifact\":\"../escape.db\",\"lastVerifiedByteLength\":1,\"lastVerifiedSha256\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}")]
    [InlineData("{\"formatVersion\":1,\"lastVerifiedSuccessUtc\":\"2026-08-15T00:00:00Z\",\"lastVerifiedArtifact\":\"C:\\\\escape.db\",\"lastVerifiedByteLength\":0,\"lastVerifiedSha256\":\"BAD\"}")]
    public async Task Invalid_or_corrupt_state_fails_closed(string json)
    {
        using var directory = new TempDirectory();
        var paths = new AutomaticBackupPathProvider(directory.Path);
        Directory.CreateDirectory(directory.Path);
        await File.WriteAllTextAsync(paths.StatePath, json);
        var result = await new AutomaticBackupStateStore(paths).ReadAsync();
        Assert.NotEqual(AutomaticBackupStateReadStatus.Valid, result.Status);
        Assert.Null(result.State);
    }

    [Fact]
    public async Task Pre_cancelled_write_does_not_create_false_success_state()
    {
        using var directory = new TempDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateStore(directory.Path).WriteAsync(ValidState(), cancellation.Token));
        Assert.Empty(Directory.Exists(directory.Path) ? Directory.GetFiles(directory.Path) : []);
    }

    [Fact]
    public async Task State_store_writes_only_under_derived_isolated_root()
    {
        using var directory = new TempDirectory();
        var paths = AutomaticBackupPathProvider.CreateForRuntime(
            DatabaseRuntimeGuard.IsolatedTestMode,
            Path.Combine(directory.Path, "pos-enterprise-isolated.db"), AppContext.BaseDirectory);

        await new AutomaticBackupStateStore(paths).WriteAsync(ValidState());

        Assert.True(File.Exists(paths.StatePath));
        Assert.Equal(Path.Combine(directory.Path, AutomaticBackupPathProvider.RootDirectoryName), paths.Root);
        Assert.Single(Directory.GetFiles(paths.Root));
    }

    private static AutomaticBackupStateStore CreateStore(string root) =>
        new(new AutomaticBackupPathProvider(root));

    private static AutomaticBackupState ValidState() => new()
    {
        LastVerifiedSuccessUtc = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero),
        LastVerifiedArtifact = "pos-enterprise-automatic-20260815-000000000.db",
        LastVerifiedByteLength = 42,
        LastVerifiedSha256 = new string('A', 64),
        LastAttemptUtc = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero),
        LastResult = AutomaticBackupStatus.Success,
        NextAttemptUtc = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero)
    };

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "POS-AutoState-" + Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
