using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Services;

namespace POS.Wpf.Services;

public sealed class AutomaticBackupHostedService(
    IAutomaticBackupService backupService,
    IAutomaticBackupStateStore stateStore,
    IAutomaticBackupStatusSource statusSource,
    IClock clock,
    TimeProvider timeProvider,
    AutomaticBackupPolicy policy,
    ILogger<AutomaticBackupHostedService> logger) : IHostedService
{
    private static readonly Action<ILogger, Exception?> DrainWarning =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3201, "AutomaticBackupDrainTimeout"),
            "Automatic backup shutdown drain ended before completion.");
    private static readonly Action<ILogger, string, Exception?> SchedulerFailure =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3202, "AutomaticBackupSchedulerFailure"),
            "Automatic backup scheduler stopped after an unexpected {FailureType}.");
    private readonly TaskCompletionSource _databaseReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _stopping;
    private Task? _worker;

    public void MarkDatabaseInitialized() => _databaseReady.TrySetResult();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _worker = RunAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var stopping = _stopping;
        var worker = _worker;
        if (stopping is null || worker is null) return;
        stopping.Cancel();
        try
        {
            await worker.WaitAsync(policy.ShutdownDrainTimeout, cancellationToken);
        }
        catch (Exception exception) when (exception is OperationCanceledException or TimeoutException)
        {
            DrainWarning(logger, null);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _databaseReady.Task.WaitAsync(cancellationToken);
            await Task.Delay(policy.StartupDelay, timeProvider, cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                await CheckDueAsync(cancellationToken);
                await Task.Delay(policy.PollInterval, timeProvider, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            SchedulerFailure(logger, exception.GetType().Name, null);
            statusSource.Publish(new(AutomaticBackupStatus.Failed));
        }
    }

    internal async Task CheckDueAsync(CancellationToken cancellationToken)
    {
        var read = await stateStore.ReadAsync(cancellationToken);
        if (read.Status is AutomaticBackupStateReadStatus.Corrupt or AutomaticBackupStateReadStatus.UnsupportedVersion)
            statusSource.Publish(new(AutomaticBackupStatus.StateCorrupt));
        else if (read.Status == AutomaticBackupStateReadStatus.Missing)
            statusSource.Publish(new(AutomaticBackupStatus.StateMissing));

        var state = read.State;
        var now = clock.UtcNow.ToUniversalTime();
        var dueAt = state?.NextAttemptUtc ?? state?.LastVerifiedSuccessUtc?.Add(policy.DueInterval);
        if (dueAt is not null && now < dueAt.Value)
        {
            statusSource.Publish(new(AutomaticBackupStatus.NotDue, state?.LastVerifiedSuccessUtc,
                state?.LastVerifiedArtifact, state?.LastRetentionWarning));
            return;
        }
        await backupService.RunAsync(cancellationToken);
    }
}
