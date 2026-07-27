using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Products;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductArchivingUiContractTests
{
    [Fact]
    public async Task Status_filter_must_map_archived_to_is_archived_true()
    {
        var context = CreateContext();

        await context.ViewModel.InitializeAsync();

        context.ViewModel.SelectedProductStatusFilter =
            context.ViewModel.ProductStatusFilters[3];

        await WaitForIdleAsync(
            context.ViewModel);

        Assert.True(
            context.Service.LastSearchRequest?.IsArchived);
    }

    [Fact]
    public async Task Default_status_filter_must_exclude_archived()
    {
        var context = CreateContext();

        await context.ViewModel.InitializeAsync();

        Assert.False(
            context.Service.LastSearchRequest?.IsArchived);
    }

    [Fact]
    public void Product_row_must_display_archived_status()
    {
        var row =
            new ProductRowViewModel(
                CreateProduct(
                    isArchived: true));

        Assert.True(row.IsArchived);
        Assert.Equal(
            "Đã lưu trữ",
            row.StatusText);
    }

    [Fact]
    public async Task Archive_command_must_delegate_once()
    {
        var context = CreateContext();

        context.ViewModel.SelectedProduct =
            new ProductRowViewModel(
                CreateProduct(
                    isArchived: false));

        context.ViewModel.ToggleProductArchiveCommand
            .Execute(null);

        await WaitForIdleAsync(
            context.ViewModel);

        Assert.Equal(
            1,
            context.Service.ArchiveCallCount);

        Assert.Equal(
            0,
            context.Service.RestoreCallCount);
    }

    [Fact]
    public async Task Restore_command_must_delegate_once()
    {
        var context = CreateContext();

        context.ViewModel.SelectedProduct =
            new ProductRowViewModel(
                CreateProduct(
                    isArchived: true));

        context.ViewModel.ToggleProductArchiveCommand
            .Execute(null);

        await WaitForIdleAsync(
            context.ViewModel);

        Assert.Equal(
            0,
            context.Service.ArchiveCallCount);

        Assert.Equal(
            1,
            context.Service.RestoreCallCount);

        Assert.Equal(
            "Đã khôi phục sản phẩm. " +
            "Sản phẩm vẫn đang ở trạng thái ngừng bán.",
            context.ViewModel.StatusMessage);
    }

    [Fact]
    public void Archived_product_must_disable_edit_toggle_active_and_inventory_adjustment()
    {
        var context = CreateContext();

        context.ViewModel.SelectedProduct =
            new ProductRowViewModel(
                CreateProduct(
                    isArchived: true));

        Assert.False(
            context.ViewModel.EditProductCommand
                .CanExecute(null));

        Assert.False(
            context.ViewModel.ToggleProductActiveCommand
                .CanExecute(null));

        Assert.False(
            context.ViewModel.AdjustInventoryCommand
                .CanExecute(null));
    }

    [Fact]
    public void Archived_product_must_allow_inventory_history_and_restore()
    {
        var context = CreateContext();

        context.ViewModel.SelectedProduct =
            new ProductRowViewModel(
                CreateProduct(
                    isArchived: true));

        Assert.True(
            context.ViewModel.ViewInventoryHistoryCommand
                .CanExecute(null));

        Assert.True(
            context.ViewModel.ToggleProductArchiveCommand
                .CanExecute(null));

        Assert.Equal(
            "Khôi phục",
            context.ViewModel
                .ToggleProductArchiveButtonText);
    }

    private static TestContext CreateContext()
    {
        var service =
            new FakeProductService();

        var services =
            new ServiceCollection()
                .AddSingleton<IProductService>(
                    service)
                .BuildServiceProvider();

        var viewModel =
            new ShellViewModel(
                services.GetRequiredService<
                    IServiceScopeFactory>(),
                new FakeProductDialogService(),
                new FakeCategoryDialogService(),
                new FakeInventoryDialogService(),
                NullLogger<ShellViewModel>.Instance);

        return new TestContext(
            viewModel,
            service);
    }

    private static ProductListItemDto CreateProduct(
        bool isArchived)
    {
        return new ProductListItemDto(
            Id: 17,
            CategoryId: 3,
            CategoryName: "Danh mục",
            Code: "SP-017",
            Barcode: null,
            Name: "Sản phẩm kiểm thử",
            UnitName: "Cái",
            CostPrice: 10_000,
            SalePrice: 15_000,
            ProfitPerUnit: 5_000,
            StockQuantity: 8,
            MinimumStock: 2,
            TrackInventory: true,
            AllowNegativeStock: false,
            IsLowStock: false,
            IsOutOfStock: false,
            IsActive: false,
            IsArchived: isArchived);
    }

    private static async Task WaitForIdleAsync(
        ShellViewModel viewModel)
    {
        var timeout =
            DateTimeOffset.UtcNow.AddSeconds(5);

        while ((viewModel.IsLoading ||
                viewModel.ToggleProductArchiveCommand
                    .IsExecuting) &&
               DateTimeOffset.UtcNow < timeout)
        {
            await Task.Delay(10);
        }

        Assert.False(viewModel.IsLoading);
        Assert.False(
            viewModel.ToggleProductArchiveCommand
                .IsExecuting);
    }

    private sealed record TestContext(
        ShellViewModel ViewModel,
        FakeProductService Service);

    private sealed class FakeProductService :
        IProductService
    {
        public int ArchiveCallCount { get; private set; }

        public int RestoreCallCount { get; private set; }

        public ProductSearchRequest?
            LastSearchRequest
        { get; private set; }

        public Task<
            Result<PagedResult<ProductListItemDto>>>
            SearchAsync(
                ProductSearchRequest request,
                CancellationToken cancellationToken = default)
        {
            LastSearchRequest = request;

            return Task.FromResult(
                Result.Success(
                    PagedResult<
                        ProductListItemDto>.Empty(
                            request.PageNumber,
                            request.PageSize)));
        }

        public Task<Result> ArchiveAsync(
            int productId,
            CancellationToken cancellationToken = default)
        {
            ArchiveCallCount++;

            return Task.FromResult(
                Result.Success());
        }

        public Task<Result> RestoreAsync(
            int productId,
            CancellationToken cancellationToken = default)
        {
            RestoreCallCount++;

            return Task.FromResult(
                Result.Success());
        }

        public Task<Result<ProductDetailsDto>>
            GetByIdAsync(
                int productId,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<ProductDetailsDto>>
            CreateAsync(
                CreateProductRequest request,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<ProductDetailsDto>>
            UpdateAsync(
                UpdateProductRequest request,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetActiveStateAsync(
            int productId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeProductDialogService :
        IProductDialogService
    {
        public Task<bool> ShowCreateAsync()
        {
            return Task.FromResult(false);
        }

        public Task<bool> ShowEditAsync(
            int productId)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class FakeCategoryDialogService :
        ICategoryManagementDialogService
    {
        public Task ShowAsync()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInventoryDialogService :
        IInventoryDialogService
    {
        public Task<bool> ShowAdjustmentAsync(
            int productId)
        {
            return Task.FromResult(false);
        }

        public Task ShowHistoryAsync(
            int? productId = null)
        {
            return Task.CompletedTask;
        }
    }
}