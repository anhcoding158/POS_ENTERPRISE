using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Products;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductArchivingUiContractTests
{
    [Fact]
    public void Product_list_layout_must_keep_search_geometry_and_column_rhythm_stable()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "POS.Wpf",
                "Views",
                "ShellWindow.xaml"));

        Assert.Contains("Width=\"380\"", source, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"320\"", source, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"400\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=\"520\"", source, StringComparison.Ordinal);
        Assert.Contains("GridLinesVisibility=\"All\"", source, StringComparison.Ordinal);
        Assert.Contains(
            "VerticalGridLinesBrush=\"{StaticResource BorderBrush}\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "BasedOn=\"{StaticResource LeftAlignedTableTextStyle}\"",
            source,
            StringComparison.Ordinal);
        Assert.Equal(
            4,
            source.Split("BorderThickness=\"0,0,1,0\"").Length - 1);

        foreach (var width in new[] { "2.6*", "1.4*", "1*", "1.15*" })
        {
            Assert.Contains($"Width=\"{width}\"", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Product_more_actions_entry_point_is_labeled_and_keeps_commands()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "POS.Wpf",
                "Views",
                "ShellWindow.xaml"));

        Assert.Contains("Kho &amp; lưu trữ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Thao tác khác", source, StringComparison.Ordinal);
        Assert.Contains(
            "Điều chỉnh tồn, lưu trữ hoặc khôi phục sản phẩm",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"⋮\"", source, StringComparison.Ordinal);
        Assert.Contains("AdjustInventoryCommand", source, StringComparison.Ordinal);
        Assert.Contains("ViewInventoryHistoryCommand", source, StringComparison.Ordinal);
        Assert.Contains("ShellInventoryHistoryNavigationButton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"InventoryHistoryMenuItem\"", source, StringComparison.Ordinal);
        Assert.Contains("ClearSelectedProductButton", source, StringComparison.Ordinal);
        Assert.Contains("Content=\"× Bỏ chọn\"", source, StringComparison.Ordinal);
        Assert.Contains("ClearSelectedProductCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenInventoryHistoryCommand", source, StringComparison.Ordinal);
        Assert.Contains("OnToggleProductArchiveClick", source, StringComparison.Ordinal);
    }

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

    [Fact]
    public async Task Global_inventory_history_does_not_inherit_selected_product_scope()
    {
        var context = CreateContext();
        context.ViewModel.SelectedProduct =
            new ProductRowViewModel(CreateProduct(isArchived: false));

        context.ViewModel.ViewInventoryHistoryCommand.Execute(null);
        await WaitForHistoryAsync(context.ViewModel);

        Assert.Null(context.InventoryDialog.LastHistorySearchTerm);
        Assert.Null(context.InventoryDialog.LastHistoryDisplayText);
    }

    [Fact]
    public async Task Clear_selected_product_clears_real_selection_without_reloading_or_mutating_data()
    {
        var context = CreateContext();
        context.ViewModel.SelectedProduct =
            new ProductRowViewModel(CreateProduct(isArchived: false));

        context.ViewModel.ClearSelectedProductCommand.Execute(null);
        await WaitForClearSelectionAsync(context.ViewModel);

        Assert.Null(context.ViewModel.SelectedProduct);
        Assert.False(context.ViewModel.HasSelectedProduct);
        Assert.Equal(0, context.Service.SearchCalls);
        Assert.False(context.ViewModel.EditProductCommand.CanExecute(null));
        Assert.False(context.ViewModel.AdjustInventoryCommand.CanExecute(null));
        Assert.False(context.ViewModel.ToggleProductArchiveCommand.CanExecute(null));
        Assert.True(context.ViewModel.ViewInventoryHistoryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Bulk_mode_must_not_leave_single_product_commands_active()
    {
        var context = CreateContext();
        context.ViewModel.SelectedProduct =
            new ProductRowViewModel(CreateProduct(isArchived: false));

        context.ViewModel.ToggleBulkSelectionCommand.Execute(null);
        while (context.ViewModel.ToggleBulkSelectionCommand.IsExecuting)
            await Task.Delay(1);

        Assert.True(context.ViewModel.IsBulkSelectionMode);
        Assert.False(context.ViewModel.ShowSingleProductContext);
        Assert.False(context.ViewModel.EditProductCommand.CanExecute(null));
        Assert.False(context.ViewModel.ToggleProductActiveCommand.CanExecute(null));
        Assert.False(context.ViewModel.AdjustInventoryCommand.CanExecute(null));
        Assert.False(context.ViewModel.ToggleProductArchiveCommand.CanExecute(null));
        Assert.False(context.ViewModel.ClearSelectedProductCommand.CanExecute(null));
    }

    [Fact]
    public async Task Global_inventory_history_is_available_without_a_product_selection()
    {
        var context = CreateContext();

        Assert.True(context.ViewModel.ViewInventoryHistoryCommand.CanExecute(null));

        context.ViewModel.ViewInventoryHistoryCommand.Execute(null);
        await WaitForHistoryAsync(context.ViewModel);

        Assert.Null(context.InventoryDialog.LastHistorySearchTerm);
        Assert.Null(context.InventoryDialog.LastHistoryDisplayText);
    }

    private static TestContext CreateContext()
    {
        var service =
            new FakeProductService();
        var inventoryDialog =
            new FakeInventoryDialogService();

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
                inventoryDialog,
                new FakeOrderHistoryWindowService(),
                new AllowAllPermissionService(),
                NullLogger<ShellViewModel>.Instance);

        return new TestContext(
            viewModel,
            service,
            inventoryDialog);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }

    private sealed class FakeOrderHistoryWindowService :
        IOrderHistoryWindowService
    {
        public Task ShowAsync() =>
            Task.CompletedTask;
    }

    private sealed class AllowAllPermissionService :
        IPermissionService
    {
        public bool HasPermission(
            SystemCapability permission) =>
            true;

        public Result Authorize(
            SystemCapability permission) =>
            Result.Success();
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
        FakeProductService Service,
        FakeInventoryDialogService InventoryDialog);

    private sealed class FakeProductService :
        IProductService
    {
        public int ArchiveCallCount { get; private set; }

        public int RestoreCallCount { get; private set; }

        public int SearchCalls { get; private set; }

        public ProductSearchRequest?
            LastSearchRequest
        { get; private set; }

        public Task<
            Result<PagedResult<ProductListItemDto>>>
            SearchAsync(
                ProductSearchRequest request,
                CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            LastSearchRequest = request;

            return Task.FromResult(
                Result.Success(
                    PagedResult.Empty<
                        ProductListItemDto>(
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
        public string? LastHistorySearchTerm { get; private set; }

        public string? LastHistoryDisplayText { get; private set; }

        public Task<bool> ShowAdjustmentAsync(
            int productId)
        {
            return Task.FromResult(false);
        }

        public Task ShowHistoryAsync(
            string? productSearchTerm = null,
            string? productDisplayText = null)
        {
            LastHistorySearchTerm = productSearchTerm;
            LastHistoryDisplayText = productDisplayText;
            return Task.CompletedTask;
        }
    }

    private static async Task WaitForHistoryAsync(ShellViewModel viewModel)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(5);
        while (viewModel.ViewInventoryHistoryCommand.IsExecuting &&
               DateTimeOffset.UtcNow < timeout)
        {
            await Task.Delay(10);
        }

        Assert.False(viewModel.ViewInventoryHistoryCommand.IsExecuting);
    }

    private static async Task WaitForClearSelectionAsync(ShellViewModel viewModel)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(5);
        while (viewModel.ClearSelectedProductCommand.IsExecuting &&
               DateTimeOffset.UtcNow < timeout)
        {
            await Task.Delay(10);
        }

        Assert.False(viewModel.ClearSelectedProductCommand.IsExecuting);
    }
}
