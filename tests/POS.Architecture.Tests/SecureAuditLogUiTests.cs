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
                foreach (var size in new[] { new Size(1180, 720), new Size(1366, 768), new Size(1280, 720), new Size(900, 560) })
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
            }
            finally
            {
                window.Close();
            }
        });
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
