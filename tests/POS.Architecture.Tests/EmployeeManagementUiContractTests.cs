using POS.Application.Authorization;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class EmployeeManagementUiContractTests
{
    [Fact]
    public void Employee_navigation_and_view_have_stable_admin_contract()
    {
        var root = RepositoryLocator.GetPath();
        var shell = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "ShellWindow.xaml"));
        var view = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "EmployeeManagementWindow.xaml"));

        Assert.Equal(1, Count(shell, "x:Name=\"EmployeeManagementNavigationButton\""));
        foreach (var automationId in new[]
        {
            "EmployeeManagementWindow", "EmployeeSearchBox", "EmployeeStatusFilter", "EmployeeAccountFilter",
            "EmployeeRoleFilter", "EmployeeList", "EmployeeDirtyState", "EmployeeValidationSummary", "EmployeeSaveButton",
            "EmployeeCreateAccountButton", "EmployeeResetPasswordButton", "EmployeeLockToggleButton",
            "EmployeeActiveToggleButton", "EmployeeChangeRoleButton"
        })
        {
            Assert.Contains(automationId, view, StringComparison.Ordinal);
        }

        Assert.True(RolePermissionPolicy.HasPermission(Role.Administrator, SystemCapability.ViewEmployees));
        Assert.False(RolePermissionPolicy.HasPermission(Role.Cashier, SystemCapability.ViewEmployees));
    }

    [Fact]
    public void Employee_view_model_uses_application_service_and_never_database_directly()
    {
        var root = RepositoryLocator.GetPath();
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "ViewModels", "EmployeeManagementViewModel.cs"));
        var app = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "App.xaml.cs"));
        var forcedView = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "ForcedPasswordChangeWindow.xaml"));

        Assert.DoesNotContain("PosDbContext", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Sqlite", viewModel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IEmployeeAccountService", viewModel, StringComparison.Ordinal);
        Assert.Contains("EmployeeManagementViewModel", app, StringComparison.Ordinal);
        Assert.Contains("ForcedPasswordChangeViewModel", app, StringComparison.Ordinal);
        Assert.Contains("ForcedPasswordChangeSaveButton", forcedView, StringComparison.Ordinal);
        Assert.Contains("ForcedPasswordChangeCancelButton", forcedView, StringComparison.Ordinal);
    }

    private static int Count(string value, string needle) => value.Split(needle, StringSplitOptions.None).Length - 1;
}
