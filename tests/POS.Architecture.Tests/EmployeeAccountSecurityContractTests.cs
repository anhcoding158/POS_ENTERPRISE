using Xunit;

namespace POS.Architecture.Tests;

public sealed class EmployeeAccountSecurityContractTests
{
    [Fact]
    public void Session_loop_must_force_password_change_before_shell_and_return_to_login_on_cancel()
    {
        var app = File.ReadAllText(RepositoryLocator.GetPath("src", "POS.Wpf", "App.xaml.cs"));
        var loopStart = app.IndexOf("while (true)", StringComparison.Ordinal);
        Assert.True(loopStart >= 0);
        var sessionLoop = app[loopStart..];

        var loginIndex = sessionLoop.IndexOf("ShowLoginWindow", StringComparison.Ordinal);
        var passwordChangeIndex = sessionLoop.IndexOf("EnsurePasswordChangeCompletedAsync", StringComparison.Ordinal);
        var shellIndex = sessionLoop.IndexOf("ShowShellWindow", StringComparison.Ordinal);

        Assert.True(loginIndex >= 0);
        Assert.True(passwordChangeIndex > loginIndex);
        Assert.True(shellIndex > passwordChangeIndex);
        Assert.Contains("currentUserService.Clear();", sessionLoop, StringComparison.Ordinal);
        Assert.Contains("continue;", sessionLoop, StringComparison.Ordinal);
        Assert.Contains("return completed && serviceProvider.GetRequiredService<ICurrentUserService>().CurrentUser?.ForcePasswordChange == false;", app, StringComparison.Ordinal);
        Assert.Contains("window.ShowDialog() == true", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Employee_production_paths_must_not_expose_hard_delete_contracts()
    {
        var sourcePaths = new[]
        {
            RepositoryLocator.GetPath("src", "POS.Application", "Abstractions", "Services", "IEmployeeAccountService.cs"),
            RepositoryLocator.GetPath("src", "POS.Application", "Services", "EmployeeAccountService.cs"),
            RepositoryLocator.GetPath("src", "POS.Infrastructure", "Persistence", "Repositories", "EmployeeRepository.cs"),
            RepositoryLocator.GetPath("src", "POS.Infrastructure", "Persistence", "Repositories", "UserRepository.cs"),
            RepositoryLocator.GetPath("src", "POS.Wpf", "ViewModels", "EmployeeManagementViewModel.cs"),
            RepositoryLocator.GetPath("src", "POS.Wpf", "Views", "EmployeeManagementWindow.xaml"),
            RepositoryLocator.GetPath("src", "POS.Wpf", "Views", "EmployeeManagementWindow.xaml.cs")
        };

        foreach (var path in sourcePaths)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("DeleteEmployee", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DeleteAccount", source, StringComparison.Ordinal);
            Assert.DoesNotContain("EntityState.Deleted", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DbSet.Remove", source, StringComparison.Ordinal);
            Assert.DoesNotContain(".Remove(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("RemoveRange", source, StringComparison.Ordinal);
        }

        var view = File.ReadAllText(RepositoryLocator.GetPath("src", "POS.Wpf", "Views", "EmployeeManagementWindow.xaml"));
        Assert.DoesNotContain("Xóa vĩnh viễn", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Employee_success_and_toast_contracts_must_not_carry_credential_material()
    {
        var root = RepositoryLocator.GetPath();
        var safeDialogSources = new[]
        {
            "src/POS.Wpf/Views/EmployeeAccountSuccessWindow.xaml",
            "src/POS.Wpf/Views/EmployeeAccountSuccessWindow.xaml.cs",
            "src/POS.Wpf/Views/SuccessDialogContent.xaml",
            "src/POS.Wpf/Views/SuccessDialogContent.xaml.cs",
            "src/POS.Wpf/Views/SuccessDialogRequest.cs",
            "src/POS.Wpf/Views/SuccessDialogViewModel.cs",
            "src/POS.Wpf/Views/SuccessDialogWindow.xaml",
            "src/POS.Wpf/Views/SuccessDialogWindow.xaml.cs"
        };

        foreach (var relativePath in safeDialogSources)
        {
            var source = File.ReadAllText(Path.Combine([root, .. relativePath.Split('/') ]));
            Assert.DoesNotContain("TemporaryPassword", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PasswordHash", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RememberedLogin", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Base64", source, StringComparison.OrdinalIgnoreCase);
        }

        var viewModel = File.ReadAllText(RepositoryLocator.GetPath("src", "POS.Wpf", "ViewModels", "EmployeeManagementViewModel.cs"));
        var eventSectionStart = viewModel.IndexOf("public sealed class EmployeeAccountSuccessEventArgs", StringComparison.Ordinal);
        var eventSectionEnd = viewModel.IndexOf("public sealed class EmployeeOperationToastEventArgs", eventSectionStart, StringComparison.Ordinal);
        Assert.True(eventSectionStart >= 0);
        Assert.True(eventSectionEnd > eventSectionStart);
        var successEventSection = viewModel[eventSectionStart..eventSectionEnd];
        Assert.DoesNotContain("TemporaryPassword", successEventSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", successEventSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RememberedLogin", successEventSection, StringComparison.OrdinalIgnoreCase);
    }
}
