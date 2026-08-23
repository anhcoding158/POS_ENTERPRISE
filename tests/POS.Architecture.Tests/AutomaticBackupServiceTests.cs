using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Services;
using POS.Infrastructure.Support;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AutomaticBackupServiceTests
{
    [Fact]
    public async Task Manual_busy_maps_to_deferred_without_success_or_prune()
    {
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Path);
        var existing = Path.Combine(directory.Path, "pos-enterprise-automatic-20260801-000000000.db");
        await File.WriteAllBytesAsync(existing, [1]);
        var state = new CapturingStateStore();
        var service = Create(directory.Path,
            ManualBackupResult.Failure(ManualBackupStatus.Busy), state);

        var result = await service.RunAsync();

        Assert.Equal(AutomaticBackupStatus.DeferredBusy, result.Status);
        Assert.True(File.Exists(existing));
        Assert.All(state.Writes, item => Assert.Null(item.LastVerifiedSuccessUtc));
        Assert.All(state.Writes, item => Assert.Equal(AutomaticBackupStatus.DeferredBusy, item.LastResult));
    }

    [Fact]
    public async Task Verified_manual_result_is_promoted_to_owned_name_then_state_then_retention()
    {
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Path);
        var manualPath = Path.Combine(directory.Path, "POS-Enterprise-Backup-source.db");
        await File.WriteAllBytesAsync(manualPath, [1, 2, 3]);
        var now = new DateTimeOffset(2026, 8, 15, 1, 2, 3, TimeSpan.Zero);
        var state = new CapturingStateStore();
        var result = await Create(directory.Path,
            ManualBackupResult.Success(manualPath, 3, new string('A', 64), now), state, now).RunAsync();

        Assert.True(result.IsVerifiedSuccess);
        Assert.NotNull(result.ArtifactIdentifier);
        Assert.StartsWith(AutomaticBackupPathProvider.ArtifactPrefix, result.ArtifactIdentifier, StringComparison.Ordinal);
        Assert.False(File.Exists(manualPath));
        Assert.True(File.Exists(Path.Combine(directory.Path, result.ArtifactIdentifier!)));
        Assert.Contains(state.Writes, item => item.LastVerifiedSuccessUtc is not null &&
            item.LastVerifiedArtifact == result.ArtifactIdentifier);
    }

    [Fact]
    public async Task Automatic_service_creates_artifact_only_in_derived_isolated_root()
    {
        using var directory = new TempDirectory();
        var paths = AutomaticBackupPathProvider.CreateForRuntime(
            DatabaseRuntimeGuard.IsolatedTestMode,
            Path.Combine(directory.Path, "pos-enterprise-isolated.db"), AppContext.BaseDirectory);
        var services = new ServiceCollection();
        services.AddScoped<IManualBackupService>(_ => new DestinationWritingManualService());
        using var provider = services.BuildServiceProvider();
        var state = new CapturingStateStore();
        var policy = AutomaticBackupPolicy.Production;
        var service = new AutomaticBackupService(provider.GetRequiredService<IServiceScopeFactory>(), paths, state,
            new AutomaticBackupRetentionService(paths, policy), new AutomaticBackupStatusSource(),
            new FixedClock(new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero)), policy);

        var result = await service.RunAsync();

        Assert.True(result.IsVerifiedSuccess);
        Assert.NotNull(result.ArtifactIdentifier);
        Assert.True(File.Exists(Path.Combine(paths.Root, result.ArtifactIdentifier!)));
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    private static AutomaticBackupService Create(string root, ManualBackupResult result,
        CapturingStateStore state, DateTimeOffset? now = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<IManualBackupService>(_ => new FakeManualService(result));
        var provider = services.BuildServiceProvider();
        var paths = new AutomaticBackupPathProvider(root);
        var policy = AutomaticBackupPolicy.Production;
        return new AutomaticBackupService(provider.GetRequiredService<IServiceScopeFactory>(), paths, state,
            new AutomaticBackupRetentionService(paths, policy), new AutomaticBackupStatusSource(),
            new FixedClock(now ?? new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero)), policy);
    }

    private sealed class FakeManualService(ManualBackupResult result) : IManualBackupService
    {
        public Task<ManualBackupResult> BackupAsync(ManualBackupRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class DestinationWritingManualService : IManualBackupService
    {
        public async Task<ManualBackupResult> BackupAsync(ManualBackupRequest request,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(request.DestinationDirectory);
            var path = Path.Combine(request.DestinationDirectory, "pos-enterprise-pre-migration-isolated.db");
            await File.WriteAllBytesAsync(path, [1, 2, 3], cancellationToken);
            return ManualBackupResult.Success(path, 3, new string('A', 64),
                new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero));
        }
    }

    private sealed class CapturingStateStore : IAutomaticBackupStateStore
    {
        public List<AutomaticBackupState> Writes { get; } = [];
        public Task<AutomaticBackupStateReadResult> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AutomaticBackupStateReadResult(AutomaticBackupStateReadStatus.Missing, null));
        public Task WriteAsync(AutomaticBackupState state, CancellationToken cancellationToken = default)
        { Writes.Add(state); return Task.CompletedTask; }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "POS-AutoService-" + Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
