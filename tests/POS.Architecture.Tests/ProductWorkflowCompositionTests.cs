using POS.Application.Common;
using POS.Application.DTOs.Categories;
using POS.Application.DTOs.Exports;
using POS.Application.DTOs.Products;
using POS.Application.Abstractions.Exports;
using POS.Application.Abstractions.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using POS.Wpf.Services;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductWorkflowCompositionTests
{
    [Fact]
    public void Product_export_window_must_construct_with_production_app_resources()
    {
        RunOnSta(() =>
        {
            EnsureApplication();
            using var viewModel = new ProductExportViewModel(
                new NoOpProductExportService(),
                new NoOpProductExportWriter());
            var window = new ProductExportWindow(viewModel);
            try
            {
                window.Measure(new global::System.Windows.Size(560, 430));
                window.Arrange(new global::System.Windows.Rect(0, 0, 560, 430));
                window.UpdateLayout();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Bulk_product_window_must_construct_with_production_app_resources()
    {
        RunOnSta(() =>
        {
            EnsureApplication();
            var row = new ProductRowViewModel(new ProductListItemDto(
                1, 2, "Đồ uống", "SP001", "000123", "Sữa", "Hộp",
                10, 20, 10, 3, 5, true, false, true, false, true));
            using var viewModel = new BulkProductViewModel(
                [row],
                new NoOpBulkProductOperationService(),
                [new CategoryOptionDto(2, "Đồ uống", 0)]);
            var window = new BulkProductWindow(viewModel);
            try
            {
                window.Show();
                window.Measure(new global::System.Windows.Size(920, 680));
                window.Arrange(new global::System.Windows.Rect(0, 0, 920, 680));
                window.UpdateLayout();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Bulk_status_production_combobox_updates_typed_preview_request_for_both_values()
    {
        RunOnSta(() =>
        {
            EnsureApplication();
            var row = new ProductRowViewModel(new ProductListItemDto(
                1, 2, "Đồ uống", "SP001", null, "Sữa", "Hộp",
                10, 20, 10, 3, 5, true, false, true, false, true));
            var service = new CapturingBulkProductOperationService();
            using var viewModel = new BulkProductViewModel(
                [row], service, [new CategoryOptionDto(2, "Đồ uống", 0)]);
            viewModel.SelectedOperation = viewModel.Operations.Single(option => option.Operation == BulkProductOperationType.SetActiveState);
            var window = new BulkProductWindow(viewModel);
            try
            {
                window.Show();
                window.Measure(new global::System.Windows.Size(920, 680));
                window.Arrange(new global::System.Windows.Rect(0, 0, 920, 680));
                window.UpdateLayout();
                window.Dispatcher.Invoke(global::System.Windows.Threading.DispatcherPriority.DataBind, new Action(() => { }));
                var combo = (global::System.Windows.Controls.ComboBox)window.FindName("StatusOperationComboBox")!;

                Assert.Equal(2, combo.Items.Count);
                viewModel.PreviewCommand.Execute(null);
                Assert.Null(service.LastRequest);
                Assert.Equal("Chọn trạng thái bán mới.", viewModel.ErrorMessage);
                combo.SelectedIndex = 1;
                combo.GetBindingExpression(global::System.Windows.Controls.Primitives.Selector.SelectedItemProperty)!.UpdateSource();
                Assert.False(viewModel.SelectedStatus!.Value);
                viewModel.PreviewCommand.Execute(null);
                Assert.False(service.LastRequest!.IsActive);

                combo.SelectedIndex = 0;
                combo.GetBindingExpression(global::System.Windows.Controls.Primitives.Selector.SelectedItemProperty)!.UpdateSource();
                Assert.True(viewModel.SelectedStatus!.Value);
                viewModel.PreviewCommand.Execute(null);
                Assert.True(service.LastRequest!.IsActive);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Export_result_is_saved_only_after_the_writer_completes()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "POS-Enterprise-export-feedback-" + Guid.NewGuid().ToString("N") + ".xlsx");
        var writer = new RecordingProductExportWriter();
        using var viewModel = new ProductExportViewModel(
            new NoOpProductExportService(),
            writer,
            initialReport: ProductExportReportType.ProductImportTemplate);

        var saved = await viewModel.ExportAsync(path);

        Assert.True(saved);
        Assert.True(writer.Completed);
        Assert.NotNull(viewModel.DialogResult);
        Assert.Equal(ProductExportDialogOutcome.Saved, viewModel.DialogResult!.Outcome);
        Assert.Equal(Path.GetFileName(path), viewModel.DialogResult.FileName);
        Assert.Equal(path, viewModel.DialogResult.DestinationPath);
    }

    private static void EnsureApplication()
    {
        if (global::System.Windows.Application.Current is null)
        {
            var application = new POS.Wpf.App();
            application.InitializeComponent();
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    private sealed class NoOpProductExportService : IProductExportService
    {
        public Task<Result<ProductExportData>> ExportAsync(
            ProductExportRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new ProductExportData(
                request.ReportType, [], [], 0, false, "test", [])));
    }

    private sealed class NoOpProductExportWriter : IProductExportWriter
    {
        public Task WriteAsync(
            ProductExportData data,
            ProductExportFormat format,
            string destinationPath,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingProductExportWriter : IProductExportWriter
    {
        public bool Completed { get; private set; }

        public Task WriteAsync(
            ProductExportData data,
            ProductExportFormat format,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            Completed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpBulkProductOperationService : IBulkProductOperationService
    {
        public Task<Result<BulkProductPreview>> PreviewAsync(
            BulkProductOperationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<BulkProductOperationResult>> CommitAsync(
            BulkProductPreview preview,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingBulkProductOperationService : IBulkProductOperationService
    {
        public BulkProductOperationRequest? LastRequest { get; private set; }

        public Task<Result<BulkProductPreview>> PreviewAsync(BulkProductOperationRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result.Success(new BulkProductPreview(Guid.NewGuid(), request, [], 1, 0, true, [])));
        }

        public Task<Result<BulkProductOperationResult>> CommitAsync(BulkProductPreview preview, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new BulkProductOperationResult(preview.PreviewId, true, preview.Request.Selection.Count, 1, 0, [])));
    }
}
