using System.Reflection;
using System.Globalization;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Products;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Wpf;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ShellSidebarInventoryHotfixTests
{
    [Fact]
    public void Sidebar_contract_has_final_labels_single_chevrons_and_fixed_footer()
    {
        var root = SolutionRoot();
        var shellPath = Path.Combine(root, "src", "POS.Wpf", "Views", "ShellWindow.xaml");
        var controlsPath = Path.Combine(root, "src", "POS.Wpf", "Themes", "Controls.xaml");
        var vietQrPath = Path.Combine(root, "src", "POS.Wpf", "Views", "ShellWindow.VietQr.cs");
        var shellText = File.ReadAllText(shellPath);
        var controlsText = File.ReadAllText(controlsPath);
        var vietQrText = File.ReadAllText(vietQrPath);
        var document = XDocument.Load(shellPath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var displayedText = document.Descendants()
            .Attributes("Text")
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var label in new[]
        {
            "Tổng quan", "Bán hàng", "Hàng hóa", "Sản phẩm & tồn kho",
            "Danh mục sản phẩm", "Lịch sử kho", "Đơn hàng", "Lịch sử đơn hàng",
            "Thanh toán QR", "Quản lý cửa hàng", "Nhân viên & tài khoản",
            "Cài đặt cửa hàng", "Dữ liệu & hỗ trợ", "Sao lưu dữ liệu",
            "Khôi phục dữ liệu", "Dung lượng", "Gói hỗ trợ"
        })
            Assert.Contains(label, displayedText);

        Assert.DoesNotContain("Đơn hàng và khách hàng", displayedText);
        Assert.DoesNotContain("⌄", displayedText);
        Assert.Contains("x:Name=\"Chevron\"", controlsText, StringComparison.Ordinal);
        Assert.Contains("ShellNavigationChildButtonStyle", controlsText, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility=\"Auto\"", shellText, StringComparison.Ordinal);
        Assert.Contains("Cấu hình VietQR", vietQrText, StringComparison.Ordinal);
        Assert.Contains("Màn hình VietQR", vietQrText, StringComparison.Ordinal);
        Assert.Contains("In mã vạch / tem giá…", shellText, StringComparison.Ordinal);
        Assert.Contains(nameof(ShellViewModel.PrintProductLabelsCommand), shellText, StringComparison.Ordinal);

        var footer = document.Descendants(presentation + "StackPanel")
            .Single(element => (string?)element.Attribute(x + "Name") == "ShellStatusFooter");
        Assert.DoesNotContain(
            footer.Ancestors(),
            ancestor => ancestor.Name == presentation + "ScrollViewer");

        foreach (var automationId in new[]
        {
            "ShellOverviewNavigationButton", "ShellSalesNavigationButton",
            "ShellInventoryGroup", "ShellProductsNavigationButton",
            "ShellCategoriesNavigationButton", "ShellInventoryHistoryNavigationButton", "ShellOrdersGroup",
            "ShellOrderHistoryNavigationButton", "ShellQrGroup",
            "ShellManagementGroup", "EmployeeManagementNavigationButton",
            "StoreSettingsNavigationButton", "ShellDataSupportGroup",
            "ShellBackupNavigationButton", "RestoreDataNavigationButton",
            "StorageStatusNavigationButton", "ShellSupportNavigationButton"
        })
            Assert.Contains(automationId, shellText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Category_and_product_routes_switch_repeatedly_without_duplicate_product_queries()
    {
        var productService = new CountingProductService();
        var categoryService = new CountingCategoryDialogService();
        using var services = new ServiceCollection()
            .AddSingleton<IProductService>(productService)
            .BuildServiceProvider();
        var viewModel = CreateViewModel(services, categoryService);

        await viewModel.InitializeAsync();
        Assert.Equal(1, productService.SearchCalls);
        Assert.Equal(ShellRoute.Products, viewModel.ActiveRoute);
        Assert.True(viewModel.IsInventoryExpanded);

        viewModel.NavigateToOverviewCommand.Execute(null);
        await WaitForIdleAsync(viewModel.NavigateToOverviewCommand);
        Assert.Equal(ShellRoute.Overview, viewModel.ActiveRoute);

        viewModel.NavigateToProductsCommand.Execute(null);
        await WaitForIdleAsync(viewModel.NavigateToProductsCommand);
        Assert.Equal(ShellRoute.Products, viewModel.ActiveRoute);
        Assert.True(viewModel.IsInventoryExpanded);
        Assert.Equal(1, productService.SearchCalls);

        for (var transition = 0; transition < 3; transition++)
        {
            viewModel.OpenCategoryManagementCommand.Execute(null);
            await WaitForIdleAsync(viewModel.OpenCategoryManagementCommand);
            Assert.Equal(ShellRoute.Categories, viewModel.ActiveRoute);
            Assert.True(viewModel.IsInventoryExpanded);

            var expectedQueries = transition + 2;
            Assert.Equal(expectedQueries, productService.SearchCalls);

            viewModel.NavigateToProductsCommand.Execute(null);
            await WaitForIdleAsync(viewModel.NavigateToProductsCommand);
            Assert.Equal(ShellRoute.Products, viewModel.ActiveRoute);
            Assert.True(viewModel.IsInventoryExpanded);
            Assert.Equal(expectedQueries, productService.SearchCalls);
        }

        viewModel.NavigateToProductsCommand.Execute(null);
        await WaitForIdleAsync(viewModel.NavigateToProductsCommand);
        Assert.Equal(4, productService.SearchCalls);
        Assert.Equal(3, categoryService.ShowCalls);
    }

    [Fact]
    public void Responsive_sidebar_uses_one_expanded_group_and_preserves_a_usable_content_width()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var viewModel = CreateViewModel(
            services,
            new CountingCategoryDialogService());

        viewModel.UpdateViewportWidth(1366);
        Assert.False(viewModel.IsSidebarCompact);
        Assert.True(viewModel.IsSidebarExpanded);
        Assert.Equal(276d, viewModel.SidebarWidth);
        Assert.True(1366d - viewModel.SidebarWidth >= 1000d);

        viewModel.IsManagementExpanded = true;
        Assert.True(viewModel.IsManagementExpanded);
        Assert.False(viewModel.IsInventoryExpanded);

        viewModel.NavigateToProductsCommand.Execute(null);
        Assert.True(viewModel.IsInventoryExpanded);
        Assert.False(viewModel.IsManagementExpanded);

        viewModel.UpdateViewportWidth(1024);
        Assert.True(viewModel.IsSidebarCompact);
        Assert.False(viewModel.IsSidebarExpanded);
        Assert.Equal(76d, viewModel.SidebarWidth);
        Assert.True(1024d - viewModel.SidebarWidth >= 900d);
    }

    [Fact]
    public async Task Real_production_shell_loads_sidebar_bindings_and_inventory_data_on_an_isolated_database()
    {
        var root = SolutionRoot();
        var scenarioRoot = Path.Combine(
            Path.GetTempPath(),
            "POS-Enterprise-Sidebar-Inventory-Test-" + Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(scenarioRoot, "pos-enterprise-isolated.db");
        Directory.CreateDirectory(scenarioRoot);
        await PortableDevelopmentDatabase.CreateMigratedAsync(databasePath);

        try
        {
            await RunOnStaAsync(() =>
            {
                if (global::System.Windows.Application.Current is null)
                {
                    var application = new App();
                    application.InitializeComponent();
                }

                var services = new ServiceCollection();
                services.AddLogging();
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Path.Combine(root, "src", "POS.Wpf"))
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Infrastructure:DatabasePath"] = databasePath,
                        ["Infrastructure:SeedDemoProductCatalog"] = bool.TrueString,
                        ["Infrastructure:SeedDefaultAdministrator"] = bool.FalseString
                    })
                    .Build();

                typeof(App).GetMethod(
                        "ConfigureApplicationServices",
                        BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [services, configuration]);

                using var provider = services.BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });
                using var scope = provider.CreateScope();
                scope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
                    .InitializeAsync().GetAwaiter().GetResult();

                provider.GetRequiredService<ICurrentUserService>().SetCurrentUser(
                    new AuthenticatedUserDto(
                        1,
                        "isolated-admin",
                        "Isolated Administrator",
                        Role.Administrator,
                        DateTimeOffset.UtcNow,
                        forcePasswordChange: false));

                var window = scope.ServiceProvider.GetRequiredService<ShellWindow>();
                var viewModel = Assert.IsType<ShellViewModel>(window.DataContext);
                window.Show();

                window.WindowState = WindowState.Normal;
                window.Width = 1366;
                window.Height = 768;
                window.Measure(new Size(1366, 768));
                window.Arrange(new Rect(0, 0, 1366, 768));
                window.UpdateLayout();

                var productsButton = NamedField<Button>(window, "ShellProductsNavigationButton");
                var categoriesButton = NamedField<Button>(window, "ShellCategoriesNavigationButton");
                var inventoryGroup = NamedField<Expander>(window, "ShellInventoryGroup");
            var grid = NamedField<DataGrid>(window, "ProductInventoryGrid");
            var cards = NamedField<UniformGrid>(window, "InventorySummaryCards");
            var bulkContextText = NamedField<TextBlock>(window, "BulkSelectionContextText");
            var bulkOperationButton = NamedField<Button>(window, "BulkOperationButton");
            var exitBulkSelectionButton = NamedField<Button>(window, "ExitBulkSelectionButton");
            var historyButton = NamedField<Button>(window, "ShellInventoryHistoryNavigationButton");

                Assert.Equal(
                    nameof(ShellViewModel.NavigateToProductsCommand),
                    BindingOperations.GetBinding(productsButton, Button.CommandProperty)?.Path.Path);
                Assert.Equal(
                    nameof(ShellViewModel.OpenCategoryManagementCommand),
                    BindingOperations.GetBinding(categoriesButton, Button.CommandProperty)?.Path.Path);
                Assert.Equal(
                    nameof(ShellViewModel.IsInventoryExpanded),
                    BindingOperations.GetBinding(inventoryGroup, Expander.IsExpandedProperty)?.Path.Path);
                Assert.Equal("ShellProductsNavigationButton", AutomationProperties.GetAutomationId(productsButton));
                Assert.Equal("Sản phẩm & tồn kho", productsButton.ToolTip);
                Assert.Equal(nameof(ShellViewModel.ViewInventoryHistoryCommand),
                    BindingOperations.GetBinding(historyButton, Button.CommandProperty)?.Path.Path);
                Assert.Equal("ShellInventoryHistoryNavigationButton", AutomationProperties.GetAutomationId(historyButton));
                Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(grid));
                Assert.Equal(1024d, window.MinWidth);

                viewModel.InitializeAsync().GetAwaiter().GetResult();
                Assert.NotEmpty(viewModel.Products);
                var queryCount = viewModel.Products.Count;
                Assert.Equal(0d, grid.Columns[0].Width.Value);

                viewModel.ToggleBulkSelectionCommand.Execute(null);
                while (viewModel.ToggleBulkSelectionCommand.IsExecuting)
                    Thread.Sleep(1);
                window.UpdateLayout();
                Assert.True(viewModel.IsBulkSelectionMode);
                Assert.Equal(68d, grid.Columns[0].Width.Value);
                Assert.Equal(Visibility.Visible, bulkContextText.Visibility);
                Assert.Equal(nameof(ShellViewModel.SelectedProductHint),
                    BindingOperations.GetBinding(bulkContextText, TextBlock.TextProperty)?.Path.Path);
                Assert.Contains("Đã chọn 0 sản phẩm — chọn thêm ít nhất 2 sản phẩm", viewModel.SelectedProductHint, StringComparison.Ordinal);
                Assert.False(viewModel.ApplyBulkOperationCommand.CanExecute(null));
                Assert.True(exitBulkSelectionButton.IsVisible || exitBulkSelectionButton.Visibility == Visibility.Visible);
                Assert.NotNull(grid.Columns[0].HeaderTemplate);

                var firstRow = Assert.IsType<DataGridRow>(grid.ItemContainerGenerator.ContainerFromIndex(0));
                var secondRow = Assert.IsType<DataGridRow>(grid.ItemContainerGenerator.ContainerFromIndex(1));
                var firstCheckBox = FindVisualChildren<CheckBox>(firstRow).Single();
                var secondCheckBox = FindVisualChildren<CheckBox>(secondRow).Single();
                firstCheckBox.IsChecked = true;
                secondCheckBox.IsChecked = true;
                window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.DataBind, new Action(() => { }));
                Assert.True(viewModel.Products[0].IsBulkSelected);
                Assert.True(viewModel.Products[1].IsBulkSelected);
                Assert.Equal(2, viewModel.SelectedBulkProductCount);
                Assert.Null(viewModel.BulkPageSelectionState);
                Assert.True(bulkOperationButton.IsEnabled);
                Assert.Contains("Đã chọn 2 sản phẩm", bulkContextText.Text, StringComparison.Ordinal);

                viewModel.ToggleBulkPageSelectionCommand.Execute(null);
                while (viewModel.ToggleBulkPageSelectionCommand.IsExecuting)
                    Thread.Sleep(1);
                window.UpdateLayout();
                Assert.Equal(viewModel.Products.Count, viewModel.SelectedBulkProductCount);
                Assert.Contains($"Đã chọn {viewModel.Products.Count} sản phẩm", viewModel.SelectedProductHint, StringComparison.Ordinal);
                Assert.True(viewModel.BulkPageSelectionState);
                Assert.True(viewModel.ApplyBulkOperationCommand.CanExecute(null));
                Assert.Equal(nameof(ShellViewModel.ApplyBulkOperationCommand),
                    BindingOperations.GetBinding(bulkOperationButton, Button.CommandProperty)?.Path.Path);

                viewModel.Products[0].IsBulkSelected = false;
                Assert.Null(viewModel.BulkPageSelectionState);
                viewModel.Products[0].IsBulkSelected = true;

                viewModel.ToggleBulkSelectionCommand.Execute(null);
                while (viewModel.ToggleBulkSelectionCommand.IsExecuting)
                    Thread.Sleep(1);
                window.UpdateLayout();
                Assert.False(viewModel.IsBulkSelectionMode);
                Assert.Equal(0d, grid.Columns[0].Width.Value);

                var productsContent = Assert.IsType<StackPanel>(productsButton.Content);
                var productsLabel = productsContent.Children
                    .OfType<TextBlock>()
                    .Single(text => text.Text == "Sản phẩm & tồn kho");
                Assert.Equal(
                    nameof(ShellViewModel.IsSidebarExpanded),
                    BindingOperations.GetBinding(productsLabel, UIElement.VisibilityProperty)?.Path.Path);

                viewModel.UpdateViewportWidth(1366);
                window.UpdateLayout();
                Assert.Equal(4, cards.Columns);

                viewModel.UpdateViewportWidth(1024);
                window.UpdateLayout();
                var compactCardsTrigger = cards.Style.Triggers
                    .OfType<DataTrigger>()
                    .Single(trigger => trigger.Binding is Binding binding &&
                        binding.Path.Path == nameof(ShellViewModel.IsSidebarCompact));
                Assert.Contains(
                    compactCardsTrigger.Setters.OfType<Setter>(),
                    setter => setter.Property == UniformGrid.ColumnsProperty &&
                        Equals(setter.Value, 2));
                Assert.True(1024d - viewModel.SidebarWidth >= 900d);
                Assert.Equal(queryCount, viewModel.Products.Count);

                window.Close();
                scope.Dispose();
                provider.Dispose();
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return Task.CompletedTask;
            });
        }
        finally
        {
            PortableDevelopmentDatabase.DeleteOwnedScenario(scenarioRoot);
        }
    }

    [Fact]
    public async Task Bulk_command_uses_only_checked_rows_and_never_falls_back_to_single_selection()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var bulkDialog = new RecordingBulkDialogService();
        var viewModel = new ShellViewModel(
            services.GetRequiredService<IServiceScopeFactory>(),
            new FakeProductDialogService(),
            new CountingCategoryDialogService(),
            new FakeInventoryDialogService(),
            new FakeOrderHistoryWindowService(),
            new AllowAllPermissionService(),
            NullLogger<ShellViewModel>.Instance,
            bulkProductDialogService: bulkDialog);

        viewModel.Products.Add(new ProductRowViewModel(CreateProductRow(1, "P001")));
        viewModel.Products.Add(new ProductRowViewModel(CreateProductRow(2, "P002")));
        viewModel.SelectedProduct = viewModel.Products[1];

        viewModel.ToggleBulkSelectionCommand.Execute(null);
        await WaitForIdleAsync(viewModel.ToggleBulkSelectionCommand);
        viewModel.ToggleBulkPageSelectionCommand.Execute(null);
        await WaitForIdleAsync(viewModel.ToggleBulkPageSelectionCommand);

        Assert.Equal(2, viewModel.SelectedBulkProductCount);
        Assert.True(viewModel.ApplyBulkOperationCommand.CanExecute(null));

        viewModel.ApplyBulkOperationCommand.Execute(null);
        await WaitForIdleAsync(viewModel.ApplyBulkOperationCommand);

        Assert.Equal([1, 2], bulkDialog.SelectedIds);
    }

    [Fact]
    public async Task Label_command_uses_single_selection_or_exact_checked_set_and_keeps_bulk_threshold_independent()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var labels = new RecordingLabelDialogService();
        var viewModel = new ShellViewModel(
            services.GetRequiredService<IServiceScopeFactory>(),
            new FakeProductDialogService(),
            new CountingCategoryDialogService(),
            new FakeInventoryDialogService(),
            new FakeOrderHistoryWindowService(),
            new AllowAllPermissionService(),
            NullLogger<ShellViewModel>.Instance,
            labelPrintDialogService: labels);

        viewModel.Products.Add(new ProductRowViewModel(CreateProductRow(1, "P001")));
        viewModel.Products.Add(new ProductRowViewModel(CreateProductRow(2, "P002")));
        viewModel.Products.Add(new ProductRowViewModel(CreateProductRow(3, "P003")));
        viewModel.SelectedProduct = viewModel.Products[1];

        Assert.True(viewModel.PrintProductLabelsCommand.CanExecute(null));
        viewModel.PrintProductLabelsCommand.Execute(null);
        await WaitForIdleAsync(viewModel.PrintProductLabelsCommand);
        Assert.Equal([2], labels.SelectedIds);

        viewModel.ToggleBulkSelectionCommand.Execute(null);
        await WaitForIdleAsync(viewModel.ToggleBulkSelectionCommand);
        viewModel.Products[0].IsBulkSelected = true;
        viewModel.Products[2].IsBulkSelected = true;
        Assert.True(viewModel.PrintProductLabelsCommand.CanExecute(null));
        viewModel.PrintProductLabelsCommand.Execute(null);
        await WaitForIdleAsync(viewModel.PrintProductLabelsCommand);
        Assert.Equal([1, 3], labels.SelectedIds);
    }

    private static ProductListItemDto CreateProductRow(int id, string code) =>
        new(
            id,
            1,
            "Đồ uống",
            code,
            id.ToString("D6", CultureInfo.InvariantCulture),
            "Sản phẩm " + code,
            "Cái",
            10000,
            7000,
            10,
            1,
            0,
            true,
            false,
            true,
            false,
            true);

    private static ShellViewModel CreateViewModel(
        ServiceProvider services,
        ICategoryManagementDialogService categoryService)
    {
        return new ShellViewModel(
            services.GetRequiredService<IServiceScopeFactory>(),
            new FakeProductDialogService(),
            categoryService,
            new FakeInventoryDialogService(),
            new FakeOrderHistoryWindowService(),
            new AllowAllPermissionService(),
            NullLogger<ShellViewModel>.Instance);
    }

    private static async Task WaitForIdleAsync(POS.Wpf.Commands.AsyncRelayCommand command)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(5);
        while (command.IsExecuting && DateTimeOffset.UtcNow < timeout)
            await Task.Delay(10);
        Assert.False(command.IsExecuting);
    }

    private static T NamedField<T>(ShellWindow window, string name)
        where T : class
    {
        return typeof(ShellWindow).GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .GetValue(window) as T ??
            throw new InvalidOperationException($"Missing initialized WPF field: {name}.");
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
                yield return typed;
            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private static Task RunOnStaAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static string SolutionRoot() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed class CountingProductService : IProductService
    {
        public int SearchCalls { get; private set; }

        public Task<Result<PagedResult<ProductListItemDto>>> SearchAsync(
            ProductSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(Result.Success(
                PagedResult.Empty<ProductListItemDto>(request.PageNumber, request.PageSize)));
        }

        public Task<Result<ProductDetailsDto>> GetByIdAsync(int productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<ProductDetailsDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<ProductDetailsDto>> UpdateAsync(UpdateProductRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> SetActiveStateAsync(int productId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ArchiveAsync(int productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> RestoreAsync(int productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CountingCategoryDialogService : ICategoryManagementDialogService
    {
        public int ShowCalls { get; private set; }
        public Task ShowAsync()
        {
            ShowCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public bool HasPermission(SystemCapability permission) => true;
        public Result Authorize(SystemCapability permission) => Result.Success();
    }

    private sealed class FakeProductDialogService : IProductDialogService
    {
        public Task<bool> ShowCreateAsync() => Task.FromResult(false);
        public Task<bool> ShowEditAsync(int productId) => Task.FromResult(false);
    }

    private sealed class FakeInventoryDialogService : IInventoryDialogService
    {
        public Task<bool> ShowAdjustmentAsync(int productId) => Task.FromResult(false);
        public Task ShowHistoryAsync(string? productSearchTerm = null, string? productDisplayText = null) => Task.CompletedTask;
    }

    private sealed class FakeOrderHistoryWindowService : IOrderHistoryWindowService
    {
        public Task ShowAsync() => Task.CompletedTask;
    }

    private sealed class RecordingBulkDialogService : IBulkProductDialogService
    {
        public IReadOnlyList<int> SelectedIds { get; private set; } = [];

        public Task<bool> ShowAsync(IReadOnlyList<ProductRowViewModel> selectedProducts)
        {
            SelectedIds = selectedProducts.Select(product => product.Id).ToArray();
            return Task.FromResult(false);
        }
    }

    private sealed class RecordingLabelDialogService : ILabelPrintDialogService
    {
        public IReadOnlyList<int> SelectedIds { get; private set; } = [];

        public Task<bool> ShowAsync(IReadOnlyList<ProductRowViewModel> selectedProducts)
        {
            SelectedIds = selectedProducts.Select(product => product.Id).ToArray();
            return Task.FromResult(false);
        }
    }
}
