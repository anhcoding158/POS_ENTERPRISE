using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Common;
using POS.Application.DTOs.Audit;
using POS.Application.Abstractions.Services;
using POS.Domain.Enums;
using POS.Wpf;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SecureAuditLogUiTests
{
    [Fact]
    public async Task Audit_window_constructs_and_lays_out_at_supported_sizes()
    {
        await RunOnStaAsync(() =>
        {
            if (global::System.Windows.Application.Current is null)
            {
                var application = new App();
                application.InitializeComponent();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            using var services = new ServiceCollection()
                .AddSingleton<IAuditLogService, EmptyAuditLogService>()
                .AddTransient<AuditLogViewModel>()
                .BuildServiceProvider();
            var viewModel = services.GetRequiredService<AuditLogViewModel>();
            var window = new AuditLogWindow(viewModel);

            Assert.Same(viewModel, window.DataContext);
            Assert.Equal("AuditLogWindow", AutomationProperties.GetAutomationId(window));
            Assert.Equal("Tất cả hành động", viewModel.SelectedAction?.DisplayName);
            window.Show();

            try
            {
                foreach (var size in new[] { new Size(1920, 1080), new Size(1366, 768), new Size(1280, 720), new Size(900, 560) })
                {
                    window.Measure(size);
                    window.Arrange(new Rect(0, 0, size.Width, size.Height));
                    window.UpdateLayout();
                    Assert.True(window.ActualWidth > 0 && window.ActualHeight > 0);
                }

                var auditList = FindVisualDescendants<global::System.Windows.Controls.DataGrid>(window)
                    .Single(grid => AutomationProperties.GetAutomationId(grid) == "AuditList");
                Assert.True(global::System.Windows.Controls.VirtualizingPanel.GetIsVirtualizing(auditList));
                Assert.Contains(auditList.Columns, column => Equals(column.Header, "Người thực hiện"));
                Assert.Contains(auditList.Columns, column => Equals(column.Header, "Thời gian"));
                Assert.Contains(auditList.Columns, column => Equals(column.Header, "Hoạt động"));
                Assert.DoesNotContain(auditList.Columns, column => Equals(column.Header, "Nghiệp vụ"));
                var searchButton = FindVisualDescendants<global::System.Windows.Controls.Button>(window)
                    .Single(button => AutomationProperties.GetAutomationId(button) == "AuditSearchButton");
                Assert.Same(viewModel.SearchCommand, searchButton.Command);
                Assert.Contains(FindVisualDescendants<global::System.Windows.Controls.TextBlock>(window),
                    textBlock => textBlock.Text == "Chọn một hoạt động để xem chi tiết");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Audit_view_model_separates_empty_filtered_empty_and_search_reset()
    {
        var service = new RecordingAuditLogService();
        using var provider = new ServiceCollection()
            .AddSingleton<IAuditLogService>(service)
            .BuildServiceProvider();
        var viewModel = new AuditLogViewModel(provider.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>());

        await viewModel.InitializeAsync();
        Assert.True(viewModel.IsDatabaseEmpty);
        Assert.False(viewModel.IsFilteredNoResult);
        Assert.True(viewModel.IsNoSelection);
        Assert.Single(service.SearchRequests);

        viewModel.ActorFilter = "no-match";
        viewModel.SearchCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SearchCommand);
        Assert.True(viewModel.IsFilteredNoResult);
        Assert.False(viewModel.IsDatabaseEmpty);
        Assert.Equal("no-match", service.SearchRequests[^1].Actor);

        viewModel.ClearFiltersCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ClearFiltersCommand);
        Assert.Equal(string.Empty, viewModel.ActorFilter);
        Assert.Equal(viewModel.ActionOptions[0], viewModel.SelectedAction);
        Assert.Equal(DateTime.Today.AddDays(-6), viewModel.FromDate?.Date);
        Assert.Equal(DateTime.Today, viewModel.ToDate?.Date);
        Assert.True(viewModel.IsDatabaseEmpty);
        Assert.Equal(3, service.SearchRequests.Count);
    }

    [Fact]
    public async Task Audit_view_model_rejects_invalid_date_without_query_and_retries_failure()
    {
        var service = new RecordingAuditLogService { FailNextSearch = true };
        using var provider = new ServiceCollection()
            .AddSingleton<IAuditLogService>(service)
            .BuildServiceProvider();
        var viewModel = new AuditLogViewModel(provider.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>());

        viewModel.FromDate = DateTime.Today;
        viewModel.ToDate = DateTime.Today.AddDays(-1);
        await viewModel.InitializeAsync();
        Assert.Empty(service.SearchRequests);
        Assert.True(viewModel.HasError);
        Assert.Contains("Từ ngày phải nhỏ hơn hoặc bằng Đến ngày", viewModel.StatusMessage, StringComparison.Ordinal);

        viewModel.ToDate = DateTime.Today;
        viewModel.SearchCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SearchCommand);
        Assert.True(viewModel.HasError);
        Assert.Contains("Không thể tải nhật ký hoạt động", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Single(service.SearchRequests);

        viewModel.RetryCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RetryCommand);
        Assert.False(viewModel.HasError);
        Assert.True(viewModel.IsFilteredNoResult);
        Assert.Equal(2, service.SearchRequests.Count);
    }

    private static async Task WaitForCommandAsync(POS.Wpf.Commands.AsyncRelayCommand command)
    {
        while (command.IsExecuting)
            await Task.Delay(1);
    }

    private sealed class EmptyAuditLogService : IAuditLogService
    {
        public Task<Result<PagedResult<AuditListItemDto>>> SearchAsync(
            AuditSearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(PagedResult.Empty<AuditListItemDto>(1, 25)));

        public Task<Result<AuditDetailsDto>> GetDetailsAsync(
            int auditId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<AuditDetailsDto>(
                new AppError(ErrorCodes.General.NotFound, "Không tìm thấy hoạt động.")));
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public List<AuditSearchRequest> SearchRequests { get; } = [];
        public bool FailNextSearch { get; set; }

        public Task<Result<PagedResult<AuditListItemDto>>> SearchAsync(
            AuditSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            SearchRequests.Add(request);
            if (FailNextSearch)
            {
                FailNextSearch = false;
                return Task.FromResult(Result.Failure<PagedResult<AuditListItemDto>>(
                    new AppError(ErrorCodes.General.Unexpected, "Lỗi truy vấn.")));
            }

            var items = request.Actor == "no-match"
                ? Array.Empty<AuditListItemDto>()
                : Array.Empty<AuditListItemDto>();
            return Task.FromResult(Result.Success(new PagedResult<AuditListItemDto>(
                items, request.PageNumber, request.PageSize, 0)));
        }

        public Task<Result<AuditDetailsDto>> GetDetailsAsync(
            int auditId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<AuditDetailsDto>(
                new AppError(ErrorCodes.General.NotFound, "Không tìm thấy hoạt động.")));
    }

    private static Task<object?> RunOnStaAsync(Action action)
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(null); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualDescendants<T>(child)) yield return descendant;
        }
    }
}
