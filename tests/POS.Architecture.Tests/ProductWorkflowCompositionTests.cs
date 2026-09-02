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
    public void Bulk_product_window_uses_compact_resizable_shell_and_hides_cancel_when_idle()
    {
        RunOnSta(() =>
        {
            EnsureApplication();
            var row = CreateProductRow();
            using var viewModel = new BulkProductViewModel(
                [row],
                new NoOpBulkProductOperationService(),
                [new CategoryOptionDto(2, "Đồ uống", 0)]);
            var window = new BulkProductWindow(viewModel);
            try
            {
                window.Show();
                window.Dispatcher.Invoke(global::System.Windows.Threading.DispatcherPriority.DataBind, new Action(() => { }));

                Assert.Equal(global::System.Windows.WindowState.Normal, window.WindowState);
                Assert.Equal(1160, window.Width);
                Assert.Equal(760, window.Height);
                Assert.Equal(global::System.Windows.ResizeMode.CanResizeWithGrip, window.ResizeMode);
                Assert.Equal(global::System.Windows.Visibility.Collapsed, ((global::System.Windows.Controls.Button)window.FindName("BulkCancelButton")!).Visibility);
                Assert.Equal(global::System.Windows.Visibility.Visible, ((global::System.Windows.Controls.DataGrid)window.FindName("BulkPreviewGrid")!).Visibility);
                var previewGrid = (global::System.Windows.Controls.DataGrid)window.FindName("BulkPreviewGrid")!;
                Assert.Equal(4, previewGrid.Columns.Count);
                Assert.Single(previewGrid.Items);
                var previewRow = Assert.IsType<BulkProductPreviewRowViewModel>(previewGrid.Items[0]);
                Assert.Equal("SP001", previewRow.ProductCode);
                Assert.Equal("Chưa xem trước", previewRow.ResultText);
                Assert.Equal(global::System.Windows.Visibility.Visible, ((global::System.Windows.Controls.Button)window.FindName("BulkCloseButton")!).Visibility);
                Assert.Equal(global::System.Windows.Visibility.Visible, ((global::System.Windows.Controls.Button)window.FindName("BulkPreviewButton")!).Visibility);
                Assert.Equal(global::System.Windows.Visibility.Visible, ((global::System.Windows.Controls.Button)window.FindName("BulkConfirmButton")!).Visibility);

                foreach (var size in new[]
                {
                    new global::System.Windows.Size(900, 650),
                    new global::System.Windows.Size(1180, 760),
                    new global::System.Windows.Size(1600, 900)
                })
                {
                    window.Measure(size);
                    window.Arrange(new global::System.Windows.Rect(0, 0, size.Width, size.Height));
                    window.UpdateLayout();
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Bulk_product_window_materializes_the_editor_for_each_operation()
    {
        RunOnSta(() =>
        {
            EnsureApplication();
            using var viewModel = new BulkProductViewModel(
                [CreateProductRow()],
                new NoOpBulkProductOperationService(),
                [new CategoryOptionDto(2, "Đồ uống", 0)]);
            var window = new BulkProductWindow(viewModel);
            try
            {
                window.Show();
                window.Measure(new global::System.Windows.Size(1180, 760));
                window.Arrange(new global::System.Windows.Rect(0, 0, 1180, 760));
                window.UpdateLayout();
                window.Dispatcher.Invoke(global::System.Windows.Threading.DispatcherPriority.DataBind, new Action(() => { }));

                foreach (var operation in viewModel.Operations)
                {
                    viewModel.SelectedOperation = operation;
                    window.Dispatcher.Invoke(global::System.Windows.Threading.DispatcherPriority.DataBind, new Action(() => { }));

                    Assert.Equal(operation.Operation == BulkProductOperationType.SetPrices,
                        ((global::System.Windows.Controls.TextBox)window.FindName("BulkSalePriceInput")!).IsVisible);
                    Assert.Equal(operation.Operation == BulkProductOperationType.SetPrices,
                        ((global::System.Windows.Controls.TextBox)window.FindName("BulkCostPriceInput")!).IsVisible);
                    Assert.Equal(operation.Operation == BulkProductOperationType.SetCategory,
                        ((global::System.Windows.Controls.ComboBox)window.FindName("BulkCategorySelector")!).IsVisible);
                    Assert.Equal(operation.Operation == BulkProductOperationType.SetActiveState,
                        ((global::System.Windows.Controls.ComboBox)window.FindName("StatusOperationComboBox")!).IsVisible);
                    Assert.Equal(operation.Operation == BulkProductOperationType.SetMinimumStock,
                        ((global::System.Windows.Controls.TextBox)window.FindName("BulkMinimumStockInput")!).IsVisible);
                }
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

    private static ProductRowViewModel CreateProductRow() => new(new ProductListItemDto(
        1, 2, "Đồ uống", "SP001", "000123", "Sữa", "Hộp",
        10, 20, 10, 3, 5, true, false, true, false, true));

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
