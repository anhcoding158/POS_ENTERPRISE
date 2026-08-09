using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Services;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ManualBackupUiTests
{
    private const string Destination = @"D:\Backup-Destination";
    private const string BackupPath = @"D:\Backup-Destination\POS-Enterprise-Backup-20260809-083045123.db";

    [Fact]
    public async Task Initial_state_requires_destination_and_explicit_consent()
    {
        using var viewModel = new ManualBackupViewModel(
            new FakeService(), new FakePicker((string?)null));

        Assert.Equal(ManualBackupUiState.Idle, viewModel.State);
        Assert.False(viewModel.ConsentAccepted);
        Assert.False(viewModel.CanBackup);
        Assert.False(viewModel.IsProgressVisible);
        Assert.False(viewModel.IsProgressIndeterminate);
    }

    [Fact]
    public async Task Cancelled_picker_does_not_change_state_or_start_backup()
    {
        var service = new FakeService();
        using var viewModel = new ManualBackupViewModel(service, new FakePicker((string?)null));

        await viewModel.PickDestinationAsync();

        Assert.Equal(ManualBackupUiState.Idle, viewModel.State);
        Assert.Null(viewModel.DestinationDirectory);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task Generate_requires_picker_destination_and_explicit_consent()
    {
        using var viewModel = new ManualBackupViewModel(new FakeService(), new FakePicker(Destination));

        await viewModel.PickDestinationAsync();
        Assert.False(viewModel.CanBackup);
        viewModel.ConsentAccepted = true;
        Assert.True(viewModel.CanBackup);
        Assert.Equal(ManualBackupUiState.Ready, viewModel.State);
    }

    [Fact]
    public async Task Request_is_single_flight_and_success_updates_metadata()
    {
        var pending = new TaskCompletionSource<ManualBackupResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeService((_, _) => pending.Task);
        using var viewModel = await ReadyAsync(service);

        var operation = viewModel.BackupAsync();
        await WaitUntilAsync(() => service.CallCount == 1);
        var reentrant = viewModel.BackupAsync();

        Assert.Equal(1, service.CallCount);
        Assert.Equal(ManualBackupUiState.Running, viewModel.State);
        Assert.True(viewModel.IsProgressVisible);
        Assert.False(viewModel.CanPickDestination);
        Assert.False(viewModel.CanChangeConsent);
        Assert.False(viewModel.CanBackup);

        pending.SetResult(ManualBackupResult.Success(BackupPath, 12345, "ABCDEF1234", new DateTimeOffset(2026, 8, 9, 8, 30, 45, TimeSpan.Zero)));
        await Task.WhenAll(operation, reentrant);

        Assert.Equal(ManualBackupUiState.Success, viewModel.State);
        Assert.Equal(BackupPath, viewModel.BackupPath);
        Assert.Contains("12.06 KB", viewModel.BackupSizeText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ABCDEF1234", viewModel.BackupSha256Text);
    }

    [Theory]
    [InlineData(ManualBackupStatus.InvalidDestination, "Thư mục đích không hợp lệ. Vui lòng chọn lại.")]
    [InlineData(ManualBackupStatus.DestinationUnavailable, "Không thể ghi vào thư mục đã chọn. Vui lòng chọn thư mục khác.")]
    [InlineData(ManualBackupStatus.SourceUnavailable, "Không thể mở database nguồn.")]
    [InlineData(ManualBackupStatus.ArchiveAlreadyExists, "Tệp backup đã tồn tại. Vui lòng thử lại.")]
    [InlineData(ManualBackupStatus.VerificationFailed, "Backup tạo xong nhưng không vượt qua verify.")]
    [InlineData(ManualBackupStatus.UnexpectedFailure, "Không thể sao lưu dữ liệu. Vui lòng thử lại.")]
    public async Task Typed_failures_map_to_fixed_safe_vietnamese_messages(
        ManualBackupStatus status, string expected)
    {
        var service = new FakeService((_, _) =>
            Task.FromResult(ManualBackupResult.Failure(status)));
        using var viewModel = await ReadyAsync(service);

        await viewModel.BackupAsync();

        Assert.Equal(ManualBackupUiState.Failed, viewModel.State);
        Assert.Equal(expected, viewModel.StatusMessage);
        Assert.Null(viewModel.BackupPath);
    }

    [Fact]
    public async Task ViewModel_depends_only_on_application_contract_and_picker_adapter()
    {
        var constructor = Assert.Single(typeof(ManualBackupViewModel).GetConstructors());
        Assert.Equal(new[] { typeof(IManualBackupService), typeof(IManualBackupFolderPicker) },
            constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        var source = File.ReadAllText(Path.Combine(SolutionRoot(), "src", "POS.Wpf",
            "ViewModels", "ManualBackupViewModel.cs"));
        foreach (var forbidden in new[]
            { "POS.Infrastructure", "DbContext", "Sqlite", "ZipArchive", "Task.Run", ".Wait(", ".Result", "HttpClient" })
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
    }

    [Fact]
    public void App_di_registers_manual_backup_service_picker_viewmodel_window_and_dialog_once()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        typeof(POS.Wpf.App).GetMethod("ConfigureApplicationServices",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [services, configuration]);

        Assert.Single(services, item => item.ServiceType == typeof(IManualBackupService));
        Assert.Single(services, item => item.ServiceType == typeof(IManualBackupFolderPicker));
        Assert.Single(services, item => item.ServiceType == typeof(IManualBackupDialogService));
        Assert.Single(services, item => item.ServiceType == typeof(ManualBackupViewModel));
        Assert.Single(services, item => item.ServiceType == typeof(POS.Wpf.Views.ManualBackupWindow));
    }

    private static async Task<ManualBackupViewModel> ReadyAsync(FakeService service)
    {
        var viewModel = new ManualBackupViewModel(service, new FakePicker(Destination));
        await viewModel.PickDestinationAsync();
        viewModel.ConsentAccepted = true;
        return viewModel;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static string SolutionRoot() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed class FakePicker(params string?[] values) : IManualBackupFolderPicker
    {
        private readonly Queue<string?> _values = new(values);
        public string? PickDestination() => _values.Count == 0 ? null : _values.Dequeue();
    }

    private sealed class FakeService(
        Func<ManualBackupRequest, CancellationToken, Task<ManualBackupResult>>? handler = null)
        : IManualBackupService
    {
        private readonly Func<ManualBackupRequest, CancellationToken, Task<ManualBackupResult>> _handler =
            handler ?? ((_, _) => Task.FromResult(ManualBackupResult.Failure(ManualBackupStatus.UnexpectedFailure)));

        public int CallCount { get; private set; }
        public ManualBackupRequest? LastRequest { get; private set; }
        public CancellationToken? LastToken { get; private set; }

        public Task<ManualBackupResult> BackupAsync(
            ManualBackupRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            LastToken = cancellationToken;
            return _handler(request, cancellationToken);
        }
    }
}
