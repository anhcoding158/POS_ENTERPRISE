using POS.Application.Abstractions.Services;
using POS.Infrastructure.Support;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AutomaticBackupRetentionTests
{
    [Fact]
    public async Task Gfs_prunes_unprotected_owned_and_preserves_foreign_state_manual_and_newest()
    {
        using var directory = new TempDirectory();
        var paths = new AutomaticBackupPathProvider(directory.Path);
        Directory.CreateDirectory(directory.Path);
        var owned = Enumerable.Range(0, 16).Select(i =>
            $"pos-enterprise-automatic-202608{(i + 1):D2}-000000000.db").ToArray();
        foreach (var name in owned) await File.WriteAllBytesAsync(Path.Combine(directory.Path, name), [1]);
        foreach (var name in new[] { "manual.db", AutomaticBackupPathProvider.StateFileName, "foreign.txt" })
            await File.WriteAllTextAsync(Path.Combine(directory.Path, name), "keep");

        var result = await new AutomaticBackupRetentionService(paths, AutomaticBackupPolicy.Production)
            .PruneAsync(owned[^1]);

        Assert.False(result.HasWarning);
        Assert.Equal(10, Directory.GetFiles(directory.Path, "pos-enterprise-automatic-*.db").Length);
        Assert.False(File.Exists(Path.Combine(directory.Path, owned[0])));
        Assert.True(File.Exists(Path.Combine(directory.Path, owned[^1])));
        Assert.True(File.Exists(Path.Combine(directory.Path, "manual.db")));
        Assert.True(File.Exists(Path.Combine(directory.Path, AutomaticBackupPathProvider.StateFileName)));
        Assert.True(File.Exists(Path.Combine(directory.Path, "foreign.txt")));
    }

    [Fact]
    public async Task Quota_prunes_oldest_but_never_newest_even_when_newest_alone_exceeds_quota()
    {
        using var directory = new TempDirectory();
        var paths = new AutomaticBackupPathProvider(directory.Path);
        Directory.CreateDirectory(directory.Path);
        var old = "pos-enterprise-automatic-20260814-000000000.db";
        var newest = "pos-enterprise-automatic-20260815-000000000.db";
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, old), new byte[8]);
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, newest), new byte[16]);
        var policy = AutomaticBackupPolicy.Production with { MaximumTotalBytes = 10, RecentRetentionCount = 1, WeeklyRetentionCount = 0, MonthlyRetentionCount = 0 };

        var result = await new AutomaticBackupRetentionService(paths, policy).PruneAsync(newest);

        Assert.True(result.HasWarning);
        Assert.False(File.Exists(Path.Combine(directory.Path, old)));
        Assert.True(File.Exists(Path.Combine(directory.Path, newest)));
    }

    [Fact]
    public async Task Subdirectories_are_not_recursed_and_traversal_identifier_is_rejected()
    {
        using var directory = new TempDirectory();
        var paths = new AutomaticBackupPathProvider(directory.Path);
        var child = Directory.CreateDirectory(Path.Combine(directory.Path, "child"));
        var nested = Path.Combine(child.FullName, "pos-enterprise-automatic-20260801-000000000.db");
        await File.WriteAllBytesAsync(nested, [1]);
        var result = await new AutomaticBackupRetentionService(paths,
            AutomaticBackupPolicy.Production with { RecentRetentionCount = 0, WeeklyRetentionCount = 0, MonthlyRetentionCount = 0 }).PruneAsync("../escape.db");
        Assert.True(result.HasWarning);
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public async Task Retention_enumerates_only_the_derived_isolated_root()
    {
        using var directory = new TempDirectory();
        var paths = AutomaticBackupPathProvider.CreateForRuntime(
            DatabaseRuntimeGuard.IsolatedTestMode,
            Path.Combine(directory.Path, "pos-enterprise-isolated.db"), AppContext.BaseDirectory);
        Directory.CreateDirectory(paths.Root);
        var protectedName = "pos-enterprise-automatic-20260815-000000000.db";
        var outsideName = "pos-enterprise-automatic-20260801-000000000.db";
        await File.WriteAllBytesAsync(Path.Combine(paths.Root, protectedName), [1]);
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, outsideName), [1]);

        await new AutomaticBackupRetentionService(paths,
            AutomaticBackupPolicy.Production with { RecentRetentionCount = 0, WeeklyRetentionCount = 0, MonthlyRetentionCount = 0 }).PruneAsync(protectedName);

        Assert.True(File.Exists(Path.Combine(paths.Root, protectedName)));
        Assert.True(File.Exists(Path.Combine(directory.Path, outsideName)));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "POS-AutoRetention-" + Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
