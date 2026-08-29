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
            "EmployeeActiveToggleButton", "EmployeeChangeRoleButton", "EmployeeEmptyAddButton",
            "EmployeeFilteredEmptyAddButton", "EmployeeFilteredEmptyClearButton", "EmployeeCreateCloseButton",
            "EmployeeIdentityHeader", "EmployeeProfileInformationCard", "EmployeeProfileActionStrip"
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

    [Fact]
    public void Employee_modern_content_contract_preserves_safe_master_detail_interaction()
    {
        var root = RepositoryLocator.GetPath();
        var view = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "EmployeeManagementWindow.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "ViewModels", "EmployeeManagementViewModel.cs"));
        var controls = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Themes", "Controls.xaml"));
        var salesService = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Services", "SalesWindowService.cs"));
        var readinessDialog = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "StoreReadinessDialogWindow.xaml"));

        Assert.Contains("ModernDataGridStyle", view, StringComparison.Ordinal);
        Assert.Contains("EnableRowVirtualization", controls, StringComparison.Ordinal);
        Assert.Contains("Header=\"Đăng nhập sai\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Sai\"", view, StringComparison.Ordinal);
        Assert.Contains("HasDetailContent", view, StringComparison.Ordinal);
        Assert.Contains("Chọn một nhân viên", view, StringComparison.Ordinal);
        Assert.Contains("Không tìm thấy nhân viên phù hợp", view, StringComparison.Ordinal);
        Assert.Contains("RoleFilterOptions", viewModel, StringComparison.Ordinal);
        Assert.Contains("GlobalEmployeeCount", viewModel, StringComparison.Ordinal);
        Assert.Contains("FilteredResultCount", viewModel, StringComparison.Ordinal);
        Assert.Contains("IsTrueEmployeeDatabaseEmpty", viewModel, StringComparison.Ordinal);
        Assert.Contains("IsFilteredNoResult", viewModel, StringComparison.Ordinal);
        Assert.Contains("GetSummaryAsync", viewModel, StringComparison.Ordinal);
        Assert.Contains("SelectedEmployee = nextSelection", viewModel, StringComparison.Ordinal);
        Assert.Contains("CancelEditCommand", view, StringComparison.Ordinal);
        Assert.Contains("EmployeeProfileTab", view, StringComparison.Ordinal);
        Assert.Contains("EmployeeAccountTab", view, StringComparison.Ordinal);
        Assert.Contains("EmployeePermissionsTab", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Nhân viên\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Tài khoản\"", view, StringComparison.Ordinal);
        Assert.Contains("ReadOnlyInfoCardStyle", view, StringComparison.Ordinal);
        Assert.Contains("ReadOnlyActionStripStyle", view, StringComparison.Ordinal);
        Assert.Contains("PhoneDisplayText", view, StringComparison.Ordinal);
        Assert.Contains("EmailDisplayText", view, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding EmployeeCode}\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding PageText}\"", view, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinition Width=\"*\" MinWidth=\"0\"", view, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\" ToolTip=\"{Binding EmployeeCode}\"", view, StringComparison.Ordinal);
        Assert.Contains("<Path Data=\"M 2,2 L 10,10 M 10,2 L 2,10\"", view, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ClearFiltersCommand}\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Tiếp tục tạo tài khoản sau khi lưu\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Hồ sơ nhân viên\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Tạo tài khoản ngay sau khi lưu", view, StringComparison.Ordinal);
        Assert.DoesNotContain("⌑", view, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedIndex=\"0\"", view, StringComparison.Ordinal);
        Assert.Contains("await _settingsStore.LoadAsync()", salesService, StringComparison.Ordinal);
        Assert.Contains("StoreReadinessDialogWindow", salesService, StringComparison.Ordinal);
        Assert.Contains("Mở cài đặt cửa hàng", readinessDialog, StringComparison.Ordinal);
        Assert.Contains("StoreReadinessCloseButton", readinessDialog, StringComparison.Ordinal);
    }

    [Fact]
    public void Employee_row_presents_typed_security_state_without_sensitive_values()
    {
        var row = new POS.Wpf.ViewModels.EmployeeRowViewModel(new POS.Application.DTOs.Employees.EmployeeListItemDto(
            7, "EMP-000007", "Test Employee", "0000000000", EmployeeStatus.Active, 9, "employee.test",
            POS.Domain.Enums.AccountStatus.Locked, Role.Manager, null, 3, DateTimeOffset.UtcNow));

        Assert.Equal("TE", row.Initials);
        Assert.Equal("Đang làm việc", row.EmployeeStatusText);
        Assert.Equal("Đã khóa", row.AccountStatusText);
        Assert.Equal("Quản lý", row.RoleText);
        Assert.Equal("3", row.FailedLoginText);
        Assert.DoesNotContain("password", row.AccountStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", row.AccountStatusText, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string value, string needle) => value.Split(needle, StringSplitOptions.None).Length - 1;
}
