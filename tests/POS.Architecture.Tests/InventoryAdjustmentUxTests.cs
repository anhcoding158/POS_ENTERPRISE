using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Inventory;
using POS.Application.DTOs.Products;
using POS.Domain.Enums;
using POS.Wpf.Commands;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class InventoryAdjustmentUxTests
{
    [Fact]
    public async Task Quantity_starts_empty_and_preview_does_not_assume_one()
    {
        var service = new FakeInventoryService();
        var viewModel = CreateViewModel(service);

        Assert.True(await viewModel.InitializeAsync(1));

        Assert.Equal(string.Empty, viewModel.QuantityText);
        Assert.Equal("—", viewModel.PreviewAfterText);
        Assert.Equal("Chưa nhập số lượng", viewModel.PreviewDeltaText);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Quantity_preview_updates_and_clearing_it_never_writes()
    {
        var service = new FakeInventoryService();
        var viewModel = CreateViewModel(service);
        await viewModel.InitializeAsync(1);

        viewModel.QuantityText = "5";
        Assert.Equal("17 Cái", viewModel.PreviewAfterText);

        viewModel.QuantityText = string.Empty;
        Assert.Equal("—", viewModel.PreviewAfterText);
        Assert.Equal("Chưa nhập số lượng", viewModel.PreviewStateText);
        Assert.Empty(service.Adjustments);
    }

    [Fact]
    public async Task Changing_movement_requires_entering_quantity_again()
    {
        var service = new FakeInventoryService();
        var viewModel = CreateViewModel(service);
        await viewModel.InitializeAsync(1);

        viewModel.QuantityText = "5";
        viewModel.SelectedMovement = viewModel.MovementOptions.Single(
            option => option.Value == InventoryMovementType.Stocktake);

        Assert.Equal(string.Empty, viewModel.QuantityText);
        Assert.Equal("Nhập số tồn thực tế", viewModel.QuantityPlaceholderText);
        Assert.Equal("—", viewModel.PreviewAfterText);

        viewModel.QuantityText = "0";
        Assert.Equal("0 Cái", viewModel.PreviewAfterText);
        Assert.Equal("-12 Cái", viewModel.PreviewDeltaText);
    }

    [Fact]
    public async Task Save_button_requires_valid_quantity_and_saves_once_when_confirmed()
    {
        var service = new FakeInventoryService();
        var viewModel = CreateViewModel(service);
        await viewModel.InitializeAsync(1);

        viewModel.Reason = "Kiểm tra số lượng";
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        viewModel.QuantityText = "abc";
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        viewModel.QuantityText = "5";
        Assert.True(viewModel.SaveCommand.CanExecute(null));

        viewModel.SaveCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SaveCommand);

        var request = Assert.Single(service.Adjustments);
        Assert.Equal(5, request.Quantity);
        Assert.Equal(InventoryMovementType.StockIn, request.MovementType);
    }

    [Fact]
    public void Adjustment_window_uses_a_placeholder_and_no_default_quantity()
    {
        var source = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "POS.Wpf", "Views", "InventoryAdjustmentWindow.xaml"));

        Assert.Contains("QuantityPlaceholderText", source, StringComparison.Ordinal);
        Assert.Contains("ShowQuantityPlaceholder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"1\"", source, StringComparison.Ordinal);
    }

    private static InventoryAdjustmentViewModel CreateViewModel(
        FakeInventoryService service)
    {
        return new InventoryAdjustmentViewModel(
            new FakeScopeFactory(service),
            NullLogger<InventoryAdjustmentViewModel>.Instance);
    }

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        for (var index = 0; index < 100 && command.IsExecuting; index++)
            await Task.Delay(10);
    }

    private sealed class FakeInventoryService : IInventoryService
    {
        public List<InventoryAdjustmentRequest> Adjustments { get; } = [];

        public Task<Result<InventoryAdjustmentResultDto>> AdjustAsync(
            InventoryAdjustmentRequest request,
            CancellationToken cancellationToken = default)
        {
            Adjustments.Add(request);
            return Task.FromResult(Result.Success(new InventoryAdjustmentResultDto(
                MovementId: 1,
                ProductId: request.ProductId,
                ProductCode: "MILK-001",
                ProductName: "Sữa tươi",
                UnitName: "Cái",
                MovementType: request.MovementType,
                QuantityBefore: 12,
                QuantityDelta: request.MovementType == InventoryMovementType.StockIn
                    ? request.Quantity
                    : request.Quantity,
                QuantityAfter: request.MovementType == InventoryMovementType.StockIn
                    ? 12 + request.Quantity
                    : request.Quantity,
                Reason: request.Reason,
                ReferenceType: request.ReferenceType,
                ReferenceId: request.ReferenceId,
                PerformedByUserId: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow)));
        }

        public Task<Result<PagedResult<InventoryMovementDto>>> SearchAsync(
            InventorySearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result.Failure<PagedResult<InventoryMovementDto>>(
                    new AppError("NOT_USED", "Not used.")));

        public Task<Result<InventoryMovementSummaryDto>> GetHistorySummaryAsync(
            InventorySearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result.Success(new InventoryMovementSummaryDto(0, 0, 0, 0)));
    }

    private sealed class FakeProductService : IProductService
    {
        public Task<Result<ProductDetailsDto>> GetByIdAsync(
            int productId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new ProductDetailsDto(
                Id: productId,
                CategoryId: 1,
                CategoryName: "Đồ uống",
                Code: "MILK-001",
                Barcode: "0000000012",
                Name: "Sữa tươi",
                Description: null,
                UnitName: "Cái",
                ImagePath: null,
                CostPrice: 10_000,
                SalePrice: 15_000,
                ProfitPerUnit: 5_000,
                StockQuantity: 12,
                MinimumStock: 2,
                TrackInventory: true,
                AllowNegativeStock: false,
                IsLowStock: false,
                IsOutOfStock: false,
                IsActive: true,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                UpdatedAtUtc: DateTimeOffset.UtcNow)));

        public Task<Result<PagedResult<ProductListItemDto>>> SearchAsync(
            ProductSearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result.Failure<PagedResult<ProductListItemDto>>(
                    new AppError("NOT_USED", "Not used.")));

        public Task<Result<ProductDetailsDto>> CreateAsync(
            CreateProductRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<ProductDetailsDto>(
                new AppError("NOT_USED", "Not used.")));

        public Task<Result<ProductDetailsDto>> UpdateAsync(
            UpdateProductRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<ProductDetailsDto>(
                new AppError("NOT_USED", "Not used.")));

        public Task<Result> SetActiveStateAsync(
            int productId,
            bool isActive,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure(
                new AppError("NOT_USED", "Not used.")));

        public Task<Result> ArchiveAsync(
            int productId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure(
                new AppError("NOT_USED", "Not used.")));

        public Task<Result> RestoreAsync(
            int productId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure(
                new AppError("NOT_USED", "Not used.")));
    }

    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly IInventoryService _inventoryService;

        public FakeScopeFactory(IInventoryService inventoryService) =>
            _inventoryService = inventoryService;

        public IServiceScope CreateScope() =>
            new FakeScope(_inventoryService);
    }

    private sealed class FakeScope : IServiceScope
    {
        public FakeScope(IInventoryService inventoryService)
        {
            ServiceProvider = new FakeServiceProvider(inventoryService);
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly IInventoryService _inventoryService;
        private readonly FakeProductService _productService = new();

        public FakeServiceProvider(IInventoryService inventoryService) =>
            _inventoryService = inventoryService;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IInventoryService)
                ? _inventoryService
                : serviceType == typeof(IProductService)
                    ? _productService
                    : null;
    }
}
