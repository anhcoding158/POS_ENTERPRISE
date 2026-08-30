using System.Collections.Concurrent;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Inventory;
using POS.Domain.Enums;
using POS.Wpf.Commands;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class InventoryHistoryUxTests
{
    [Fact]
    public void Inventory_history_surface_uses_one_search_and_business_language()
    {
        var source = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "POS.Wpf", "Views", "InventoryHistoryWindow.xaml"));

        Assert.Contains("Tên, mã sản phẩm hoặc mã vạch", source, StringComparison.Ordinal);
        Assert.Contains("Text=\"Tìm sản phẩm\"", source, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"250\"", source, StringComparison.Ordinal);
        Assert.Contains("<UniformGrid Grid.Row=\"0\" Columns=\"3\"", source, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding SearchCommand}\"", source, StringComparison.Ordinal);
        Assert.Contains("Xóa bộ lọc", source, StringComparison.Ordinal);
        Assert.Contains("Làm mới", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Lọc lịch sử", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Đặt lại", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Tải lại", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AUDIT TRAIL", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Movement có delta", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Bỏ giới hạn sản phẩm", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Inventory_history_legacy_layout_must_keep_a_compact_sidebar_and_safe_table()
    {
        var source = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "POS.Wpf", "Views", "InventoryHistoryWindow.xaml"));

        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility\" Value=\"Auto\"", source, StringComparison.Ordinal);
        Assert.Contains("GridLinesVisibility\" Value=\"All\"", source, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment\" Value=\"Stretch\"", source, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", source, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding ReferenceText}\"", source, StringComparison.Ordinal);
        Assert.Contains("Đóng chi tiết", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Đang giới hạn sản phẩm đã chọn", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_term_is_sent_to_history_query_after_debounce()
    {
        var service = new FakeInventoryService();
        using var viewModel = CreateViewModel(service);

        Assert.True(await viewModel.InitializeAsync(null));
        service.Requests.Clear();

        viewModel.ProductSearchTerm = "  Sữa tươi  ";
        await Task.Delay(450);

        var request = Assert.Single(service.Requests);
        Assert.Equal("Sữa tươi", request.ProductSearchTerm);
        Assert.Null(request.ProductId);
        Assert.True(viewModel.HasMovements);
    }

    [Fact]
    public async Task Enter_applies_search_immediately_and_clear_search_runs_once()
    {
        var service = new FakeInventoryService();
        using var viewModel = CreateViewModel(service);
        await viewModel.InitializeAsync(null);
        service.Requests.Clear();

        viewModel.ProductSearchTerm = "MILK-001";
        viewModel.ApplyFiltersCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ApplyFiltersCommand);

        Assert.Single(service.Requests);
        Assert.Equal("MILK-001", service.Requests.ToArray()[0].ProductSearchTerm);

        service.Requests.Clear();
        viewModel.ClearSearchCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ClearSearchCommand);

        Assert.Single(service.Requests);
        Assert.Null(service.Requests.ToArray()[0].ProductSearchTerm);
        Assert.False(viewModel.HasSearchTerm);
    }

    [Fact]
    public async Task Clear_filters_removes_navigation_scope_without_selecting_first_product()
    {
        var service = new FakeInventoryService();
        using var viewModel = CreateViewModel(service);
        await viewModel.InitializeAsync("MILK-001", "Sữa tươi · MILK-001");
        service.Requests.Clear();

        viewModel.ClearFiltersCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ClearFiltersCommand);

        var request = Assert.Single(service.Requests);
        Assert.Null(request.ProductId);
        Assert.Null(request.ProductSearchTerm);
        Assert.False(viewModel.HasInitialProductContext);
    }

    [Fact]
    public async Task Product_navigation_uses_visible_search_criterion_without_product_id_scope()
    {
        var service = new FakeInventoryService();
        using var viewModel = CreateViewModel(service);

        await viewModel.InitializeAsync("MILK-001", "Sữa tươi · MILK-001");

        var initialRequest = service.Requests.Last();
        Assert.Null(initialRequest.ProductId);
        Assert.Equal("MILK-001", initialRequest.ProductSearchTerm);
        Assert.True(viewModel.HasInitialProductContext);
        Assert.Contains("Sữa tươi · MILK-001", viewModel.InitialProductContextText, StringComparison.Ordinal);

        service.Requests.Clear();
        viewModel.ProductSearchTerm = "BREAD-002";
        await Task.Delay(450);

        var changedRequest = Assert.Single(service.Requests);
        Assert.Null(changedRequest.ProductId);
        Assert.Equal("BREAD-002", changedRequest.ProductSearchTerm);
        Assert.False(viewModel.HasInitialProductContext);
    }

    [Fact]
    public async Task Refresh_keeps_search_and_current_page_request()
    {
        var service = new FakeInventoryService
        {
            Page = new PagedResult<InventoryMovementDto>(
                [CreateMovement(1, "MILK-001", "Sữa tươi")],
                pageNumber: 1,
                pageSize: 30,
                totalCount: 31)
        };
        using var viewModel = CreateViewModel(service);
        await viewModel.InitializeAsync(null);
        viewModel.ProductSearchTerm = "MILK";
        await Task.Delay(450);
        service.Requests.Clear();

        viewModel.NextPageCommand.Execute(null);
        await WaitForCommandAsync(viewModel.NextPageCommand);
        service.Requests.Clear();

        viewModel.RefreshCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RefreshCommand);

        var request = Assert.Single(service.Requests);
        Assert.Equal("MILK", request.ProductSearchTerm);
        Assert.Equal(2, request.PageNumber);
    }

    [Fact]
    public async Task Latest_search_response_wins_when_older_response_returns_late()
    {
        var service = new FakeInventoryService
        {
            SearchHandler = async (request, cancellationToken) =>
            {
                if (request.ProductSearchTerm == "A")
                    await Task.Delay(650, CancellationToken.None);

                return Result.Success(SuccessPage(
                    request.ProductSearchTerm == "B"
                        ? CreateMovement(2, "B-002", "Bánh quy")
                        : CreateMovement(1, "A-001", "Áo dụng cụ")));
            }
        };
        using var viewModel = CreateViewModel(service);
        await viewModel.InitializeAsync(null);

        viewModel.ProductSearchTerm = "A";
        await Task.Delay(360);
        viewModel.ProductSearchTerm = "B";
        await Task.Delay(1000);

        var movement = Assert.Single(viewModel.Movements);
        Assert.Equal("B-002", movement.ProductCode);
    }

    [Fact]
    public async Task Invalid_date_range_does_not_query_and_does_not_show_old_rows()
    {
        var service = new FakeInventoryService();
        using var viewModel = CreateViewModel(service);
        await viewModel.InitializeAsync(null);
        service.Requests.Clear();

        viewModel.FromDate = DateTime.Today.AddDays(1);
        viewModel.ToDate = DateTime.Today;
        await Task.Delay(450);

        Assert.Empty(service.Requests);
        Assert.True(viewModel.HasDateRangeError);
        Assert.Contains("không được lớn hơn", viewModel.DateRangeError, StringComparison.Ordinal);
        Assert.Empty(viewModel.Movements);
        Assert.True(viewModel.HasError);
        Assert.False(viewModel.ShowEmptyState);
    }

    [Fact]
    public async Task Empty_and_failure_states_are_not_conflated()
    {
        var service = new FakeInventoryService();
        using var viewModel = CreateViewModel(service);
        await viewModel.InitializeAsync(null);

        service.SearchHandler = (_, _) => Task.FromResult(
            Result.Failure<PagedResult<InventoryMovementDto>>(
                new AppError("TEST_FAILURE", "Synthetic failure.")));
        viewModel.ApplyFiltersCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ApplyFiltersCommand);
        Assert.True(viewModel.HasError);
        Assert.False(viewModel.ShowEmptyState);

        service.SearchHandler = (_, _) => Task.FromResult(Result.Success(SuccessPage()));
        viewModel.ApplyFiltersCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ApplyFiltersCommand);
        Assert.False(viewModel.HasError);
        Assert.True(viewModel.ShowEmptyState);
        Assert.Contains("Chưa có lịch sử tồn kho", viewModel.EmptyStateText, StringComparison.Ordinal);
    }

    [Fact]
    public void Inventory_history_window_constructs_at_supported_measure_sizes()
    {
        RunOnSta(() =>
        {
            if (global::System.Windows.Application.Current is null)
            {
                var application = new POS.Wpf.App();
                application.InitializeComponent();
            }
            using var viewModel = CreateViewModel(new FakeInventoryService());
            var window = new InventoryHistoryWindow(viewModel);
            foreach (var size in new[]
            {
                 new Size(1920, 1080),
                 new Size(1366, 768),
                 new Size(1280, 720),
                 new Size(1180, 720),
                 new Size(1000, 620),
                 new Size(1000, 640)
            })
            {
                window.Measure(size);
                window.Arrange(new Rect(new Point(), size));
                Assert.True(window.ActualWidth <= size.Width || window.Width <= size.Width);
                Assert.True(window.ActualHeight <= size.Height || window.Height <= size.Height);
            }

            window.Close();
        });
    }

    private static InventoryHistoryViewModel CreateViewModel(FakeInventoryService service)
    {
        var factory = new FakeScopeFactory(service);
        return new InventoryHistoryViewModel(
            factory,
            NullLogger<InventoryHistoryViewModel>.Instance);
    }

    private static PagedResult<InventoryMovementDto> SuccessPage(
        InventoryMovementDto? movement = null) =>
        new(
            movement is null ? [] : [movement],
            pageNumber: 1,
            pageSize: 30,
            totalCount: movement is null ? 0 : 1);

    private static InventoryMovementDto CreateMovement(
        int id,
        string code,
        string name) =>
        new(
            id,
            id,
            code,
            name,
            "Cái",
            InventoryMovementType.StockIn,
            0,
            1,
            1,
            "Kiểm thử",
            null,
            null,
            null,
            DateTimeOffset.UtcNow);

    private static async Task WaitForCommandAsync(AsyncRelayCommand command)
    {
        for (var index = 0; index < 100 && command.IsExecuting; index++)
            await Task.Delay(10);
    }

    private static void RunOnSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        completion.Task.GetAwaiter().GetResult();
    }

    private sealed class FakeInventoryService : IInventoryService
    {
        public ConcurrentBag<InventorySearchRequest> Requests { get; } = [];
        public PagedResult<InventoryMovementDto> Page { get; init; } = SuccessPage(CreateMovement(1, "INIT-001", "Sản phẩm"));
        public InventoryMovementSummaryDto Summary { get; init; } = new(1, 1, 0, 0);
        public Func<InventorySearchRequest, CancellationToken, Task<Result<PagedResult<InventoryMovementDto>>>>? SearchHandler { get; set; }

        public Task<Result<PagedResult<InventoryMovementDto>>> SearchAsync(InventorySearchRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return SearchHandler is null
                ? Task.FromResult(Result.Success(new PagedResult<InventoryMovementDto>(
                    Page.Items,
                    request.PageNumber,
                    request.PageSize,
                    Page.TotalCount)))
                : SearchHandler(request, cancellationToken);
        }

        public Task<Result<InventoryAdjustmentResultDto>> AdjustAsync(InventoryAdjustmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<InventoryAdjustmentResultDto>(new AppError("NOT_USED", "Not used.")));

        public Task<Result<InventoryMovementSummaryDto>> GetHistorySummaryAsync(
            InventorySearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(Summary));
    }

    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly IInventoryService _service;

        public FakeScopeFactory(IInventoryService service) => _service = service;

        public IServiceScope CreateScope() => new FakeScope(_service);
    }

    private sealed class FakeScope : IServiceScope
    {
        public FakeScope(IInventoryService service) => ServiceProvider = new FakeServiceProvider(service);
        public IServiceProvider ServiceProvider { get; }
        public void Dispose() { }
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly IInventoryService _service;

        public FakeServiceProvider(IInventoryService service) => _service = service;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IInventoryService) ? _service : null;
    }
}
