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

                viewModel.InitializeAsync().GetAwaiter().GetResult();
                Assert.NotEmpty(viewModel.Employees);
                Assert.Equal(viewModel.Employees.Count, viewModel.TotalCount);
                Assert.Same(viewModel.Employees[0], viewModel.SelectedEmployee);
                Assert.True(viewModel.HasSelection);
                Assert.Equal(viewModel.TotalCount, viewModel.TotalEmployees);
                Assert.True(viewModel.HasDetailContent);
                viewModel.SelectEmployeeAsync(viewModel.Employees[0].Id).GetAwaiter().GetResult();
                Assert.NotEqual("Ch\u01b0a c\u00f3", viewModel.EffectivePermissionsText);

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
                Assert.False(viewModel.IsEmptyState);
                Assert.True(viewModel.IsNoResultState);

                viewModel.NewEmployeeCommand.Execute(null);
                while (viewModel.NewEmployeeCommand.IsExecuting)
                    Thread.Sleep(5);
                Assert.True(viewModel.IsCreateMode);
                Assert.True(viewModel.IsEditing);
                Assert.Empty(viewModel.EmployeeCode);
                Assert.Empty(viewModel.FullName);

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
}
