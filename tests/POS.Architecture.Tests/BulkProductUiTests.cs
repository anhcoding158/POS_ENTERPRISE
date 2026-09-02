using POS.Application.Common;
using POS.Application.DTOs.Categories;
using POS.Application.DTOs.Products;
using POS.Application.Abstractions.Services;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class BulkProductUiTests
{
    [Fact]
    public void Product_row_selection_is_bindable_and_notifies_the_shell()
    {
        var row = new ProductRowViewModel(new ProductListItemDto(
            1, 2, "Đồ uống", "SP001", "000123", "Sữa", "Hộp",
            10, 20, 10, 3, 5, true, false, true, false, true)
        { UpdatedAtUtc = DateTimeOffset.UtcNow });
        var notified = false;
        row.PropertyChanged += (_, args) =>
            notified |= args.PropertyName == nameof(ProductRowViewModel.IsBulkSelected);

        row.IsBulkSelected = true;

        Assert.True(row.IsBulkSelected);
        Assert.True(notified);
    }

    [Fact]
    public void Bulk_dialog_exposes_only_named_product_operations_and_scope()
    {
        var row = new ProductRowViewModel(new ProductListItemDto(
            1, 2, "Đồ uống", "SP001", null, "Sữa", "Hộp",
            10, 20, 10, 3, 5, true, false, true, false, true));
        using var viewModel = new BulkProductViewModel(
            [row],
            new NoOpBulkService(),
            [new CategoryOptionDto(2, "Đồ uống", 0)]);

        Assert.Equal(1, viewModel.SelectedCount);
        Assert.Contains(viewModel.Operations, operation => operation.DisplayName == "Cập nhật giá");
        Assert.Contains(viewModel.Operations, operation => operation.DisplayName == "Chuyển danh mục");
        Assert.Contains(viewModel.Operations, operation => operation.DisplayName == "Đổi trạng thái bán");
        Assert.Contains(viewModel.Operations, operation => operation.DisplayName == "Đặt tồn tối thiểu");
        var initialRow = Assert.Single(viewModel.PreviewRows);
        Assert.Equal("Chưa xem trước", initialRow.ResultText);
        Assert.Contains("Giá bán: 20", initialRow.BeforeValue, StringComparison.Ordinal);
        Assert.Contains("Giá vốn: 10", initialRow.BeforeValue, StringComparison.Ordinal);

        viewModel.SelectedOperation = viewModel.Operations.Single(operation => operation.Operation == BulkProductOperationType.SetCategory);

        var categoryRow = Assert.Single(viewModel.PreviewRows);
        Assert.Equal("Đồ uống", categoryRow.BeforeValue);
        Assert.Equal("—", categoryRow.AfterValue);
        Assert.Equal("Chưa xem trước", categoryRow.ResultText);
    }

    [Fact]
    public void Changing_bulk_input_invalidates_preview_and_locks_confirmation()
    {
        var row = new ProductRowViewModel(new ProductListItemDto(
            1, 2, "Đồ uống", "SP001", null, "Sữa", "Hộp",
            10, 20, 10, 3, 5, true, false, true, false, true));
        using var viewModel = new BulkProductViewModel(
            [row],
            new ImmediatePreviewService(changeCount: 1),
            [new CategoryOptionDto(2, "Đồ uống", 0)]);

        viewModel.SalePriceText = "100";
        viewModel.CostPriceText = "200";
        viewModel.PreviewCommand.Execute(null);

        Assert.True(viewModel.HasPreview);
        Assert.False(viewModel.IsPreviewStale);
        Assert.True(viewModel.ConfirmCommand.CanExecute(null));

        viewModel.SalePriceText = "101";

        Assert.False(viewModel.HasPreview);
        Assert.True(viewModel.IsPreviewStale);
        var staleRow = Assert.Single(viewModel.PreviewRows);
        Assert.Contains("Giá bán: 20", staleRow.BeforeValue, StringComparison.Ordinal);
        Assert.Equal("—", staleRow.AfterValue);
        Assert.Equal("Cần xem trước lại", staleRow.ResultText);
        Assert.True(staleRow.IsPreviewStale);
        Assert.False(viewModel.ConfirmCommand.CanExecute(null));
        Assert.Contains("xem trước lại", viewModel.PreviewPromptText, StringComparison.Ordinal);
    }

    [Fact]
    public void Reference_projection_keeps_current_value_for_all_four_operations()
    {
        var row = new ProductRowViewModel(new ProductListItemDto(
            1, 2, "Đồ uống", "SP001", null, "Sữa", "Hộp",
            10, 20, 10, 3, 5, true, false, true, false, true));
        using var viewModel = new BulkProductViewModel(
            [row],
            new NoOpBulkService(),
            [new CategoryOptionDto(2, "Đồ uống", 0)]);

        var expectedCurrent = new Dictionary<BulkProductOperationType, string>
        {
            [BulkProductOperationType.SetPrices] = "Giá bán: 20",
            [BulkProductOperationType.SetCategory] = "Đồ uống",
            [BulkProductOperationType.SetActiveState] = "Đang bán",
            [BulkProductOperationType.SetMinimumStock] = "5"
        };

        foreach (var operation in viewModel.Operations)
        {
            viewModel.SelectedOperation = operation;
            var projectedRow = Assert.Single(viewModel.PreviewRows);
            Assert.Contains(expectedCurrent[operation.Operation], projectedRow.BeforeValue, StringComparison.Ordinal);
            Assert.Equal("—", projectedRow.AfterValue);
            Assert.Equal("Chưa xem trước", projectedRow.ResultText);
        }
    }

    [Fact]
    public void No_op_preview_disables_confirmation_and_keeps_reference_rows_safe()
    {
        var row = new ProductRowViewModel(new ProductListItemDto(
            1, 2, "Đồ uống", "SP001", null, "Sữa", "Hộp",
            10, 20, 10, 3, 5, true, false, true, false, true));
        using var viewModel = new BulkProductViewModel(
            [row],
            new ImmediatePreviewService(changeCount: 0),
            [new CategoryOptionDto(2, "Đồ uống", 0)]);

        viewModel.SalePriceText = "100";
        viewModel.CostPriceText = "200";
        viewModel.PreviewCommand.Execute(null);

        Assert.True(viewModel.HasPreview);
        Assert.False(viewModel.HasChanges);
        Assert.False(viewModel.ConfirmCommand.CanExecute(null));
        var previewRow = Assert.Single(viewModel.PreviewRows);
        Assert.Equal("Không đổi", previewRow.ResultText);
    }

    private sealed class NoOpBulkService : IBulkProductOperationService
    {
        public Task<Result<BulkProductPreview>> PreviewAsync(BulkProductOperationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<BulkProductOperationResult>> CommitAsync(BulkProductPreview preview, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ImmediatePreviewService(int changeCount) : IBulkProductOperationService
    {
        public Task<Result<BulkProductPreview>> PreviewAsync(BulkProductOperationRequest request, CancellationToken cancellationToken = default)
        {
            var row = new BulkProductPreviewRow(1, "SP001", "Sữa", "100", "200", changeCount > 0, null);
            return Task.FromResult(Result.Success(new BulkProductPreview(Guid.NewGuid(), request, [row], changeCount, changeCount == 0 ? 1 : 0, true, [])));
        }

        public Task<Result<BulkProductOperationResult>> CommitAsync(BulkProductPreview preview, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new BulkProductOperationResult(preview.PreviewId, true, preview.Request.Selection.Count, preview.ChangeCount, preview.NoOpCount, [])));
    }
}
