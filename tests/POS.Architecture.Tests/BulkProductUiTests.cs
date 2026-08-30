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
    }

    private sealed class NoOpBulkService : IBulkProductOperationService
    {
        public Task<Result<BulkProductPreview>> PreviewAsync(BulkProductOperationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<BulkProductOperationResult>> CommitAsync(BulkProductPreview preview, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
