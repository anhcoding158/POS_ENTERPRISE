using POS.Application.Authorization;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class StoreSettingsUiContractTests
{
    [Fact]
    public void Store_setup_surface_has_one_admin_navigation_and_stable_automation_ids()
    {
        var root = FindRepositoryRoot();
        var shell = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "ShellWindow.xaml"));
        var view = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "StoreSettingsWindow.xaml"));
        Assert.Equal(1, Count(shell, "x:Name=\"StoreSettingsNavigationButton\""));
        foreach (var id in new[] { "StoreSetupValidationSummary", "StoreSetupStoreName", "StoreSetupSave", "StoreSetupCancel", "StoreSetupTestQr", "StoreSetupTestPrinter" }) Assert.Contains(id, view, StringComparison.Ordinal);
        Assert.Contains("1. Thông tin cửa hàng", view, StringComparison.Ordinal);
        Assert.Contains("6. Readiness", view, StringComparison.Ordinal);
        Assert.False(RolePermissionPolicy.HasPermission(Role.Cashier, SystemCapability.ManageStoreSetup));
        Assert.True(RolePermissionPolicy.HasPermission(Role.Administrator, SystemCapability.ManageStoreSetup));
    }

    [Fact]
    public void Store_setup_does_not_add_a_second_window_or_direct_database_access()
    {
        var root = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "POS.Wpf"), "*StoreSettings*", SearchOption.AllDirectories).ToArray();
        Assert.Equal(1, files.Count(x => x.EndsWith("StoreSettingsWindow.xaml", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(1, files.Count(x => x.EndsWith("StoreSettingsWindow.xaml.cs", StringComparison.OrdinalIgnoreCase)));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "ViewModels", "StoreSettingsViewModel.cs"));
        Assert.DoesNotContain("PosDbContext", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Sqlite", viewModel, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string value, string needle) => value.Split(needle, StringSplitOptions.None).Length - 1;
    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "POS.Enterprise.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
