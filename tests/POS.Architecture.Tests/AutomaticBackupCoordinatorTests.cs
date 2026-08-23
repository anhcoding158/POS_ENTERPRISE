using POS.Application.Abstractions.Services;
using POS.Infrastructure.Support;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AutomaticBackupCoordinatorTests
{
    [Fact]
    public void Gate_is_non_blocking_and_only_one_owner_exists()
    {
        using var coordinator = new BackupCoordinator();
        Assert.True(coordinator.TryAcquire(out var first));
        Assert.NotNull(first);
        Assert.False(coordinator.TryAcquire(out var second));
        Assert.Null(second);
        first!.Dispose();
        Assert.True(coordinator.TryAcquire(out var next));
        next!.Dispose();
    }

    [Fact]
    public async Task Lease_disposal_is_idempotent_and_releases_after_async_disposal()
    {
        using var coordinator = new BackupCoordinator();
        Assert.True(coordinator.TryAcquire(out var lease));
        await lease!.DisposeAsync();
        lease.Dispose();
        Assert.True(coordinator.TryAcquire(out var next));
        next!.Dispose();
    }

    [Fact]
    public void Coordinator_has_no_scoped_or_orchestration_dependencies()
    {
        Assert.Empty(typeof(BackupCoordinator).GetConstructors().Single().GetParameters());
        Assert.DoesNotContain(typeof(IManualBackupService), typeof(BackupCoordinator).GetInterfaces());
        Assert.DoesNotContain(typeof(IAutomaticBackupService), typeof(BackupCoordinator).GetInterfaces());
    }
}
