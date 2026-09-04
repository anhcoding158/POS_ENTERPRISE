using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Suppliers;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Wpf;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SupplierUiContractTests
{
    [Fact]
    public void Supplier_navigation_and_window_expose_the_bounded_master_contract()
    {
        var root = RepositoryLocator.GetPath();
        var shell = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "ShellWindow.xaml"));
        var window = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "SupplierManagementWindow.xaml"));
        var editor = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "SupplierEditorWindow.xaml"));
        var editorCodeBehind = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "SupplierEditorWindow.xaml.cs"));
        var controls = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Themes", "Controls.xaml"));
        var app = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "App.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "ViewModels", "SupplierManagementViewModel.cs"));

        Assert.Equal(1, Count(shell, "x:Name=\"ShellSuppliersNavigationButton\""));
        Assert.Contains("CanViewSuppliers", shell, StringComparison.Ordinal);
        Assert.Contains("OpenSupplierManagementCommand", shell, StringComparison.Ordinal);

        foreach (var automationId in new[]
        {
            "SupplierManagementWindow", "SupplierAddButton", "SupplierSearchField",
            "SupplierStatusFilter", "SupplierRefreshButton", "SupplierGrid",
            "SupplierNoSelection", "SupplierDetailStatusBadge", "SupplierInactiveBanner",
            "SupplierEditButton", "SupplierToggleActiveButton",
            "SupplierInlineError", "SupplierSaveButton", "SupplierCancelButton",
            "SupplierCodeField", "SupplierNameField", "SupplierTaxCodeField",
            "SupplierContactField", "SupplierPhoneField", "SupplierEmailField",
            "SupplierAddressField", "SupplierNotesField"
        })
        {
            Assert.Contains(automationId, window + editor, StringComparison.Ordinal);
        }

        Assert.Contains("IsLoading", window, StringComparison.Ordinal);
        Assert.Contains("IsError", window, StringComparison.Ordinal);
        Assert.Contains("Chưa có nhà cung cấp phù hợp", window, StringComparison.Ordinal);
        Assert.Contains("SelectedUpdatedAtText", window, StringComparison.Ordinal);
        Assert.Contains("CanManageSuppliers", window, StringComparison.Ordinal);
        Assert.Contains("TotalCountText", window, StringComparison.Ordinal);
        Assert.Contains("Text=\"Chọn một nhà cung cấp để xem chi tiết.\"", window, StringComparison.Ordinal);
        Assert.Contains("Nhà cung cấp đang ngừng hoạt động và không dùng cho chứng từ mua mới.", window, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource SupplierDataGridStyle}\"", window, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource SupplierDataGridStatusCellStyle}\"", window, StringComparison.Ordinal);
        Assert.Contains("Style x:Key=\"SupplierCenteredDataGridColumnHeaderStyle\"", controls, StringComparison.Ordinal);
        Assert.Contains("HeaderStyle=\"{StaticResource SupplierCenteredDataGridColumnHeaderStyle}\"", window, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource DangerOutlineButtonStyle}\"", window, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinition Width=\"Auto\"", window, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"112\"", window, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"150\"", window, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"170\"", window, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"200\"", window, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"155\"", window, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment=\"Center\"", window, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Center\"", window, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"NoWrap\"", window, StringComparison.Ordinal);
        Assert.Contains("StackPanel Orientation=\"Horizontal\"", window, StringComparison.Ordinal);
        Assert.Contains("Width=\"190\"", window, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Right\"", window, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Thêm nhà cung cấp\"", window, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Tìm kiếm nhà cung cấp\"", window, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Lọc trạng thái nhà cung cấp\"", window, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Chỉnh sửa nhà cung cấp\"", window, StringComparison.Ordinal);
        Assert.Contains("{Binding ToggleActiveText}", window, StringComparison.Ordinal);
        Assert.Contains("public string SelectedInitials", viewModel, StringComparison.Ordinal);
        Assert.Contains("public bool IsSelectedInactive", viewModel, StringComparison.Ordinal);
        Assert.Contains("public string SelectedUpdatedAtText", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("<Window.Resources>", window, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ModernComboBoxStyle\"", controls, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SupplierDataGridStyle\"", controls, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"DangerOutlineButtonStyle\"", controls, StringComparison.Ordinal);
        Assert.Contains("Source=\"Themes/Controls.xaml\"", app, StringComparison.Ordinal);
        Assert.DoesNotContain("ModernComboBoxStyle\"", window[..window.IndexOf("<Grid", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"SupplierEditorWindow\"", editor, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Chỉnh sửa nhà cung cấp\"", editor, StringComparison.Ordinal);
        Assert.Contains("IsDirty", editorCodeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("Xóa", window, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PurchaseReceipt", window + editor, StringComparison.Ordinal);
        Assert.DoesNotContain("Lịch sử nhập hàng", window, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Supplier_view_model_uses_short_application_scopes_and_no_database_access()
    {
        var root = RepositoryLocator.GetPath();
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "ViewModels", "SupplierManagementViewModel.cs"));
        var editor = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "ViewModels", "SupplierEditorViewModel.cs"));

        Assert.DoesNotContain("PosDbContext", viewModel + editor, StringComparison.Ordinal);
        Assert.DoesNotContain("Sqlite", viewModel + editor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IServiceScopeFactory", viewModel + editor, StringComparison.Ordinal);
        Assert.Contains("ISupplierService", viewModel + editor, StringComparison.Ordinal);
        Assert.Contains("ShowCreateAsync", viewModel, StringComparison.Ordinal);
        Assert.Contains("ShowEditAsync", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_composition_resolves_authorized_supplier_service()
    {
        var root = Path.Combine(Path.GetTempPath(), "pos-supplier-composition-" + Guid.NewGuid().ToString("N"));
        var previousRuntimeMode = Environment.GetEnvironmentVariable(DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable);

        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable(DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable, DatabaseRuntimeGuard.IsolatedTestMode);
            var repositoryRoot = RepositoryLocator.GetPath();
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(repositoryRoot, "src", "POS.Wpf"))
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Infrastructure:DatabasePath"] = Path.Combine(root, "fixture.db"),
                    ["Infrastructure:SeedDefaultAdministrator"] = bool.FalseString
                })
                .Build();
            var services = new ServiceCollection();
            services.AddLogging();
            typeof(POS.Wpf.App).GetMethod(
                    "ConfigureApplicationServices",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(null, [services, configuration]);

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            using var scope = provider.CreateScope();

            Assert.IsType<AuthorizedSupplierService>(scope.ServiceProvider.GetRequiredService<ISupplierService>());
            Assert.NotNull(provider.GetRequiredService<ISupplierManagementDialogService>());
            Assert.NotNull(provider.GetRequiredService<ISupplierDialogService>());
        }
        finally
        {
            Environment.SetEnvironmentVariable(DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable, previousRuntimeMode);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Supplier_windows_load_with_production_resources_and_dialog_path_on_sta()
    {
        await RunOnStaAsync(() =>
        {
            if (global::System.Windows.Application.Current is null)
            {
                var application = new App();
                application.InitializeComponent();
            }

            var sharedComboBoxStyle = Assert.IsType<Style>(
                global::System.Windows.Application.Current!.Resources["ModernComboBoxStyle"]);
            var scenarioRoot = Path.Combine(
                Path.GetTempPath(),
                "POS-Enterprise-R6.1-Supplier-UI-Test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scenarioRoot);

            var previousRuntimeMode = Environment.GetEnvironmentVariable(
                DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable);

            try
            {
                Environment.SetEnvironmentVariable(
                    DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable,
                    DatabaseRuntimeGuard.IsolatedTestMode);

                var repositoryRoot = RepositoryLocator.GetPath();
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Path.Combine(repositoryRoot, "src", "POS.Wpf"))
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Infrastructure:DatabasePath"] = Path.Combine(scenarioRoot, "fixture.db"),
                        ["Infrastructure:SeedDefaultAdministrator"] = bool.FalseString
                    })
                    .Build();

                var services = new ServiceCollection();
                services.AddLogging();
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

                provider.GetRequiredService<ICurrentUserService>().SetCurrentUser(
                    new AuthenticatedUserDto(
                        1,
                        "supplier-ui-admin",
                        "Supplier UI Administrator",
                        Role.Administrator,
                        DateTimeOffset.UtcNow,
                        forcePasswordChange: false));

                var managementViewModel = scope.ServiceProvider
                    .GetRequiredService<SupplierManagementViewModel>();
                var managementWindow = new SupplierManagementWindow(managementViewModel);

                try
                {
                    Assert.Same(managementViewModel, managementWindow.DataContext);
                    managementViewModel.Suppliers.Add(new SupplierListRowViewModel(
                        new SupplierListItemDto(1, "SUP-ACTIVE", "Active Supplier", null, "Contact Active", "0123456789", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
                    managementViewModel.Suppliers.Add(new SupplierListRowViewModel(
                        new SupplierListItemDto(2, "SUP-INACTIVE", "Inactive Supplier", null, "Contact Inactive", "0987654321", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
                    var contentRoot = Assert.IsType<Grid>(managementWindow.Content);
                    var supplierGrid = FindByAutomationId<DataGrid>(managementWindow, "SupplierGrid");
                    Assert.NotNull(supplierGrid);
                    Assert.Equal(5, supplierGrid!.Columns.Count);
                    var refreshButton = FindByAutomationId<Button>(managementWindow, "SupplierRefreshButton");
                    Assert.NotNull(refreshButton);
                    var statusFilter = Assert.IsType<ComboBox>(FindLogicalDescendant<ComboBox>(managementWindow));

                    managementWindow.Content = null;
                    var layoutHost = new Window
                    {
                        Content = contentRoot,
                        DataContext = managementViewModel,
                        ShowInTaskbar = false,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -10000,
                        Top = -10000
                    };
                    layoutHost.Show();

                    try
                    {
                        foreach (var size in new[] { new Size(1180, 760), new Size(1080, 640), new Size(1920, 1000) })
                        {
                            layoutHost.Width = size.Width;
                            layoutHost.Height = size.Height;
                            layoutHost.UpdateLayout();
                            supplierGrid.UpdateLayout();

                            Assert.True(refreshButton!.ActualWidth >= refreshButton.MinWidth, $"Refresh button width {refreshButton.ActualWidth} is below MinWidth {refreshButton.MinWidth} at {size.Width}x{size.Height}.");
                            Assert.True(refreshButton.ActualHeight >= refreshButton.MinHeight);
                            var refreshContent = Assert.IsType<StackPanel>(refreshButton.Content);
                            Assert.True(refreshContent.DesiredSize.Width + refreshButton.Padding.Left + refreshButton.Padding.Right <= refreshButton.ActualWidth + 1);
                            Assert.True(refreshContent.DesiredSize.Height <= refreshButton.ActualHeight + 1);

                            var columnWidths = supplierGrid.Columns.Select(column => column.ActualWidth).ToArray();
                            Assert.True(columnWidths.Sum() <= supplierGrid.ActualWidth + 1, $"Supplier columns overflow the grid at {size.Width}x{size.Height}: sum={columnWidths.Sum()}, grid={supplierGrid.ActualWidth}.");
                            Assert.InRange(columnWidths[0], 110, 140);
                        Assert.InRange(columnWidths[2], 90, 200);
                            Assert.InRange(columnWidths[3], 125, 155);
                            Assert.InRange(columnWidths[4], 150, 170);
                            Assert.True(columnWidths[1] >= 90, $"Supplier name column is below its minimum at {size.Width}x{size.Height}.");
                        }

                        Assert.Equal(DataGridLengthUnitType.Star, supplierGrid.Columns[1].Width.UnitType);
                        Assert.Equal(DataGridLengthUnitType.Star, supplierGrid.Columns[2].Width.UnitType);
                        Assert.Equal(200, supplierGrid.Columns[2].MaxWidth);
                        Assert.Equal(155, supplierGrid.Columns[3].MaxWidth);
                        Assert.Equal(170, supplierGrid.Columns[4].MaxWidth);

                        var statusBadges = FindVisualDescendants<Border>(supplierGrid)
                            .Where(border => global::System.Windows.Automation.AutomationProperties.GetAutomationId(border) == "SupplierStatusBadge")
                            .ToArray();
                        Assert.Equal(2, statusBadges.Length);
                        Assert.All(statusBadges, badge =>
                        {
                            Assert.Equal(VerticalAlignment.Center, badge.VerticalAlignment);
                            Assert.Equal(HorizontalAlignment.Center, badge.HorizontalAlignment);
                            Assert.InRange(badge.ActualHeight, 26, 28.5);
                            Assert.True(
                                badge.ActualWidth <= supplierGrid.Columns[4].ActualWidth - 8 + 1,
                                $"Status badge width {badge.ActualWidth} exceeds status column {supplierGrid.Columns[4].ActualWidth} after cell padding.");
                        });
                        var statusHeader = FindVisualDescendants<DataGridColumnHeader>(supplierGrid)
                            .Single(header => string.Equals(header.Content?.ToString(), "Trạng thái", StringComparison.Ordinal));
                        Assert.Equal(HorizontalAlignment.Center, statusHeader.HorizontalContentAlignment);
                        Assert.Equal(VerticalAlignment.Center, statusHeader.VerticalContentAlignment);
                        Assert.Equal(new Thickness(0), statusHeader.Padding);
                    }
                    finally
                    {
                        layoutHost.Close();
                    }

                    Assert.Same(sharedComboBoxStyle, statusFilter.Style);

                    var owner = new Window
                    {
                        Width = 10,
                        Height = 10,
                        ShowInTaskbar = false,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -10000,
                        Top = -10000
                    };
                    owner.Show();

                    try
                    {
                        SupplierEditorWindow? openedEditor = null;
                        var closeTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(10)
                        };
                        RoutedEventHandler editorLoadedHandler = (_, args) =>
                        {
                            if (args.Source is not SupplierEditorWindow editor) return;

                            openedEditor = editor;
                            closeTimer.Stop();
                            editor.Measure(new Size(620, 720));
                            editor.Arrange(new Rect(0, 0, 620, 720));
                            editor.UpdateLayout();
                            editor.Close();
                        };
                        EventManager.RegisterClassHandler(
                            typeof(SupplierEditorWindow),
                            FrameworkElement.LoadedEvent,
                            editorLoadedHandler);
                        closeTimer.Start();

                        var result = provider
                            .GetRequiredService<ISupplierDialogService>()
                            .ShowCreateAsync(owner)
                            .GetAwaiter()
                            .GetResult();

                    Assert.Null(result);
                    Assert.NotNull(openedEditor);
                    Assert.Equal(
                        "SupplierEditorWindow",
                        global::System.Windows.Automation.AutomationProperties.GetAutomationId(openedEditor));
                }
                    finally
                    {
                        owner.Close();
                    }
                }
                finally
                {
                    managementWindow.Close();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable,
                    previousRuntimeMode);

                if (Directory.Exists(scenarioRoot))
                {
                    Directory.Delete(scenarioRoot, recursive: true);
                }
            }
        });
    }

    private static async Task RunOnStaAsync(Action action)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
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
        await completion.Task;
        thread.Join();
    }

    private static T? FindLogicalDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is T match) return match;

            if (child is DependencyObject dependencyObject)
            {
                var descendant = FindLogicalDescendant<T>(dependencyObject);
                if (descendant is not null) return descendant;
            }
        }

        return null;
    }

    private static T? FindByAutomationId<T>(DependencyObject root, string automationId)
        where T : DependencyObject
    {
        if (root is T automationMatch && global::System.Windows.Automation.AutomationProperties.GetAutomationId(root) == automationId)
            return automationMatch;

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            var childMatch = FindByAutomationId<T>(child, automationId);
            if (childMatch is not null) return childMatch;
        }

        return null;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;

            foreach (var descendant in FindVisualDescendants<T>(child))
                yield return descendant;
        }
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
