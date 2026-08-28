using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Employees;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Wpf;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class EmployeeManagementUiHotfixTests
{
    [Fact]
    public async Task Production_employee_window_constructs_and_loads_on_an_isolated_migrated_database()
    {
        var root = SolutionRoot();
        var scenarioRoot = Path.Combine(
            Path.GetTempPath(),
            "POS-Enterprise-R4.2-UI-Hotfix-Test-" + Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(scenarioRoot, "pos-enterprise-isolated.db");
        Directory.CreateDirectory(scenarioRoot);
        File.Copy(Path.Combine(root, "data", "pos-enterprise.db"), databasePath);

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
                var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
                initializer.InitializeAsync().GetAwaiter().GetResult();

                var currentUser = provider.GetRequiredService<ICurrentUserService>();
                currentUser.SetCurrentUser(new AuthenticatedUserDto(
                    1,
                    "isolated-admin",
                    "Isolated Administrator",
                    Role.Administrator,
                    DateTimeOffset.UtcNow,
                    forcePasswordChange: false));

                var viewModel = scope.ServiceProvider.GetRequiredService<EmployeeManagementViewModel>();
                var window = new EmployeeManagementWindow(viewModel);
                Assert.Same(viewModel, window.DataContext);
                Assert.Equal(1, viewModel.PageNumber);
                Assert.Equal("Tất cả vai trò", viewModel.SelectedRoleFilter.DisplayName);
                Assert.Null(viewModel.SelectedEmployeeFilter.Value);
                Assert.Null(viewModel.SelectedAccountFilter.Value);
                Assert.Equal(4, viewModel.RoleOptions.Count);
                Assert.True(viewModel.CanManageEmployees);
                Assert.True(viewModel.CanManageAccounts);
                Assert.True(viewModel.CanResetPasswords);
                Assert.True(viewModel.CanLockAccounts);
                Assert.True(viewModel.CanAssignRoles);

                window.Show();
                while (!viewModel.IsLoaded)
                    global::System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        global::System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() => { }));
                window.Measure(new Size(1180, 720));
                window.Arrange(new Rect(0, 0, 1180, 720));
                window.UpdateLayout();
                var roleFilter = Assert.IsType<System.Windows.Controls.ComboBox>(window.FindName("EmployeeRoleFilterComboBox"));
                Assert.Same(viewModel, roleFilter.DataContext);
                Assert.Equal(viewModel.RoleFilterOptions.Count, roleFilter.Items.Count);
                Assert.Same(viewModel.SelectedRoleFilter, roleFilter.SelectedItem);
                var renderedRoleTexts = string.Join(
                    "|",
                    FindVisualDescendants<System.Windows.Controls.TextBlock>(roleFilter)
                        .Select(textBlock => $"{textBlock.Text}[{textBlock.ActualWidth:N1}x{textBlock.ActualHeight:N1}]"));
                Assert.Contains("Tất cả vai trò", renderedRoleTexts, StringComparison.Ordinal);
                Assert.NotEmpty(viewModel.Employees);
                Assert.Equal(viewModel.Employees.Count, viewModel.TotalCount);
                Assert.Same(viewModel.Employees[0], viewModel.SelectedEmployee);
                Assert.True(viewModel.HasSelection);
                Assert.Equal(viewModel.TotalCount, viewModel.TotalEmployees);
                Assert.Equal(viewModel.TotalEmployees, viewModel.GlobalEmployeeCount);
                Assert.Equal(viewModel.TotalCount, viewModel.FilteredResultCount);
                Assert.True(viewModel.HasDetailContent);
                viewModel.SelectEmployeeAsync(viewModel.Employees[0].Id).GetAwaiter().GetResult();
                Assert.NotEqual("Ch\u01b0a c\u00f3", viewModel.EffectivePermissionsText);

                var identityHeader = Assert.IsType<System.Windows.Controls.Border>(window.FindName("EmployeeIdentityHeader"));
                var identityTextBlock = Assert.IsType<System.Windows.Controls.StackPanel>(window.FindName("EmployeeIdentityTextBlock"));
                var identityStatusCard = Assert.IsType<System.Windows.Controls.Border>(window.FindName("EmployeeIdentityStatusCard"));
                var employeeList = FindVisualDescendants<System.Windows.Controls.DataGrid>(window)
                    .Single(grid => System.Windows.Automation.AutomationProperties.GetAutomationId(grid) == "EmployeeList");
                var employeeMasterCard = FindVisualAncestor<System.Windows.Controls.Border>(employeeList);
                Assert.NotNull(employeeMasterCard);
                var identityTexts = FindVisualDescendants<System.Windows.Controls.TextBlock>(identityHeader).Select(textBlock => textBlock.Text).ToArray();
                Assert.Contains(viewModel.FullName, identityTexts);
                Assert.Contains(viewModel.EmployeeCode, identityTexts);
                Assert.Contains(viewModel.UsernameText, identityTexts);
                Assert.Contains("Nhân viên", identityTexts);
                Assert.Contains(viewModel.EmployeeStatusText, identityTexts);
                Assert.Contains("Tài khoản", identityTexts);
                Assert.Contains(viewModel.AccountStatusText, identityTexts);

                var profileCard = Assert.IsType<System.Windows.Controls.Border>(window.FindName("EmployeeProfileInformationCard"));
                var actionStrip = Assert.IsType<System.Windows.Controls.Border>(window.FindName("EmployeeProfileActionStrip"));
                var editButton = FindVisualDescendants<System.Windows.Controls.Button>(actionStrip)
                    .Single(button => System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "EmployeeEditButton");
                Assert.Same(viewModel.EditProfileCommand, editButton.Command);
                Assert.True(profileCard.IsVisible && profileCard.ActualWidth > 0 && profileCard.ActualHeight > 0);
                Assert.True(actionStrip.IsVisible && actionStrip.ActualWidth > 0 && actionStrip.ActualHeight > 0);
                Assert.Contains("Thông tin hồ sơ", FindVisualDescendants<System.Windows.Controls.TextBlock>(profileCard).Select(textBlock => textBlock.Text));

                var codeText = FindVisualDescendants<System.Windows.Controls.TextBlock>(identityHeader)
                    .Single(textBlock => textBlock.Text == viewModel.EmployeeCode);
                Assert.Equal(viewModel.EmployeeCode, codeText.ToolTip?.ToString());
                Assert.Equal(System.Windows.TextTrimming.CharacterEllipsis, codeText.TextTrimming);
                Assert.True(codeText.ActualWidth <= identityHeader.ActualWidth);
                var originalPhone = viewModel.PhoneNumber;
                var originalEmail = viewModel.EmailAddress;
                viewModel.PhoneNumber = string.Empty;
                viewModel.EmailAddress = string.Empty;
                window.UpdateLayout();
                var profileTexts = FindVisualDescendants<System.Windows.Controls.TextBlock>(profileCard).Select(textBlock => textBlock.Text).ToArray();
                Assert.Equal("Chưa cập nhật", viewModel.PhoneDisplayText);
                Assert.Equal("Chưa cập nhật", viewModel.EmailDisplayText);
                Assert.Contains("Chưa cập nhật", profileTexts);
                viewModel.PhoneNumber = originalPhone;
                viewModel.EmailAddress = originalEmail;
                viewModel.DiscardUnsavedChanges();

                foreach (var size in new[] { new Size(1180, 720), new Size(1366, 768), new Size(1280, 720), new Size(1000, 620) })
                {
                    window.Measure(size);
                    window.Arrange(new Rect(0, 0, size.Width, size.Height));
                    window.UpdateLayout();
                    Assert.True(identityHeader.IsVisible && identityHeader.ActualWidth > 0 && identityHeader.ActualHeight > 0);
                    Assert.True(profileCard.IsVisible && profileCard.ActualWidth > 0 && profileCard.ActualHeight > 0);
                    Assert.True(actionStrip.IsVisible && actionStrip.ActualWidth > 0 && actionStrip.ActualHeight > 0);
                    Assert.True(codeText.ActualWidth > 0 && codeText.ActualHeight > 0);
                    var profileBounds = new Rect(profileCard.TransformToAncestor(window).Transform(new Point(0, 0)), new Size(profileCard.ActualWidth, profileCard.ActualHeight));
                    var actionBounds = new Rect(actionStrip.TransformToAncestor(window).Transform(new Point(0, 0)), new Size(actionStrip.ActualWidth, actionStrip.ActualHeight));
                    var masterBounds = Bounds(employeeMasterCard!, window);
                    var summaryText = FindVisualDescendants<System.Windows.Controls.TextBlock>(employeeMasterCard!)
                        .Single(textBlock => textBlock.Text == viewModel.PageText);
                    var summaryBounds = Bounds(summaryText, window);
                    var identityTextBounds = Bounds(identityTextBlock, window);
                    var identityStatusBounds = Bounds(identityStatusCard, window);
                    Assert.True(masterBounds.Contains(summaryBounds), $"Employee summary escaped the master card at {size}.");
                    Assert.False(identityTextBounds.IntersectsWith(identityStatusBounds), $"Employee identity text overlaps status at {size}.");
                    Assert.True(identityStatusCard.ActualWidth > 0 && identityStatusCard.ActualHeight > 0);
                    Assert.True(actionBounds.Top >= profileBounds.Bottom - 1);
                }

                viewModel.SearchTerm = Guid.NewGuid().ToString("N");
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                Assert.Empty(viewModel.Employees);
                Assert.Equal(0, viewModel.TotalCount);
                Assert.Null(viewModel.SelectedEmployee);
                Assert.False(viewModel.HasSelection);

                viewModel.SearchTerm = string.Empty;
                viewModel.SelectedEmployeeFilter = viewModel.EmployeeFilters.Single(option => option.Value == EmployeeStatus.Inactive);
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                Assert.Empty(viewModel.Employees);
                Assert.Equal(1, viewModel.GlobalEmployeeCount);
                Assert.Equal(0, viewModel.FilteredResultCount);
                Assert.True(viewModel.HasActiveSearchOrFilter);
                Assert.True(viewModel.IsFilteredNoResult);
                Assert.False(viewModel.IsTrueEmployeeDatabaseEmpty);
                Assert.False(viewModel.IsEmptyState);
                Assert.True(viewModel.IsNoResultState);

                viewModel.NewEmployeeCommand.Execute(null);
                while (viewModel.NewEmployeeCommand.IsExecuting)
                    Thread.Sleep(5);
                Assert.True(viewModel.IsCreateMode);
                Assert.True(viewModel.IsEditing);
                window.UpdateLayout();
                var createClose = Assert.IsType<System.Windows.Controls.Button>(window.FindName("EmployeeCreateCloseButton"));
                Assert.True(createClose.IsVisible);
                Assert.Equal(0, viewModel.SelectedDetailTabIndex);
                Assert.Empty(viewModel.EmployeeCode);
                Assert.Empty(viewModel.FullName);

                foreach (var size in new[] { new Size(1366, 768), new Size(1280, 720), new Size(1000, 620) })
                {
                    window.Measure(size);
                    window.Arrange(new Rect(0, 0, size.Width, size.Height));
                    window.UpdateLayout();
                    Assert.True(createClose.ActualWidth > 0 && createClose.ActualHeight > 0);
                }

                viewModel.EmployeeCode = "UX-" + Guid.NewGuid().ToString("N")[..8];
                viewModel.FullName = "UI hotfix isolated employee";
                viewModel.CreateAccount = true;
                viewModel.SaveCommand.Execute(null);
                while (viewModel.SaveCommand.IsExecuting)
                    Thread.Sleep(5);
                Assert.False(viewModel.IsCreateMode);
                Assert.True(viewModel.HasSelection);
                Assert.False(viewModel.HasAccount);
                Assert.True(viewModel.IsCreatingAccount);
                Assert.Equal(1, viewModel.SelectedDetailTabIndex);

                viewModel.SearchTerm = string.Empty;
                viewModel.SelectedEmployeeFilter = viewModel.EmployeeFilters[0];
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                Assert.NotEmpty(viewModel.Employees);

                var employeeService = scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>();
                currentUser.SetCurrentUser(new AuthenticatedUserDto(
                    1,
                    "isolated-cashier",
                    "Isolated Cashier",
                    Role.Cashier,
                    DateTimeOffset.UtcNow,
                    forcePasswordChange: false));
                var unauthorized = employeeService.SearchAsync(new EmployeeSearchRequest())
                    .GetAwaiter()
                    .GetResult();
                Assert.True(unauthorized.IsFailure);

                currentUser.SetCurrentUser(new AuthenticatedUserDto(
                    1,
                    "isolated-admin",
                    "Isolated Administrator",
                    Role.Administrator,
                    DateTimeOffset.UtcNow,
                    forcePasswordChange: false));
                Assert.NotNull(scope.ServiceProvider.GetRequiredService<SalesViewModel>());
                Assert.NotNull(scope.ServiceProvider.GetRequiredService<StoreSettingsViewModel>());
                window.Close();
                scope.Dispose();
                provider.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return Task.CompletedTask;
            });
        }
        finally
        {
            DeleteOwnedScenario(scenarioRoot);
        }
    }

    [Fact]
    public void Employee_navigation_boundary_logs_exception_chain_and_does_not_use_startup_error_text()
    {
        var root = SolutionRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "POS.Wpf",
            "Services",
            "EmployeeManagementDialogService.cs"));

        Assert.Contains("ILogger<EmployeeManagementDialogService>", source, StringComparison.Ordinal);
        Assert.Contains("ExceptionChain={ExceptionChain}", source, StringComparison.Ordinal);
        Assert.Contains("FormatExceptionChain", source, StringComparison.Ordinal);
        Assert.DoesNotContain("khởi động an toàn", source, StringComparison.OrdinalIgnoreCase);
    }

    private static Task<object?> RunOnStaAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
                completion.SetResult(null);
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

    private static void DeleteOwnedScenario(string scenarioRoot)
    {
        var fullRoot = Path.GetFullPath(scenarioRoot);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        if (!fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The test scenario escaped the temporary boundary.");
        for (var attempt = 0; attempt < 20 && Directory.Exists(fullRoot); attempt++)
        {
            try
            {
                Directory.Delete(fullRoot, recursive: true);
            }
            catch (IOException)
            {
                if (attempt == 19) return;
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == 19) return;
                Thread.Sleep(50);
            }
        }

        // SQLite/native handles can outlive the disposed provider until the testhost exits.
        // The exact temporary root is retried here and is finalized by checkpoint cleanup.
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualDescendants<T>(child))
                yield return descendant;
        }
    }

    private static T? FindVisualAncestor<T>(DependencyObject node)
        where T : DependencyObject
    {
        var current = System.Windows.Media.VisualTreeHelper.GetParent(node);
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static Rect Bounds(FrameworkElement element, Window window)
    {
        var origin = element.TransformToAncestor(window).Transform(new Point(0, 0));
        return new Rect(origin, new Size(element.ActualWidth, element.ActualHeight));
    }
}
