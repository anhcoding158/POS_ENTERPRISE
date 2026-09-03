using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Employees;
using POS.Domain.Enums;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class EmployeeManagementFeedbackTests
{
    private const string FinalAdministratorMessage = "Không thể thực hiện vì đây là Administrator cuối cùng còn sử dụng được.";

    [Fact]
    public void Final_administrator_rejections_publish_error_for_all_four_security_commands_without_success()
    {
        using var fixture = CreateFixture(succeedMutations: false);
        var errors = new List<EmployeeOperationErrorEventArgs>();
        var successToasts = new List<EmployeeOperationToastEventArgs>();
        fixture.ViewModel.ErrorNotificationRequested += (_, args) => errors.Add(args);
        fixture.ViewModel.ToastRequested += (_, args) => successToasts.Add(args);

        var originalState = (fixture.ViewModel.AccountStatusText, fixture.ViewModel.EmployeeStatusText, fixture.ViewModel.RoleText);
        var commands = new[]
        {
            fixture.ViewModel.ToggleLockCommand,
            fixture.ViewModel.ToggleAccountActiveCommand,
            fixture.ViewModel.ToggleActiveCommand,
            fixture.ViewModel.ChangeRoleCommand
        };

        foreach (var command in commands)
        {
            Assert.True(command.CanExecute(null));
            command.Execute(null);
            WaitForCompletion(command);
        }

        Assert.Equal(4, errors.Count);
        Assert.All(errors, error =>
        {
            Assert.Equal("Không thể thực hiện", error.Title);
            Assert.Equal(FinalAdministratorMessage, error.Message);
        });
        Assert.Empty(successToasts);
        Assert.Equal(originalState, (fixture.ViewModel.AccountStatusText, fixture.ViewModel.EmployeeStatusText, fixture.ViewModel.RoleText));
        Assert.True(fixture.ViewModel.IsStatusError);
        Assert.Equal(FinalAdministratorMessage, fixture.ViewModel.StatusMessage);
    }

    [Fact]
    public void A_successful_security_mutation_keeps_success_feedback_without_error_notification()
    {
        using var fixture = CreateFixture(succeedMutations: true);
        var errors = new List<EmployeeOperationErrorEventArgs>();
        var successToasts = new List<EmployeeOperationToastEventArgs>();
        fixture.ViewModel.ErrorNotificationRequested += (_, args) => errors.Add(args);
        fixture.ViewModel.ToastRequested += (_, args) => successToasts.Add(args);

        fixture.ViewModel.ToggleLockCommand.Execute(null);
        WaitForCompletion(fixture.ViewModel.ToggleLockCommand);

        Assert.Empty(errors);
        var toast = Assert.Single(successToasts);
        Assert.Equal("Đã khóa tài khoản employee.test.", toast.Message);
        Assert.False(fixture.ViewModel.IsStatusError);
        Assert.Equal(string.Empty, fixture.ViewModel.StatusMessage);
    }

    [Fact]
    public async Task Successful_employee_list_load_clears_loading_status_after_detail_selection()
    {
        using var fixture = CreateFixture(succeedMutations: true);

        await fixture.ViewModel.InitializeAsync();

        Assert.True(fixture.ViewModel.IsLoaded);
        Assert.False(fixture.ViewModel.IsLoadingState);
        Assert.False(fixture.ViewModel.IsStatusVisible);
        Assert.Equal(string.Empty, fixture.ViewModel.StatusMessage);
    }

    [Fact]
    public void Employee_window_presents_owned_error_and_uses_status_error_severity()
    {
        var root = RepositoryLocator.GetPath();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "EmployeeManagementWindow.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "EmployeeManagementWindow.xaml.cs"));

        Assert.Contains("DataTrigger Binding=\"{Binding IsStatusError}\" Value=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("EmployeeStatusTextStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding StatusMessage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ErrorNotificationRequested += OnErrorNotificationRequested", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ErrorNotificationRequested -= OnErrorNotificationRequested", codeBehind, StringComparison.Ordinal);
        Assert.Contains("global::System.Windows.MessageBox.Show(", codeBehind, StringComparison.Ordinal);
        Assert.Contains("this,", codeBehind, StringComparison.Ordinal);
        Assert.Contains("e.Message", codeBehind, StringComparison.Ordinal);
        Assert.Contains("e.Title", codeBehind, StringComparison.Ordinal);
        Assert.Contains("MessageBoxButton.OK", codeBehind, StringComparison.Ordinal);
        Assert.Contains("MessageBoxImage.Warning", codeBehind, StringComparison.Ordinal);
    }

    private static Fixture CreateFixture(bool succeedMutations)
    {
        var service = new FakeEmployeeAccountService(succeedMutations);
        var viewModel = new EmployeeManagementViewModel(
            new FakeScopeFactory(service),
            new AllowAllPermissionService(),
            NullLogger<EmployeeManagementViewModel>.Instance);

        SetDetails(viewModel, service.Details);
        return new Fixture(viewModel, service);
    }

    private static void SetDetails(EmployeeManagementViewModel viewModel, EmployeeDetailsDto details) =>
        typeof(EmployeeManagementViewModel)
            .GetField("_details", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, details);

    private static void WaitForCompletion(POS.Wpf.Commands.AsyncRelayCommand command)
    {
        Assert.True(SpinWait.SpinUntil(() => !command.IsExecuting, TimeSpan.FromSeconds(2)));
    }

    private sealed record Fixture(EmployeeManagementViewModel ViewModel, FakeEmployeeAccountService Service) : IDisposable
    {
        public void Dispose() => ViewModel.Dispose();
    }

    private sealed class FakeScopeFactory(IEmployeeAccountService service) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeScope(service);
    }

    private sealed class FakeScope(IEmployeeAccountService service) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider(service);
        public void Dispose() { }
    }

    private sealed class FakeServiceProvider(IEmployeeAccountService service) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IEmployeeAccountService) ? service : null;
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public bool HasPermission(SystemCapability permission) => true;
        public Result Authorize(SystemCapability permission) => Result.Success();
    }

    private sealed class FakeEmployeeAccountService(bool succeedMutations) : IEmployeeAccountService
    {
        private static readonly AppError FinalAdministratorError = new("CONFLICT", FinalAdministratorMessage);
        private static readonly AppError NotUsedError = new("NOT_USED", "Không dùng trong test.");

        public EmployeeDetailsDto Details { get; } = new(
            1, "EMP-000001", "Employee Test", "+84 900 000 001", "employee.test@example.invalid",
            EmployeeStatus.Active, 1, "employee.test", AccountStatus.Active, Role.Administrator,
            null, 0, false, false, DateTimeOffset.UtcNow, [], null);

        public Task<Result<PagedResult<EmployeeListItemDto>>> SearchAsync(EmployeeSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new PagedResult<EmployeeListItemDto>(
                [new EmployeeListItemDto(Details.Id, Details.EmployeeCode, Details.FullName, Details.PhoneNumber,
                    Details.EmployeeStatus, Details.UserId, Details.Username, Details.AccountStatus, Details.Role,
                    Details.LastSuccessfulLoginUtc, Details.FailedLoginAttempts, Details.UpdatedAtUtc, Details.LastFailedLoginUtc)],
                request.PageNumber, request.PageSize, 1)));

        public Task<Result<EmployeeSummaryDto>> GetSummaryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new EmployeeSummaryDto(1, 1, 0)));

        public Task<Result<EmployeeDetailsDto>> GetAsync(int employeeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(Details));

        public Task<Result<EmployeeDetailsDto>> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<EmployeeDetailsDto>(NotUsedError));

        public Task<Result<EmployeeDetailsDto>> UpdateEmployeeAsync(UpdateEmployeeRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<EmployeeDetailsDto>(NotUsedError));

        public Task<Result<EmployeeDetailsDto>> CreateAccountAsync(CreateEmployeeAccountRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<EmployeeDetailsDto>(NotUsedError));

        public Task<Result> ResetPasswordAsync(ResetEmployeePasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure(NotUsedError));

        public Task<Result> SetAccountLockAsync(SetAccountLockRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(succeedMutations ? Result.Success() : Result.Failure(FinalAdministratorError));

        public Task<Result> SetEmployeeActiveAsync(SetEmployeeActiveRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(succeedMutations ? Result.Success() : Result.Failure(FinalAdministratorError));

        public Task<Result> SetAccountActiveAsync(SetAccountActiveRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(succeedMutations ? Result.Success() : Result.Failure(FinalAdministratorError));

        public Task<Result> ChangeRoleAsync(ChangeEmployeeRoleRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(succeedMutations ? Result.Success() : Result.Failure(FinalAdministratorError));

        public Task<Result> CompletePasswordChangeAsync(CompletePasswordChangeRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure(NotUsedError));
    }
}
