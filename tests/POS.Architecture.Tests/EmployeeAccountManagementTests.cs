using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Employees;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Common;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class EmployeeAccountManagementTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Administrator_can_create_employee_and_optional_account_without_plaintext()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        var temporaryPassword = EphemeralPassword();

        var create = await fixture.Service.CreateEmployeeAsync(new CreateEmployeeRequest
        {
            EmployeeCode = " EMP-100 ",
            FullName = "Nhân viên bán hàng",
            PhoneNumber = "0900000000",
            CreateAccount = true,
            Username = "cashier.one",
            TemporaryPassword = temporaryPassword,
            Role = Role.Cashier
        });

        Assert.True(create.IsSuccess, create.IsFailure ? create.AppError.Message : null);
        Assert.NotNull(create.Value);
        Assert.Equal("EMP-100", create.Value.EmployeeCode);
        Assert.Equal(AccountStatus.ForcePasswordChange, create.Value.AccountStatus);

        var stored = await database.Context.Users.SingleAsync(user => user.Username == "cashier.one");
        Assert.NotEqual(temporaryPassword, stored.PasswordHash);
        Assert.True(stored.ForcePasswordChange);
        Assert.Equal(Role.Cashier, stored.Role);

        var audits = await database.Context.SecurityAuditEvents
            .Where(audit => audit.TargetEmployeeId == create.Value.Id)
            .ToArrayAsync();
        Assert.Contains(audits, audit => audit.Action == SecurityAuditAction.EmployeeCreated);
        Assert.Contains(audits, audit => audit.Action == SecurityAuditAction.AccountCreated);
        Assert.DoesNotContain(audits, audit => audit.Result.Contains(temporaryPassword, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_normalizes_term_filters_and_paginates_stably()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();

        foreach (var item in new[] { ("EMP-001", "Alice"), ("EMP-002", "Bob"), ("EMP-003", "Carol") })
        {
            var result = await fixture.Service.CreateEmployeeAsync(new CreateEmployeeRequest
            {
                EmployeeCode = item.Item1,
                FullName = item.Item2,
                Role = Role.Cashier
            });
            Assert.True(result.IsSuccess);
        }

        var page = await fixture.Service.SearchAsync(new EmployeeSearchRequest
        {
            SearchTerm = "  bob ",
            EmployeeStatus = EmployeeStatus.Active,
            AccountStatus = AccountStatus.NoAccount,
            PageNumber = 1,
            PageSize = 1
        });

        Assert.True(page.IsSuccess);
        Assert.Single(page.Value.Items);
        Assert.Equal("EMP-002", page.Value.Items[0].EmployeeCode);
        Assert.Equal(1, page.Value.TotalCount);
    }

    [Fact]
    public async Task Account_creation_reset_and_forced_password_change_are_atomic_and_audited()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        var temporaryPassword = EphemeralPassword();
        var resetPassword = EphemeralPassword();
        var changedPassword = EphemeralPassword();

        var employee = await fixture.Service.CreateEmployeeAsync(new CreateEmployeeRequest
        {
            EmployeeCode = "EMP-FORCE",
            FullName = "Tài khoản bắt buộc đổi mật khẩu"
        });
        Assert.True(employee.IsSuccess);

        var account = await fixture.Service.CreateAccountAsync(new CreateEmployeeAccountRequest
        {
            EmployeeId = employee.Value.Id,
            ExpectedUpdatedAtUtc = employee.Value.UpdatedAtUtc,
            Username = "force.user",
            TemporaryPassword = temporaryPassword,
            Role = Role.Cashier
        });
        Assert.True(account.IsSuccess, account.IsFailure ? account.AppError.Message : null);
        Assert.True(account.Value.ForcePasswordChange);

        var reset = await fixture.Service.ResetPasswordAsync(new ResetEmployeePasswordRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = account.Value.UpdatedAtUtc,
            TemporaryPassword = resetPassword
        });
        Assert.True(reset.IsSuccess, reset.IsFailure ? reset.AppError.Message : null);

        var target = await database.Context.Users.SingleAsync(user => user.Username == "force.user");
        fixture.CurrentUser.SetCurrentUser(new AuthenticatedUserDto(
            target.Id, target.Username, target.FullName, target.Role, Now, forcePasswordChange: true));

        var change = await fixture.Service.CompletePasswordChangeAsync(new CompletePasswordChangeRequest
        {
            NewPassword = changedPassword,
            ConfirmPassword = changedPassword
        });

        Assert.True(change.IsSuccess, change.IsFailure ? change.AppError.Message : null);
        Assert.False(fixture.CurrentUser.CurrentUser!.ForcePasswordChange);
        Assert.False((await database.Context.Users.SingleAsync(user => user.Id == target.Id)).ForcePasswordChange);
        Assert.Contains(
            await database.Context.SecurityAuditEvents.ToArrayAsync(),
            audit => audit.Action == SecurityAuditAction.ForcedPasswordChangeCompleted);
    }

    [Fact]
    public async Task Final_administrator_cannot_be_locked_or_deactivated()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        var admin = await fixture.Service.SearchAsync(new EmployeeSearchRequest
        {
            Role = Role.Administrator,
            PageSize = 10
        });
        Assert.True(admin.IsSuccess);
        var administrator = Assert.Single(admin.Value.Items);

        var lockResult = await fixture.Service.SetAccountLockAsync(new SetAccountLockRequest
        {
            EmployeeId = administrator.Id,
            ExpectedUpdatedAtUtc = administrator.UpdatedAtUtc,
            Locked = true
        });
        Assert.True(lockResult.IsFailure);
        Assert.Contains("Administrator cuối cùng", lockResult.AppError.Message, StringComparison.Ordinal);

        var deactivateResult = await fixture.Service.SetEmployeeActiveAsync(new SetEmployeeActiveRequest
        {
            EmployeeId = administrator.Id,
            ExpectedUpdatedAtUtc = administrator.UpdatedAtUtc,
            Active = false
        });
        Assert.True(deactivateResult.IsFailure);
        Assert.Contains("Administrator cuối cùng", deactivateResult.AppError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_authorized_user_is_denied_at_application_boundary()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        fixture.CurrentUser.SetCurrentUser(new AuthenticatedUserDto(99, "cashier", "Thu ngân", Role.Cashier, Now));

        var result = await fixture.Service.SearchAsync(new EmployeeSearchRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.General.Forbidden, result.AppError.Code);
    }

    [Fact]
    public void Password_policy_is_centralized_and_never_returns_credential_material()
    {
        var invalidPassword = new string('x', 3);
        var result = POS.Application.Authentication.PasswordPolicy.Validate(invalidPassword, "cashier");

        Assert.False(result.IsValid);
        Assert.DoesNotContain(invalidPassword, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cashier", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection, PosDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public PosDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new PosDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async Task<Fixture> CreateAdministratorFixtureAsync()
        {
            var hasher = new BCryptPasswordHasher();
            var user = new User("admin", hasher.HashPassword(EphemeralPassword()), "Quản trị viên", Role.Administrator, Now);
            var employee = new Employee("EMP-ADMIN", user.FullName, null, null, Now);
            employee.AttachAccount(user, Now);
            Context.Employees.Add(employee);
            await Context.SaveChangesAsync();

            var currentUser = new CurrentUserService();
            currentUser.SetCurrentUser(new AuthenticatedUserDto(user.Id, user.Username, user.FullName, user.Role, Now));
            var employeeRepository = new EmployeeRepository(Context);
            var userRepository = new UserRepository(Context);
            var service = new EmployeeAccountService(
                employeeRepository,
                userRepository,
                new SecurityAuditRepository(Context),
                new EfUnitOfWork(Context),
                hasher,
                currentUser,
                new PermissionService(currentUser),
                new FixedClock(Now));
            return new Fixture(service, currentUser);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed record Fixture(EmployeeAccountService Service, CurrentUserService CurrentUser);

    private static string EphemeralPassword() => "R4" + Guid.NewGuid().ToString("N") + "A1!";

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
