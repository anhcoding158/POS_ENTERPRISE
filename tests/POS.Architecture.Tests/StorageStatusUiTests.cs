using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.Services;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class StorageStatusUiTests
{
    [Theory]
    [InlineData(StoragePreflightStatus.Allowed, StorageStatusUiState.Allowed, "Dung lượng an toàn")]
    [InlineData(StoragePreflightStatus.AllowedWithWarning, StorageStatusUiState.AllowedWithWarning, "Dung lượng trống đang thấp")]
    [InlineData(StoragePreflightStatus.Insufficient, StorageStatusUiState.Insufficient, "Không đủ dung lượng an toàn")]
    [InlineData(StoragePreflightStatus.MetricsUnavailable, StorageStatusUiState.MetricsUnavailable, "Chưa đọc được trạng thái dung lượng")]
    public async Task Typed_status_has_safe_presentation(
        StoragePreflightStatus status, StorageStatusUiState expected, string text)
    {
        var monitor = new FakeMonitor(status);
        using var viewModel = ViewModel(monitor);
        await viewModel.RefreshAsync();
        Assert.Equal(expected, viewModel.State);
        Assert.Equal(text, viewModel.StatusText);
        Assert.Equal(1, monitor.SnapshotCalls);
        Assert.Equal(1, monitor.EvaluateCalls);
        Assert.Equal(0, monitor.LastRequest!.RequiredAdditionalBytes);
    }

    [Fact]
    public async Task Unknown_status_is_retryable_unavailable_not_healthy()
    {
        using var viewModel = ViewModel(new FakeMonitor((StoragePreflightStatus)999));
        await viewModel.RefreshAsync();
        Assert.Equal(StorageStatusUiState.MetricsUnavailable, viewModel.State);
        Assert.DoesNotContain("an toàn", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.CanRefresh);
    }

    [Fact]
    public async Task Initial_loading_single_flight_and_metrics_are_distinct()
    {
        var completion = new TaskCompletionSource<DatabaseStorageSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var monitor = new FakeMonitor(StoragePreflightStatus.Allowed) { Completion = completion };
        using var viewModel = ViewModel(monitor);
        Assert.Equal(StorageStatusUiState.NotChecked, viewModel.State);
        Assert.Equal("Chưa có dữ liệu", viewModel.MainDatabaseSizeText);

        var first = viewModel.RefreshAsync();
        var second = viewModel.RefreshAsync();
        Assert.Equal(StorageStatusUiState.Loading, viewModel.State);
        Assert.False(viewModel.CanRefresh);
        Assert.Equal(1, monitor.SnapshotCalls);
        completion.SetResult(FakeMonitor.Snapshot);
        await Task.WhenAll(first, second);

        Assert.Equal("1 KB", viewModel.MainDatabaseSizeText);
        Assert.Equal("3 KB", viewModel.SqliteFootprintText);
        Assert.Equal("8 GB", viewModel.AvailableFreeText);
        Assert.Equal("08/08/2026 00:00 UTC", viewModel.LastCheckedText);
        Assert.True(viewModel.CanRefresh);
    }

    [Fact]
    public async Task Failure_is_safe_retryable_and_has_no_raw_detail()
    {
        const string canary = @"Data Source=C:\Users\private\store.db;Password=secret";
        var monitor = new FakeMonitor(StoragePreflightStatus.Allowed)
            { Exception = new IOException(canary) };
        using var viewModel = ViewModel(monitor);
        await viewModel.RefreshAsync();
        Assert.Equal(StorageStatusUiState.MetricsUnavailable, viewModel.State);
        Assert.True(viewModel.CanRefresh);
        Assert.DoesNotContain(canary, viewModel.StatusText + viewModel.GuidanceText,
            StringComparison.Ordinal);
        Assert.Equal("Không có dữ liệu", viewModel.MainDatabaseSizeText);
    }

    [Fact]
    public async Task Cancellation_is_propagated_and_does_not_create_false_failure()
    {
        var completion = new TaskCompletionSource<DatabaseStorageSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var monitor = new FakeMonitor(StoragePreflightStatus.Allowed) { Completion = completion };
        using var viewModel = ViewModel(monitor);
        var refresh = viewModel.RefreshAsync();
        viewModel.CancelRefresh();
        completion.SetCanceled(monitor.LastToken);
        await refresh;
        Assert.Equal(StorageStatusUiState.NotChecked, viewModel.State);
        Assert.True(monitor.LastToken.IsCancellationRequested);
        Assert.Equal(0, monitor.EvaluateCalls);
    }

    [Theory]
    [InlineData(null, "Không có dữ liệu")]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(1073741824L, "1 GB")]
    [InlineData(1099511627776L, "1 TB")]
    [InlineData(long.MaxValue, "8388608 TB")]
    public void Byte_formatting_is_deterministic_and_overflow_safe(long? value, string expected) =>
        Assert.Equal(expected, StorageStatusViewModel.FormatBytes(value));

    [Fact]
    public void Xaml_and_shell_have_required_safe_surface()
    {
        var root = RepositoryLocator.Root;
        var xaml = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "StorageStatusWindow.xaml"));
        var shell = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "ShellWindow.xaml"));
        Assert.Contains("Dung lượng database chính", xaml, StringComparison.Ordinal);
        Assert.Contains("Tổng dung lượng SQLite đang sử dụng", xaml, StringComparison.Ordinal);
        Assert.Contains("RefreshCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("Đóng", xaml, StringComparison.Ordinal);
        Assert.Contains("PageTitleTextStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("StorageStatusNavigationButton", shell, StringComparison.Ordinal);
        foreach (var forbidden in new[]
        {
            "DatabasePath", "ConnectionString", "IncludeDatabase", "Backup",
            "Restore", "Cleanup", "Delete", "HttpClient", "SendAsync"
        })
            Assert.DoesNotContain(forbidden, xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Composition_uses_authenticated_owner_modal_and_exact_di_registrations()
    {
        var root = RepositoryLocator.Root;
        var service = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Services", "StorageStatusDialogService.cs"));
        Assert.Contains("_currentUserService.IsAuthenticated", service, StringComparison.Ordinal);
        Assert.Contains("window.Owner = owner", service, StringComparison.Ordinal);
        Assert.Contains("_isOpen", service, StringComparison.Ordinal);

        var services = new ServiceCollection();
        typeof(POS.Wpf.App).GetMethod("ConfigureApplicationServices",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
            [services, new ConfigurationBuilder().AddInMemoryCollection().Build()]);
        Assert.Single(services, x => x.ServiceType == typeof(StorageStatusViewModel));
        Assert.Single(services, x => x.ServiceType == typeof(IStorageStatusDialogService));
        Assert.Single(services, x => x.ServiceType == typeof(POS.Wpf.Views.StorageStatusWindow));
    }

    private static StorageStatusViewModel ViewModel(FakeMonitor monitor) =>
        new(monitor, NullLogger<StorageStatusViewModel>.Instance);

    private sealed class FakeMonitor(StoragePreflightStatus status) : IDatabaseStorageMonitor
    {
        public static readonly DatabaseStorageSnapshot Snapshot = new(
            DatabaseStorageSnapshotStatus.Available, StorageWarningState.Healthy,
            @"C:\", 16L << 30, 8L << 30, 1L << 10, 2L << 10, 3L << 10,
            new DateTimeOffset(2026, 8, 8, 0, 0, 0, TimeSpan.Zero), StorageUnavailableReason.None);
        public int SnapshotCalls { get; private set; }
        public int EvaluateCalls { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public StoragePreflightRequest? LastRequest { get; private set; }
        public TaskCompletionSource<DatabaseStorageSnapshot>? Completion { get; init; }
        public Exception? Exception { get; init; }
        public Task<DatabaseStorageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            SnapshotCalls++;
            LastToken = cancellationToken;
            if (Exception is not null) throw Exception;
            return Completion?.Task ?? Task.FromResult(Snapshot);
        }
        public StoragePreflightResult EvaluatePreflight(DatabaseStorageSnapshot snapshot, StoragePreflightRequest request)
        {
            EvaluateCalls++;
            LastRequest = request;
            return new(status, request.RequiredAdditionalBytes, 1, 1, snapshot.AvailableFreeBytes);
        }
        public long EstimatePreMigrationBackupBytes(long sqliteStorageFootprintBytes) =>
            throw new InvalidOperationException("UI must not estimate a migration backup.");
    }
}
