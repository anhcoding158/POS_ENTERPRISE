using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Services;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SupportBundleUiTests
{
    private const string Destination = @"D:\Support-Export";
    private const string Archive = @"D:\Support-Export\POS-Enterprise-Support-safe.zip";

    [Fact]
    public void Initial_state_has_no_consent_destination_or_automatic_export()
    {
        var service = new FakeService();
        using var viewModel = new SupportBundleViewModel(service, new FakePicker((string?)null));

        Assert.Equal(SupportBundleUiState.Idle, viewModel.State);
        Assert.False(viewModel.ConsentAccepted);
        Assert.False(viewModel.CanGenerate);
        Assert.False(viewModel.IsProgressVisible);
        Assert.True(viewModel.IsProgressIndeterminate);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task Cancelled_picker_does_not_change_state_or_start_export()
    {
        var service = new FakeService();
        using var viewModel = new SupportBundleViewModel(service, new FakePicker((string?)null));

        await viewModel.PickDestinationAsync();

        Assert.Equal(SupportBundleUiState.Idle, viewModel.State);
        Assert.Null(viewModel.DestinationDirectory);
        Assert.Equal(0, service.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative")]
    public async Task Empty_or_relative_picker_result_never_enables_generate(string value)
    {
        using var viewModel = new SupportBundleViewModel(
            new FakeService(), new FakePicker(value));
        await viewModel.PickDestinationAsync();
        viewModel.ConsentAccepted = true;
        Assert.False(viewModel.CanGenerate);
        Assert.Equal(SupportBundleUiState.Idle, viewModel.State);
    }

    [Fact]
    public async Task Generate_requires_picker_destination_and_explicit_consent()
    {
        using var viewModel = new SupportBundleViewModel(
            new FakeService(), new FakePicker(Destination));

        await viewModel.PickDestinationAsync();
        Assert.False(viewModel.CanGenerate);
        viewModel.ConsentAccepted = true;
        Assert.True(viewModel.CanGenerate);
        Assert.Equal(SupportBundleUiState.Ready, viewModel.State);
    }

    [Fact]
    public async Task Request_always_excludes_database_and_running_is_single_flight()
    {
        var pending = Pending();
        var service = new FakeService((_, _) => pending.Task);
        using var viewModel = await ReadyAsync(service);

        var operation = viewModel.GenerateAsync();
        await WaitUntilAsync(() => service.CallCount == 1);
        var reentrant = viewModel.GenerateAsync();

        Assert.False(service.LastRequest!.IncludeDatabase);
        Assert.Equal(Destination, service.LastRequest.DestinationDirectory);
        Assert.Equal(1, service.CallCount);
        Assert.Equal(SupportBundleUiState.Running, viewModel.State);
        Assert.True(viewModel.IsProgressVisible);
        Assert.True(viewModel.IsProgressIndeterminate);
        Assert.False(viewModel.CanPickDestination);
        Assert.False(viewModel.CanChangeConsent);
        Assert.False(viewModel.CanGenerate);
        Assert.True(viewModel.CanCancel);

        pending.SetResult(SupportBundleResult.Success(Archive));
        await Task.WhenAll(operation, reentrant);
    }

    [Fact]
    public async Task Cancel_requests_operation_token_and_waits_for_typed_terminal_result()
    {
        var pending = Pending();
        var service = new FakeService((_, _) => pending.Task);
        using var viewModel = await ReadyAsync(service);
        var operation = viewModel.GenerateAsync();
        await WaitUntilAsync(() => service.LastToken.HasValue);

        await viewModel.CancelAsync();

        Assert.Equal(SupportBundleUiState.Cancelling, viewModel.State);
        Assert.Equal("Đang hủy...", viewModel.StatusMessage);
        Assert.True(service.LastToken!.Value.IsCancellationRequested);
        Assert.False(viewModel.CanCancel);
        Assert.DoesNotContain("thành công", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);

        pending.SetResult(SupportBundleResult.Failure(SupportBundleStatus.Cancelled));
        await operation;
        Assert.Equal(SupportBundleUiState.Cancelled, viewModel.State);
        Assert.Equal("Đã hủy tạo gói hỗ trợ.", viewModel.StatusMessage);
        Assert.Null(viewModel.ArchivePath);
    }

    [Fact]
    public async Task Late_cancellation_followed_by_success_is_presented_as_success()
    {
        var pending = Pending();
        var service = new FakeService((_, _) => pending.Task);
        using var viewModel = await ReadyAsync(service);
        var operation = viewModel.GenerateAsync();
        await WaitUntilAsync(() => service.LastToken.HasValue);
        await viewModel.CancelAsync();

        pending.SetResult(SupportBundleResult.Success(Archive));
        await operation;

        Assert.Equal(SupportBundleUiState.Success, viewModel.State);
        Assert.Equal(Path.GetFileName(Archive), viewModel.ArchiveName);
        Assert.Equal(Archive, viewModel.ArchivePath);
        Assert.Equal("Gói hỗ trợ đã được tạo thành công.", viewModel.StatusMessage);
    }

    [Theory]
    [InlineData(SupportBundleStatus.InvalidDestination, "Thư mục lưu không hợp lệ. Vui lòng chọn lại.")]
    [InlineData(SupportBundleStatus.DestinationUnavailable, "Không thể ghi vào thư mục đã chọn. Vui lòng chọn thư mục khác.")]
    [InlineData(SupportBundleStatus.ArchiveAlreadyExists, "Tên tệp gói hỗ trợ đã tồn tại. Vui lòng thử tạo lại.")]
    [InlineData(SupportBundleStatus.DatabaseInclusionNotSupported, "Yêu cầu tạo gói hỗ trợ không được hỗ trợ.")]
    [InlineData(SupportBundleStatus.ArchiveCreationFailure, "Không thể tạo tệp gói hỗ trợ. Vui lòng kiểm tra thư mục lưu.")]
    [InlineData(SupportBundleStatus.UnexpectedFailure, "Không thể tạo gói hỗ trợ. Vui lòng thử lại.")]
    public async Task Typed_failures_map_to_fixed_safe_vietnamese_messages(
        SupportBundleStatus status, string expected)
    {
        var service = new FakeService((_, _) =>
            Task.FromResult(SupportBundleResult.Failure(status)));
        using var viewModel = await ReadyAsync(service);

        await viewModel.GenerateAsync();

        Assert.Equal(SupportBundleUiState.Failed, viewModel.State);
        Assert.Equal(expected, viewModel.StatusMessage);
        Assert.Null(viewModel.ArchiveName);
        Assert.Null(viewModel.ArchivePath);
    }

    [Fact]
    public async Task Thrown_exception_is_contained_without_raw_canary()
    {
        const string canary = "SELECT secret FROM Customers at C:\\Users\\private\\store.db";
        var service = new FakeService((_, _) =>
            throw new InvalidOperationException(canary));
        using var viewModel = await ReadyAsync(service);

        var exception = await Record.ExceptionAsync(viewModel.GenerateAsync);

        Assert.Null(exception);
        Assert.Equal(SupportBundleUiState.Failed, viewModel.State);
        Assert.DoesNotContain(canary, viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\Users", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Close_while_running_requests_cancel_and_signals_only_after_terminal_result()
    {
        var pending = Pending();
        var service = new FakeService((_, _) => pending.Task);
        using var viewModel = await ReadyAsync(service);
        var closeReady = 0;
        viewModel.CloseReady += (_, _) => closeReady++;
        var operation = viewModel.GenerateAsync();
        await WaitUntilAsync(() => service.LastToken.HasValue);

        Assert.False(viewModel.RequestClose());
        Assert.Equal(SupportBundleUiState.Cancelling, viewModel.State);
        Assert.True(service.LastToken!.Value.IsCancellationRequested);
        Assert.Equal(0, closeReady);

        pending.SetResult(SupportBundleResult.Failure(SupportBundleStatus.Cancelled));
        await operation;
        Assert.Equal(1, closeReady);
        Assert.True(viewModel.RequestClose());
    }

    [Fact]
    public async Task Reset_requires_new_consent_destination_and_operation_token()
    {
        var tokens = new List<CancellationToken>();
        var service = new FakeService((_, token) =>
        {
            tokens.Add(token);
            return Task.FromResult(SupportBundleResult.Success(Archive));
        });
        var picker = new FakePicker(Destination, Destination);
        using var viewModel = new SupportBundleViewModel(service, picker);

        await viewModel.PickDestinationAsync();
        viewModel.ConsentAccepted = true;
        await viewModel.GenerateAsync();
        Assert.False(viewModel.ConsentAccepted);
        Assert.True(viewModel.CanReset);

        await viewModel.ResetAsync();
        Assert.Equal(SupportBundleUiState.Idle, viewModel.State);
        Assert.Null(viewModel.DestinationDirectory);
        Assert.False(viewModel.CanGenerate);

        await viewModel.PickDestinationAsync();
        viewModel.ConsentAccepted = true;
        await viewModel.GenerateAsync();
        Assert.Equal(2, tokens.Count);
        Assert.NotEqual(tokens[0], tokens[1]);
    }

    [Fact]
    public void Xaml_has_required_consent_indeterminate_progress_and_no_database_or_network_action()
    {
        var xaml = File.ReadAllText(Path.Combine(SolutionRoot(), "src", "POS.Wpf",
            "Views", "SupportBundleWindow.xaml"));
        Assert.Contains(SupportBundleViewModel.ConsentText, xaml, StringComparison.Ordinal);
        Assert.Contains("Không chứa database bán hàng", xaml, StringComparison.Ordinal);
        Assert.Contains("WAL/SHM/journal/backup", xaml, StringComparison.Ordinal);
        Assert.Contains("Không tự động tải lên hoặc gửi", xaml, StringComparison.Ordinal);
        Assert.Contains("IsProgressIndeterminate", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IncludeDatabase", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SendAsync", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("%", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Entry_point_is_inside_authenticated_shell_without_new_permission()
    {
        var root = SolutionRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "ShellWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "ShellWindow.xaml.cs"));
        Assert.Contains("SupportBundleNavigationButton", xaml, StringComparison.Ordinal);
        Assert.Contains("Gói hỗ trợ", xaml, StringComparison.Ordinal);
        Assert.Contains("_currentUserService.IsAuthenticated", code, StringComparison.Ordinal);
        Assert.Contains("_supportBundleDialogService.Show(this)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("SupportBundle", File.ReadAllText(Path.Combine(root,
            "src", "POS.Application", "Authorization", "SystemPermission.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ViewModel_depends_only_on_application_contract_and_picker_adapter()
    {
        var constructor = Assert.Single(typeof(SupportBundleViewModel).GetConstructors());
        Assert.Equal(new[] { typeof(ISupportBundleService), typeof(ISupportBundleFolderPicker) },
            constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        var source = File.ReadAllText(Path.Combine(SolutionRoot(), "src", "POS.Wpf",
            "ViewModels", "SupportBundleViewModel.cs"));
        foreach (var forbidden in new[]
            { "POS.Infrastructure", "DbContext", "Sqlite", "ZipArchive", "InfrastructureOptions", "Task.Run", ".Wait(", ".Result" })
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
    }

    [Fact]
    public void App_di_registers_bundle_service_picker_viewmodel_window_and_dialog_once()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        typeof(POS.Wpf.App).GetMethod("ConfigureApplicationServices",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [services, configuration]);

        Assert.Single(services, item => item.ServiceType == typeof(ISupportBundleService));
        Assert.Single(services, item => item.ServiceType == typeof(ISupportBundleFolderPicker));
        Assert.Single(services, item => item.ServiceType == typeof(ISupportBundleDialogService));
        Assert.Single(services, item => item.ServiceType == typeof(SupportBundleViewModel));
        Assert.Single(services, item => item.ServiceType == typeof(POS.Wpf.Views.SupportBundleWindow));
    }

    private static async Task<SupportBundleViewModel> ReadyAsync(FakeService service)
    {
        var viewModel = new SupportBundleViewModel(service, new FakePicker(Destination));
        await viewModel.PickDestinationAsync();
        viewModel.ConsentAccepted = true;
        return viewModel;
    }

    private static TaskCompletionSource<SupportBundleResult> Pending() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }

    private static string SolutionRoot() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed class FakePicker(params string?[] values) : ISupportBundleFolderPicker
    {
        private readonly Queue<string?> _values = new(values);
        public string? PickDestination() => _values.Count == 0 ? null : _values.Dequeue();
    }

    private sealed class FakeService : ISupportBundleService
    {
        private readonly Func<SupportBundleRequest, CancellationToken, Task<SupportBundleResult>> _handler;
        public FakeService(Func<SupportBundleRequest, CancellationToken, Task<SupportBundleResult>>? handler = null) =>
            _handler = handler ?? ((_, _) => Task.FromResult(
                SupportBundleResult.Failure(SupportBundleStatus.UnexpectedFailure)));
        public int CallCount { get; private set; }
        public SupportBundleRequest? LastRequest { get; private set; }
        public CancellationToken? LastToken { get; private set; }
        public Task<SupportBundleResult> ExportAsync(
            SupportBundleRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            LastToken = cancellationToken;
            return _handler(request, cancellationToken);
        }
    }
}
