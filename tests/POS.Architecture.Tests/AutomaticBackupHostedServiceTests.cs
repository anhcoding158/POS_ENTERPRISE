using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Services;
using POS.Infrastructure.Support;
using POS.Wpf.Services;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AutomaticBackupHostedServiceTests
{
    [Fact]
    public void Production_timing_contract_is_exact()
    {
        var policy = AutomaticBackupPolicy.Production;
        Assert.Equal(TimeSpan.FromSeconds(60), policy.StartupDelay);
        Assert.Equal(TimeSpan.FromHours(24), policy.DueInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), policy.PollInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), policy.RetryInterval);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.ShutdownDrainTimeout);
    }

    [Fact]
    public async Task Missing_state_is_due_but_future_persisted_backoff_is_not_due()
    {
        var now = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
        var service = new CountingBackupService();
        var store = new FakeStore(new(AutomaticBackupStateReadStatus.Missing, null));
        var hosted = Create(service, store, now);
        await hosted.CheckDueAsync(CancellationToken.None);
        Assert.Equal(1, service.Count);

        store.Result = new(AutomaticBackupStateReadStatus.Valid, new AutomaticBackupState
        {
            LastAttemptUtc = now,
            LastResult = AutomaticBackupStatus.DeferredBusy,
            NextAttemptUtc = now.AddMinutes(30)
        });
        await hosted.CheckDueAsync(CancellationToken.None);
        Assert.Equal(1, service.Count);
    }

    [Fact]
    public async Task Verified_success_is_not_due_before_24_hours_and_is_due_at_boundary()
    {
        var success = new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);
        var state = new AutomaticBackupState
        {
            LastVerifiedSuccessUtc = success,
            LastVerifiedArtifact = "pos-enterprise-automatic-20260814-000000000.db",
            LastVerifiedByteLength = 1,
            LastVerifiedSha256 = new string('A', 64),
            LastAttemptUtc = success,
            LastResult = AutomaticBackupStatus.Success
        };
        var service = new CountingBackupService();
        var store = new FakeStore(new(AutomaticBackupStateReadStatus.Valid, state));
        await Create(service, store, success.AddHours(23).AddMinutes(59)).CheckDueAsync(default);
        Assert.Equal(0, service.Count);
        await Create(service, store, success.AddHours(24)).CheckDueAsync(default);
        Assert.Equal(1, service.Count);
    }

    [Fact]
    public async Task Cancellation_stops_host_while_waiting_for_database_ready()
    {
        var hosted = Create(new CountingBackupService(),
            new FakeStore(new(AutomaticBackupStateReadStatus.Missing, null)), DateTimeOffset.UtcNow);
        await hosted.StartAsync(default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await hosted.StopAsync(timeout.Token);
    }

    private static AutomaticBackupHostedService Create(CountingBackupService service,
        FakeStore store, DateTimeOffset now) => new(service, store, new AutomaticBackupStatusSource(),
            new FixedClock(now), TimeProvider.System, AutomaticBackupPolicy.Production,
            NullLogger<AutomaticBackupHostedService>.Instance);

    private sealed class CountingBackupService : IAutomaticBackupService
    {
        public int Count { get; private set; }
        public Task<AutomaticBackupResult> RunAsync(CancellationToken cancellationToken = default)
        { Count++; return Task.FromResult(new AutomaticBackupResult(AutomaticBackupStatus.Success, DateTimeOffset.UtcNow)); }
    }
    private sealed class FakeStore(AutomaticBackupStateReadResult result) : IAutomaticBackupStateStore
    {
        public AutomaticBackupStateReadResult Result { get; set; } = result;
        public Task<AutomaticBackupStateReadResult> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result);
        public Task WriteAsync(AutomaticBackupState state, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
}
