namespace POS.Application.Abstractions.Services;

public sealed record AutomaticBackupPolicy
{
    public static AutomaticBackupPolicy Production { get; } = new();

    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan DueInterval { get; init; } = TimeSpan.FromHours(24);
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan ShutdownDrainTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public int RecentRetentionCount { get; init; } = 7;
    public int WeeklyRetentionCount { get; init; } = 4;
    public int MonthlyRetentionCount { get; init; } = 3;
    public long MaximumTotalBytes { get; init; } = 2_147_483_648L;
}
