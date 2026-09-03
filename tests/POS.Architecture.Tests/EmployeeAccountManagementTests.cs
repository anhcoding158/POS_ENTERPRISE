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
        var accountAudit = Assert.Single(audits, audit => audit.Action == SecurityAuditAction.AccountCreated);
        var accountChanges = SecurityAuditChangeSet.Deserialize(accountAudit.BeforeValuesJson);
        Assert.Equal("Chưa có tài khoản", accountChanges.Single(change => change.FieldKey == "Trạng thái tài khoản").BeforeValue);
        Assert.Equal("Chờ nhân viên đổi mật khẩu lần đầu", accountChanges.Single(change => change.FieldKey == "Trạng thái tài khoản").AfterValue);
        Assert.DoesNotContain(temporaryPassword, accountAudit.BeforeValuesJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Employee_update_audits_only_normalized_changed_fields_and_masks_contact_values()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        var created = await fixture.Service.CreateEmployeeAsync(new CreateEmployeeRequest
        {
            EmployeeCode = "EMP-DIFF",
            FullName = "Nguyễn Văn B",
            PhoneNumber = "0900001234",
            EmailAddress = "before@example.com",
            Role = Role.Cashier
        });
        Assert.True(created.IsSuccess);

        var updated = await fixture.Service.UpdateEmployeeAsync(new UpdateEmployeeRequest
        {
            EmployeeId = created.Value.Id,
            ExpectedUpdatedAtUtc = created.Value.UpdatedAtUtc,
            EmployeeCode = " EMP-DIFF ",
            FullName = "Nguyễn Văn Bình",
            PhoneNumber = " 0900005678 ",
            EmailAddress = " after@example.com "
        });
        Assert.True(updated.IsSuccess, updated.IsFailure ? updated.AppError.Message : null);

        var audit = await database.Context.SecurityAuditEvents
            .Where(item => item.TargetEmployeeId == created.Value.Id && item.Action == SecurityAuditAction.EmployeeUpdated)
            .OrderByDescending(item => item.Id)
            .FirstAsync();
        var changes = SecurityAuditChangeSet.Deserialize(audit.BeforeValuesJson);
        Assert.Equal(["Họ tên", "Số điện thoại", "Email"], changes.Select(change => change.FieldKey).ToArray());
        Assert.Equal("Nguyễn Văn B", changes[0].BeforeValue);
        Assert.Equal("Nguyễn Văn Bình", changes[0].AfterValue);
        Assert.Equal("09••••1234", changes[1].BeforeValue);
        Assert.Equal("09••••5678", changes[1].AfterValue);
        Assert.Equal("b•••••@example.com", changes[2].BeforeValue);
        Assert.Equal("a••••@example.com", changes[2].AfterValue);
    }

    [Fact]
    public async Task Unlock_audit_contains_account_state_transition()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        var employee = await fixture.Service.CreateEmployeeAsync(new CreateEmployeeRequest
        {
            EmployeeCode = "EMP-UNLOCK",
            FullName = "Nhân viên mở khóa",
            Role = Role.Cashier
        });
        Assert.True(employee.IsSuccess);
        var account = await fixture.Service.CreateAccountAsync(new CreateEmployeeAccountRequest
        {
            EmployeeId = employee.Value.Id,
            ExpectedUpdatedAtUtc = employee.Value.UpdatedAtUtc,
            Username = "unlock.user",
            TemporaryPassword = EphemeralPassword(),
            Role = Role.Cashier
        });
        Assert.True(account.IsSuccess);
        var storedAccount = await database.Context.Users.SingleAsync(user => user.Username == "unlock.user");
        storedAccount.ChangePasswordHash(new BCryptPasswordHasher().HashPassword(EphemeralPassword()), Now);
        await database.Context.SaveChangesAsync();
        var locked = await fixture.Service.SetAccountLockAsync(new SetAccountLockRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = account.Value.UpdatedAtUtc,
            Locked = true
        });
        Assert.True(locked.IsSuccess);
        var lockedDetails = await fixture.Service.GetAsync(account.Value.Id);
        var unlocked = await fixture.Service.SetAccountLockAsync(new SetAccountLockRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = lockedDetails.Value.UpdatedAtUtc,
            Locked = false
        });
        Assert.True(unlocked.IsSuccess, unlocked.IsFailure ? unlocked.AppError.Message : null);

        var audit = await database.Context.SecurityAuditEvents
            .Where(item => item.TargetEmployeeId == account.Value.Id && item.Action == SecurityAuditAction.AccountUnlocked)
            .SingleAsync();
        var change = Assert.Single(SecurityAuditChangeSet.Deserialize(audit.BeforeValuesJson), item => item.FieldKey == "Trạng thái tài khoản");
        Assert.Equal("Đang khóa", change.BeforeValue);
        Assert.Equal("Đang hoạt động", change.AfterValue);
    }

    [Fact]
    public async Task Account_reactivation_is_independent_from_employee_lifecycle_and_preserves_first_login_state()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        var employee = await fixture.Service.CreateEmployeeAsync(new CreateEmployeeRequest
        {
            EmployeeCode = "EMP-ACCOUNT-LIFECYCLE",
            FullName = "Nhân viên kích hoạt tài khoản",
            Role = Role.Cashier
        });
        Assert.True(employee.IsSuccess);
        var account = await fixture.Service.CreateAccountAsync(new CreateEmployeeAccountRequest
        {
            EmployeeId = employee.Value.Id,
            ExpectedUpdatedAtUtc = employee.Value.UpdatedAtUtc,
            Username = "account.lifecycle",
            TemporaryPassword = EphemeralPassword(),
            Role = Role.Cashier
        });
        Assert.True(account.IsSuccess);

        var disabled = await fixture.Service.SetAccountActiveAsync(new SetAccountActiveRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = account.Value.UpdatedAtUtc,
            Active = false
        });
        Assert.True(disabled.IsSuccess, disabled.IsFailure ? disabled.AppError.Message : null);
        var disabledDetails = await fixture.Service.GetAsync(account.Value.Id);
        Assert.True(disabledDetails.IsSuccess);
        Assert.Equal(EmployeeStatus.Active, disabledDetails.Value.EmployeeStatus);
        Assert.Equal(AccountStatus.Disabled, disabledDetails.Value.AccountStatus);

        var reactivated = await fixture.Service.SetAccountActiveAsync(new SetAccountActiveRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = disabledDetails.Value.UpdatedAtUtc,
            Active = true
        });
        Assert.True(reactivated.IsSuccess, reactivated.IsFailure ? reactivated.AppError.Message : null);
        var activeDetails = await fixture.Service.GetAsync(account.Value.Id);
        Assert.True(activeDetails.IsSuccess);
        Assert.Equal(EmployeeStatus.Active, activeDetails.Value.EmployeeStatus);
        Assert.Equal(AccountStatus.ForcePasswordChange, activeDetails.Value.AccountStatus);

        var accountAudit = await database.Context.SecurityAuditEvents
            .Where(item => item.TargetEmployeeId == account.Value.Id && item.TargetType == "Tài khoản")
            .OrderByDescending(item => item.Id)
            .Take(2)
            .ToArrayAsync();
        Assert.Contains(accountAudit, item => item.Action == SecurityAuditAction.EmployeeDeactivated &&
            AuditPresentationResolver.ActionText(item.Action, item.BusinessArea, item.TargetType) == "Vô hiệu hóa tài khoản");
        var activationAudit = Assert.Single(accountAudit, item => item.Action == SecurityAuditAction.EmployeeReactivated);
        Assert.Equal("Kích hoạt lại tài khoản", AuditPresentationResolver.ActionText(activationAudit.Action, activationAudit.BusinessArea, activationAudit.TargetType));
        var stateChange = Assert.Single(SecurityAuditChangeSet.Deserialize(activationAudit.BeforeValuesJson), change => change.FieldKey == "Trạng thái tài khoản");
        Assert.Equal("Đã vô hiệu hóa", stateChange.BeforeValue);
        Assert.Equal("Chờ nhân viên đổi mật khẩu lần đầu", stateChange.AfterValue);
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
        fixture.RememberedLoginStore.TrySave(new RememberedLoginCredential(
            RememberedLoginCredential.CurrentVersion,
            target.Id,
            "stale-fingerprint",
            Now.AddDays(1)));

        var change = await fixture.Service.CompletePasswordChangeAsync(new CompletePasswordChangeRequest
        {
            NewPassword = changedPassword,
            ConfirmPassword = changedPassword
        });

        Assert.True(change.IsSuccess, change.IsFailure ? change.AppError.Message : null);
        Assert.False(fixture.CurrentUser.CurrentUser!.ForcePasswordChange);
        Assert.Null(fixture.RememberedLoginStore.Load());
        Assert.False((await database.Context.Users.SingleAsync(user => user.Id == target.Id)).ForcePasswordChange);
        Assert.Contains(
            await database.Context.SecurityAuditEvents.ToArrayAsync(),
            audit => audit.Action == SecurityAuditAction.ForcedPasswordChangeCompleted);
        var forcedChangeAudit = await database.Context.SecurityAuditEvents
            .SingleAsync(audit => audit.Action == SecurityAuditAction.ForcedPasswordChangeCompleted);
        var forcedChanges = SecurityAuditChangeSet.Deserialize(forcedChangeAudit.BeforeValuesJson);
        Assert.Equal("Có", Assert.Single(forcedChanges, change => change.FieldKey == "Yêu cầu đổi mật khẩu").BeforeValue);
        Assert.Equal("Không", Assert.Single(forcedChanges, change => change.FieldKey == "Yêu cầu đổi mật khẩu").AfterValue);
        foreach (var secret in new[] { temporaryPassword, resetPassword, changedPassword })
        {
            foreach (var audit in await database.Context.SecurityAuditEvents.ToArrayAsync())
            {
                Assert.DoesNotContain(secret, audit.BeforeValuesJson, StringComparison.Ordinal);
                Assert.DoesNotContain(secret, audit.AfterValuesJson, StringComparison.Ordinal);
            }
        }
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
    public async Task Final_administrator_guard_covers_account_disable_and_role_demotion()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        var employee = await fixture.Service.CreateEmployeeAsync(new CreateEmployeeRequest
        {
            EmployeeCode = "EMP-ADMIN-SECOND",
            FullName = "Quản trị viên thứ hai"
        });
        Assert.True(employee.IsSuccess);
        var account = await fixture.Service.CreateAccountAsync(new CreateEmployeeAccountRequest
        {
            EmployeeId = employee.Value.Id,
            ExpectedUpdatedAtUtc = employee.Value.UpdatedAtUtc,
            Username = "second.admin",
            TemporaryPassword = EphemeralPassword(),
            Role = Role.Administrator
        });
        Assert.True(account.IsSuccess);

        var disabled = await fixture.Service.SetAccountActiveAsync(new SetAccountActiveRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = account.Value.UpdatedAtUtc,
            Active = false
        });
        Assert.True(disabled.IsSuccess, disabled.IsFailure ? disabled.AppError.Message : null);

        var first = await fixture.Service.GetAsync((await fixture.Service.SearchAsync(new EmployeeSearchRequest
        {
            SearchTerm = "EMP-ADMIN",
            PageSize = 10
        })).Value.Items.Single(item => item.EmployeeCode == "EMP-ADMIN").Id);
        Assert.True(first.IsSuccess);
        var finalDisable = await fixture.Service.SetAccountActiveAsync(new SetAccountActiveRequest
        {
            EmployeeId = first.Value.Id,
            ExpectedUpdatedAtUtc = first.Value.UpdatedAtUtc,
            Active = false
        });
        Assert.True(finalDisable.IsFailure);
        Assert.Contains("Administrator cuối cùng", finalDisable.AppError.Message, StringComparison.Ordinal);

        var reactivated = await fixture.Service.SetAccountActiveAsync(new SetAccountActiveRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = (await fixture.Service.GetAsync(account.Value.Id)).Value.UpdatedAtUtc,
            Active = true
        });
        Assert.True(reactivated.IsSuccess, reactivated.IsFailure ? reactivated.AppError.Message : null);

        var secondDetails = await fixture.Service.GetAsync(account.Value.Id);
        Assert.True(secondDetails.IsSuccess);
        var demoted = await fixture.Service.ChangeRoleAsync(new ChangeEmployeeRoleRequest
        {
            EmployeeId = secondDetails.Value.Id,
            ExpectedUpdatedAtUtc = secondDetails.Value.UpdatedAtUtc,
            Role = Role.Manager
        });
        Assert.True(demoted.IsSuccess, demoted.IsFailure ? demoted.AppError.Message : null);

        first = await fixture.Service.GetAsync(first.Value.Id);
        Assert.True(first.IsSuccess);
        var finalDemotion = await fixture.Service.ChangeRoleAsync(new ChangeEmployeeRoleRequest
        {
            EmployeeId = first.Value.Id,
            ExpectedUpdatedAtUtc = first.Value.UpdatedAtUtc,
            Role = Role.Manager
        });
        Assert.True(finalDemotion.IsFailure);
        Assert.Contains("Administrator cuối cùng", finalDemotion.AppError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deactivated_administrator_is_not_counted_and_stale_mutation_is_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        var employee = await fixture.Service.CreateEmployeeAsync(new CreateEmployeeRequest
        {
            EmployeeCode = "EMP-ADMIN-STALE",
            FullName = "Quản trị viên kiểm tra phiên bản"
        });
        Assert.True(employee.IsSuccess);
        var account = await fixture.Service.CreateAccountAsync(new CreateEmployeeAccountRequest
        {
            EmployeeId = employee.Value.Id,
            ExpectedUpdatedAtUtc = employee.Value.UpdatedAtUtc,
            Username = "stale.admin",
            TemporaryPassword = EphemeralPassword(),
            Role = Role.Administrator
        });
        Assert.True(account.IsSuccess);

        var secondDetails = await fixture.Service.GetAsync(account.Value.Id);
        Assert.True(secondDetails.IsSuccess);
        var deactivated = await fixture.Service.SetEmployeeActiveAsync(new SetEmployeeActiveRequest
        {
            EmployeeId = secondDetails.Value.Id,
            ExpectedUpdatedAtUtc = secondDetails.Value.UpdatedAtUtc,
            Active = false
        });
        Assert.True(deactivated.IsSuccess, deactivated.IsFailure ? deactivated.AppError.Message : null);

        var firstDetails = await fixture.Service.SearchAsync(new EmployeeSearchRequest
        {
            EmployeeStatus = EmployeeStatus.Active,
            Role = Role.Administrator,
            PageSize = 10
        });
        Assert.True(firstDetails.IsSuccess);
        var finalAdmin = Assert.Single(firstDetails.Value.Items);
        var finalGuard = await fixture.Service.SetAccountLockAsync(new SetAccountLockRequest
        {
            EmployeeId = finalAdmin.Id,
            ExpectedUpdatedAtUtc = finalAdmin.UpdatedAtUtc,
            Locked = true
        });
        Assert.True(finalGuard.IsFailure);
        Assert.Contains("Administrator cuối cùng", finalGuard.AppError.Message, StringComparison.Ordinal);

        var staleUpdate = await fixture.Service.SetEmployeeActiveAsync(new SetEmployeeActiveRequest
        {
            EmployeeId = finalAdmin.Id,
            ExpectedUpdatedAtUtc = finalAdmin.UpdatedAtUtc.AddTicks(-1),
            Active = true
        });
        Assert.True(staleUpdate.IsFailure);
        Assert.Equal(ErrorCodes.General.Conflict, staleUpdate.AppError.Code);
    }

    [Fact]
    public async Task Current_account_security_mutations_revoke_local_remembered_credential()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        var employee = await fixture.Service.CreateEmployeeAsync(new CreateEmployeeRequest
        {
            EmployeeCode = "EMP-REVOKE",
            FullName = "Tài khoản thu hồi phiên"
        });
        Assert.True(employee.IsSuccess);
        var account = await fixture.Service.CreateAccountAsync(new CreateEmployeeAccountRequest
        {
            EmployeeId = employee.Value.Id,
            ExpectedUpdatedAtUtc = employee.Value.UpdatedAtUtc,
            Username = "revoke.admin",
            TemporaryPassword = EphemeralPassword(),
            Role = Role.Administrator
        });
        Assert.True(account.IsSuccess);
        var target = await database.Context.Users.SingleAsync(user => user.Username == "revoke.admin");
        var owner = await database.Context.Users.SingleAsync(user => user.Username == "admin");

        void AuthenticateTarget(bool forcePasswordChange = false) => fixture.CurrentUser.SetCurrentUser(
            new AuthenticatedUserDto(target.Id, target.Username, target.FullName, target.Role, Now, forcePasswordChange));
        void AuthenticateOwner() => fixture.CurrentUser.SetCurrentUser(
            new AuthenticatedUserDto(owner.Id, owner.Username, owner.FullName, owner.Role, Now));
        async Task<EmployeeDetailsDto> LoadTarget()
        {
            AuthenticateOwner();
            var loaded = await fixture.Service.GetAsync(account.Value.Id);
            Assert.True(loaded.IsSuccess, loaded.IsFailure ? loaded.AppError.Message : null);
            return loaded.Value;
        }
        void SeedRememberedCredential() => fixture.RememberedLoginStore.TrySave(new RememberedLoginCredential(
            RememberedLoginCredential.CurrentVersion, target.Id, "local-fingerprint", Now.AddDays(1)));

        AuthenticateTarget(true);
        SeedRememberedCredential();
        var reset = await fixture.Service.ResetPasswordAsync(new ResetEmployeePasswordRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = account.Value.UpdatedAtUtc,
            TemporaryPassword = EphemeralPassword()
        });
        Assert.True(reset.IsSuccess);
        Assert.Null(fixture.RememberedLoginStore.Load());
        Assert.False(fixture.CurrentUser.IsAuthenticated);

        var details = await LoadTarget();
        AuthenticateTarget(true);
        SeedRememberedCredential();
        var locked = await fixture.Service.SetAccountLockAsync(new SetAccountLockRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = details.UpdatedAtUtc,
            Locked = true
        });
        Assert.True(locked.IsSuccess);
        Assert.Null(fixture.RememberedLoginStore.Load());
        Assert.False(fixture.CurrentUser.IsAuthenticated);

        details = await LoadTarget();
        AuthenticateTarget(true);
        var unlocked = await fixture.Service.SetAccountLockAsync(new SetAccountLockRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = details.UpdatedAtUtc,
            Locked = false
        });
        Assert.True(unlocked.IsSuccess);

        details = await LoadTarget();
        AuthenticateTarget(true);
        SeedRememberedCredential();
        var disabled = await fixture.Service.SetAccountActiveAsync(new SetAccountActiveRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = details.UpdatedAtUtc,
            Active = false
        });
        Assert.True(disabled.IsSuccess);
        Assert.Null(fixture.RememberedLoginStore.Load());
        Assert.False(fixture.CurrentUser.IsAuthenticated);

        details = await LoadTarget();
        AuthenticateTarget(true);
        var enabled = await fixture.Service.SetAccountActiveAsync(new SetAccountActiveRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = details.UpdatedAtUtc,
            Active = true
        });
        Assert.True(enabled.IsSuccess);

        details = await LoadTarget();
        AuthenticateTarget(true);
        SeedRememberedCredential();
        var deactivated = await fixture.Service.SetEmployeeActiveAsync(new SetEmployeeActiveRequest
        {
            EmployeeId = account.Value.Id,
            ExpectedUpdatedAtUtc = details.UpdatedAtUtc,
            Active = false
        });
        Assert.True(deactivated.IsSuccess);
        Assert.Null(fixture.RememberedLoginStore.Load());
        Assert.False(fixture.CurrentUser.IsAuthenticated);
    }

    [Fact]
    public async Task Known_wrong_password_persists_failure_audit_without_credential_material()
    {
        await using var database = await TestDatabase.CreateAsync();
        var fixture = await database.CreateAdministratorFixtureAsync();
        var hasher = new BCryptPasswordHasher();
        var auth = new AuthService(
            new UserRepository(database.Context),
            hasher,
            new EfUnitOfWork(database.Context),
            fixture.CurrentUser,
            new FixedClock(Now),
            new InMemoryRememberedLoginStore(),
            new SecurityAuditRepository(database.Context));

        var result = await auth.LoginAsync(new LoginRequest("admin", "definitely-wrong"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.Authentication.InvalidCredentials, result.AppError.Code);
        var persisted = await database.Context.SecurityAuditEvents
            .Where(audit => audit.Action == SecurityAuditAction.LoginFailed)
            .ToArrayAsync();
        Assert.Single(persisted);
        Assert.DoesNotContain("definitely-wrong", persisted[0].BeforeValuesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("definitely-wrong", persisted[0].AfterValuesJson, StringComparison.Ordinal);
        var changes = SecurityAuditChangeSet.Deserialize(persisted[0].BeforeValuesJson);
        Assert.Equal("1", Assert.Single(changes, change => change.FieldKey == "Sai liên tiếp").AfterValue);
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
            var rememberedLoginStore = new InMemoryRememberedLoginStore();
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
                new FixedClock(Now),
                rememberedLoginStore: rememberedLoginStore);
            return new Fixture(service, currentUser, rememberedLoginStore);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed record Fixture(
        EmployeeAccountService Service,
        CurrentUserService CurrentUser,
        InMemoryRememberedLoginStore RememberedLoginStore);

    private sealed class InMemoryRememberedLoginStore : IRememberedLoginStore
    {
        private RememberedLoginCredential? _credential;

        public RememberedLoginCredential? Load() => _credential;
        public bool TrySave(RememberedLoginCredential credential) { _credential = credential; return true; }
        public bool TryDelete() { _credential = null; return true; }
    }

    private static string EphemeralPassword() => "R4" + Guid.NewGuid().ToString("N") + "A1!";

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
