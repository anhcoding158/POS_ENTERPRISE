using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Authentication;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Employees;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

/// <summary>
/// Employee/account use cases. Authorization is enforced here and not only in WPF.
/// </summary>
public sealed class EmployeeAccountService : IEmployeeAccountService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISecurityAuditRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPermissionService _permissionService;
    private readonly IClock _clock;

    public EmployeeAccountService(
        IEmployeeRepository employeeRepository,
        IUserRepository userRepository,
        ISecurityAuditRepository auditRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService,
        IPermissionService permissionService,
        IClock clock)
    {
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<PagedResult<EmployeeListItemDto>>> SearchAsync(
        EmployeeSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = _permissionService.Authorize(SystemCapability.ViewEmployees);
        if (authorization.IsFailure) return Result.Failure<PagedResult<EmployeeListItemDto>>(authorization.AppError);

        try
        {
            var page = await _employeeRepository.SearchAsync(
                request.SearchTerm,
                request.EmployeeStatus,
                request.AccountStatus,
                request.Role,
                request.PageNumber,
                request.PageSize,
                _clock.UtcNow,
                cancellationToken);
            return Result.Success(page.Map(MapListItem));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Failure<PagedResult<EmployeeListItemDto>>(ErrorCodes.General.Validation, exception.Message);
        }
    }

    public async Task<Result<EmployeeDetailsDto>> GetAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var authorization = _permissionService.Authorize(SystemCapability.ViewEmployees);
        if (authorization.IsFailure) return Result.Failure<EmployeeDetailsDto>(authorization.AppError);
        var employee = await _employeeRepository.GetByIdWithAccountAsync(employeeId, cancellationToken);
        return employee is null
            ? Failure<EmployeeDetailsDto>(ErrorCodes.General.NotFound, "Không tìm thấy nhân viên.")
            : Result.Success(MapDetails(employee));
    }

    public async Task<Result<EmployeeDetailsDto>> CreateEmployeeAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = _permissionService.Authorize(SystemCapability.ManageEmployees);
        if (authorization.IsFailure) return Result.Failure<EmployeeDetailsDto>(authorization.AppError);

        var validation = ValidateEmployeeCode(request.EmployeeCode, null);
        if (validation is not null) return Failure<EmployeeDetailsDto>(ErrorCodes.General.Validation, validation);
        if (string.IsNullOrWhiteSpace(request.FullName)) return Failure<EmployeeDetailsDto>(ErrorCodes.General.Validation, "Vui lòng nhập họ tên nhân viên.");
        if (!Enum.IsDefined(request.Role)) return Failure<EmployeeDetailsDto>(ErrorCodes.General.Validation, "Vai trò nhân viên không hợp lệ.");

        User? account = null;
        if (request.CreateAccount)
        {
            var accountAuthorization = _permissionService.Authorize(SystemCapability.ManageAccounts);
            if (accountAuthorization.IsFailure) return Result.Failure<EmployeeDetailsDto>(accountAuthorization.AppError);
            var accountValidation = ValidateAccountInput(request.Username, request.TemporaryPassword, request.Role);
            if (accountValidation is not null) return Failure<EmployeeDetailsDto>(ErrorCodes.General.Validation, accountValidation);
            if (await _userRepository.NormalizedUsernameExistsAsync(request.Username!, cancellationToken: cancellationToken))
                return Failure<EmployeeDetailsDto>(ErrorCodes.General.Conflict, "Tên đăng nhập đã tồn tại.");
            account = new User(request.Username!, _passwordHasher.HashPassword(request.TemporaryPassword!), request.FullName, request.Role, _clock.UtcNow);
            account.MarkPasswordChangeRequired(_clock.UtcNow);
        }

        Employee employee;
        try
        {
            employee = new Employee(request.EmployeeCode, request.FullName, request.PhoneNumber, request.EmailAddress, _clock.UtcNow);
            if (account is not null) employee.AttachAccount(account, _clock.UtcNow);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return Failure<EmployeeDetailsDto>(ErrorCodes.General.Validation, exception.Message);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _employeeRepository.AddAsync(employee, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _auditRepository.AddAsync(new SecurityAuditEvent(
                ActorId(), employee.Id, employee.LoginAccount?.Id,
                SecurityAuditAction.EmployeeCreated, "Success", Guid.NewGuid(), _clock.UtcNow), cancellationToken);
            if (account is not null)
            {
                await _auditRepository.AddAsync(new SecurityAuditEvent(
                    ActorId(), employee.Id, employee.LoginAccount?.Id,
                    SecurityAuditAction.AccountCreated, "Success", Guid.NewGuid(), _clock.UtcNow), cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(MapDetails(employee));
        }
        catch (PersistenceConflictException exception)
        {
            return Failure<EmployeeDetailsDto>(ErrorCodes.General.Conflict, ConflictMessage(exception));
        }
    }

    public async Task<Result<EmployeeDetailsDto>> UpdateEmployeeAsync(
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = _permissionService.Authorize(SystemCapability.ManageEmployees);
        if (authorization.IsFailure) return Result.Failure<EmployeeDetailsDto>(authorization.AppError);
        var employee = await _employeeRepository.GetByIdWithAccountAsync(request.EmployeeId, cancellationToken);
        if (employee is null) return Failure<EmployeeDetailsDto>(ErrorCodes.General.NotFound, "Không tìm thấy nhân viên.");
        if (!IsExpectedVersion(employee.UpdatedAtUtc, request.ExpectedUpdatedAtUtc)) return Conflict<EmployeeDetailsDto>();
        var codeError = ValidateEmployeeCode(request.EmployeeCode, employee.Id);
        if (codeError is not null) return Failure<EmployeeDetailsDto>(ErrorCodes.General.Validation, codeError);

        try
        {
            if (await _employeeRepository.NormalizedEmployeeCodeExistsAsync(request.EmployeeCode, employee.Id, cancellationToken))
                return Failure<EmployeeDetailsDto>(ErrorCodes.General.Conflict, "Mã nhân viên đã tồn tại.");
            employee.UpdateProfile(request.EmployeeCode, request.FullName, request.PhoneNumber, request.EmailAddress, _clock.UtcNow);
            employee.LoginAccount?.UpdateProfile(request.FullName, employee.LoginAccount.Role, _clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Failure<EmployeeDetailsDto>(ErrorCodes.General.Validation, exception.Message);
        }

        return await SaveMutationAsync(employee, SecurityAuditAction.EmployeeUpdated, "Success", cancellationToken);
    }

    public async Task<Result<EmployeeDetailsDto>> CreateAccountAsync(
        CreateEmployeeAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = _permissionService.Authorize(SystemCapability.ManageAccounts);
        if (authorization.IsFailure) return Result.Failure<EmployeeDetailsDto>(authorization.AppError);
        var employee = await _employeeRepository.GetByIdWithAccountAsync(request.EmployeeId, cancellationToken);
        if (employee is null) return Failure<EmployeeDetailsDto>(ErrorCodes.General.NotFound, "Không tìm thấy nhân viên.");
        if (!employee.IsActive) return Failure<EmployeeDetailsDto>(ErrorCodes.General.Validation, "Không thể cấp tài khoản cho nhân viên đã ngừng hoạt động.");
        if (employee.LoginAccount is not null) return Failure<EmployeeDetailsDto>(ErrorCodes.General.Conflict, "Nhân viên đã có tài khoản đăng nhập.");
        if (!IsExpectedVersion(employee.UpdatedAtUtc, request.ExpectedUpdatedAtUtc)) return Conflict<EmployeeDetailsDto>();
        var accountError = ValidateAccountInput(request.Username, request.TemporaryPassword, request.Role);
        if (accountError is not null) return Failure<EmployeeDetailsDto>(ErrorCodes.General.Validation, accountError);
        if (await _userRepository.NormalizedUsernameExistsAsync(request.Username, cancellationToken: cancellationToken))
            return Failure<EmployeeDetailsDto>(ErrorCodes.General.Conflict, "Tên đăng nhập đã tồn tại.");

        var account = new User(request.Username, _passwordHasher.HashPassword(request.TemporaryPassword), employee.FullName, request.Role, _clock.UtcNow);
        account.MarkPasswordChangeRequired(_clock.UtcNow);
        employee.AttachAccount(account, _clock.UtcNow);
        return await SaveMutationAsync(employee, SecurityAuditAction.AccountCreated, "Success", cancellationToken);
    }

    public async Task<Result> ResetPasswordAsync(
        ResetEmployeePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = _permissionService.Authorize(SystemCapability.ResetPasswords);
        if (authorization.IsFailure) return authorization;
        var employee = await _employeeRepository.GetByIdWithAccountAsync(request.EmployeeId, cancellationToken);
        if (employee?.LoginAccount is null) return Failure(ErrorCodes.General.NotFound, "Không tìm thấy tài khoản đăng nhập.");
        if (!IsExpectedVersion(employee.UpdatedAtUtc, request.ExpectedUpdatedAtUtc)) return Conflict();
        var passwordError = ValidatePassword(request.TemporaryPassword, employee.LoginAccount.Username);
        if (passwordError is not null) return Failure(ErrorCodes.General.Validation, passwordError);

        employee.LoginAccount.ResetPasswordHash(_passwordHasher.HashPassword(request.TemporaryPassword), _clock.UtcNow);
        return await SaveMutationResultAsync(employee, SecurityAuditAction.PasswordReset, cancellationToken);
    }

    public async Task<Result> SetAccountLockAsync(
        SetAccountLockRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = _permissionService.Authorize(SystemCapability.LockUnlockAccounts);
        if (authorization.IsFailure) return authorization;
        var employee = await _employeeRepository.GetByIdWithAccountAsync(request.EmployeeId, cancellationToken);
        if (employee?.LoginAccount is null) return Failure(ErrorCodes.General.NotFound, "Không tìm thấy tài khoản đăng nhập.");
        if (!IsExpectedVersion(employee.UpdatedAtUtc, request.ExpectedUpdatedAtUtc)) return Conflict();
        if (request.Locked)
        {
            var guard = await EnsureNotFinalAdministratorAsync(employee, cancellationToken);
            if (guard.IsFailure) return guard;
            employee.LoginAccount.ManualLock(_clock.UtcNow);
        }
        else
        {
            if (!employee.IsActive) return Failure(ErrorCodes.General.Validation, "Không thể mở khóa tài khoản của nhân viên đang ngừng hoạt động.");
            employee.LoginAccount.ManualUnlock(_clock.UtcNow);
            employee.LoginAccount.Activate(_clock.UtcNow);
        }
        return await SaveMutationResultAsync(employee, request.Locked ? SecurityAuditAction.AccountLocked : SecurityAuditAction.AccountUnlocked, cancellationToken);
    }

    public async Task<Result> SetEmployeeActiveAsync(
        SetEmployeeActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = _permissionService.Authorize(SystemCapability.ManageEmployees);
        if (authorization.IsFailure) return authorization;
        var employee = await _employeeRepository.GetByIdWithAccountAsync(request.EmployeeId, cancellationToken);
        if (employee is null) return Failure(ErrorCodes.General.NotFound, "Không tìm thấy nhân viên.");
        if (!IsExpectedVersion(employee.UpdatedAtUtc, request.ExpectedUpdatedAtUtc)) return Conflict();
        if (!request.Active)
        {
            var guard = await EnsureNotFinalAdministratorAsync(employee, cancellationToken);
            if (guard.IsFailure) return guard;
            employee.Deactivate(_clock.UtcNow);
            employee.LoginAccount?.Deactivate(_clock.UtcNow);
        }
        else
        {
            employee.Activate(_clock.UtcNow);
        }
        return await SaveMutationResultAsync(employee, request.Active ? SecurityAuditAction.EmployeeReactivated : SecurityAuditAction.EmployeeDeactivated, cancellationToken);
    }

    public async Task<Result> ChangeRoleAsync(
        ChangeEmployeeRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = _permissionService.Authorize(SystemCapability.AssignRolesPermissions);
        if (authorization.IsFailure) return authorization;
        if (!Enum.IsDefined(request.Role)) return Failure(ErrorCodes.General.Validation, "Vai trò không hợp lệ.");
        var employee = await _employeeRepository.GetByIdWithAccountAsync(request.EmployeeId, cancellationToken);
        if (employee?.LoginAccount is null) return Failure(ErrorCodes.General.NotFound, "Không tìm thấy tài khoản đăng nhập.");
        if (!IsExpectedVersion(employee.UpdatedAtUtc, request.ExpectedUpdatedAtUtc)) return Conflict();
        if (employee.LoginAccount.Role == request.Role) return Result.Success();
        if (employee.LoginAccount.Role == Role.Administrator && request.Role != Role.Administrator)
        {
            var guard = await EnsureNotFinalAdministratorAsync(employee, cancellationToken);
            if (guard.IsFailure) return guard;
        }
        employee.LoginAccount.UpdateProfile(employee.FullName, request.Role, _clock.UtcNow);
        return await SaveMutationResultAsync(employee, SecurityAuditAction.RoleChanged, cancellationToken);
    }

    public async Task<Result> CompletePasswordChangeAsync(
        CompletePasswordChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is not int userId)
            return Failure(ErrorCodes.General.Unauthorized, "Phiên đăng nhập không còn hợp lệ.");
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null) return Failure(ErrorCodes.Authentication.CurrentUserNotFound, "Không tìm thấy tài khoản hiện tại.");
        if (!user.ForcePasswordChange) return Failure(ErrorCodes.General.Conflict, "Tài khoản hiện tại không yêu cầu đổi mật khẩu.");
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            return Failure(ErrorCodes.General.Validation, "Mật khẩu xác nhận không khớp.");
        var passwordError = ValidatePassword(request.NewPassword, user.Username);
        if (passwordError is not null) return Failure(ErrorCodes.General.Validation, passwordError);

        user.ChangePasswordHash(_passwordHasher.HashPassword(request.NewPassword), _clock.UtcNow);
        await _auditRepository.AddAsync(new SecurityAuditEvent(userId, null, user.Id, SecurityAuditAction.ForcedPasswordChangeCompleted, "Success", Guid.NewGuid(), _clock.UtcNow), cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _currentUserService.SetCurrentUser(new POS.Application.DTOs.Authentication.AuthenticatedUserDto(
                user.Id,
                user.Username,
                user.FullName,
                user.Role,
                _currentUserService.CurrentUser!.AuthenticatedAtUtc,
                forcePasswordChange: false));
            return Result.Success();
        }
        catch (PersistenceConflictException exception)
        {
            return Failure(ErrorCodes.General.Conflict, ConflictMessage(exception));
        }
    }

    private async Task<Result<EmployeeDetailsDto>> SaveMutationAsync(Employee employee, SecurityAuditAction action, string result, CancellationToken cancellationToken)
    {
        var saved = await SaveMutationResultAsync(employee, action, cancellationToken);
        return saved.IsFailure ? Result.Failure<EmployeeDetailsDto>(saved.AppError) : Result.Success(MapDetails(employee));
    }

    private async Task<Result> SaveMutationResultAsync(Employee employee, SecurityAuditAction action, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // New account linkage assigns the User ID only when the graph is first persisted.
            // Save the mutation before constructing the audit event so every target identity is
            // durable and valid, while the surrounding transaction still keeps both writes atomic.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _auditRepository.AddAsync(new SecurityAuditEvent(ActorId(), employee.Id, employee.LoginAccount?.Id, action, "Success", Guid.NewGuid(), _clock.UtcNow), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (PersistenceConflictException exception)
        {
            return Failure(ErrorCodes.General.Conflict, ConflictMessage(exception));
        }
    }

    private async Task<Result> EnsureNotFinalAdministratorAsync(Employee employee, CancellationToken cancellationToken)
    {
        if (employee.LoginAccount?.Role != Role.Administrator) return Result.Success();
        if (await _employeeRepository.CountUsableAdministratorsAsync(_clock.UtcNow, cancellationToken) <= 1)
            return Failure(ErrorCodes.General.Conflict, "Không thể thực hiện vì đây là Administrator cuối cùng còn sử dụng được.");
        return Result.Success();
    }

    private int? ActorId() => _currentUserService.UserId;

    private static string? ValidateEmployeeCode(string? code, int? excludeId)
    {
        var value = code?.Trim() ?? string.Empty;
        return value.Length is < 2 or > 30 || value.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.')
            ? "Mã nhân viên không hợp lệ."
            : null;
    }

    private static string? ValidateAccountInput(string? username, string? password, Role role)
    {
        if (!Enum.IsDefined(role)) return "Vai trò không hợp lệ.";
        if (string.IsNullOrWhiteSpace(username)) return "Vui lòng nhập tên đăng nhập.";
        var trimmed = username.Trim();
        if (trimmed.Length is < 3 or > 50 || trimmed.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
            return "Tên đăng nhập không hợp lệ.";
        return ValidatePassword(password, trimmed);
    }

    private static string? ValidatePassword(string? password, string? username) =>
        PasswordPolicy.Validate(password, username).IsValid
            ? null
            : PasswordPolicy.Validate(password, username).ErrorMessage;

    private static bool IsExpectedVersion(DateTimeOffset actual, DateTimeOffset expected) =>
        expected != default && actual.ToUniversalTime() == expected.ToUniversalTime();

    private static AccountStatus GetAccountStatus(Employee employee, DateTimeOffset utcNow)
    {
        var user = employee.LoginAccount;
        if (user is null) return AccountStatus.NoAccount;
        if (!user.IsActive) return AccountStatus.Disabled;
        if (user.IsManuallyLocked || (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > utcNow)) return AccountStatus.Locked;
        return user.ForcePasswordChange ? AccountStatus.ForcePasswordChange : AccountStatus.Active;
    }

    private EmployeeListItemDto MapListItem(Employee employee)
    {
        var user = employee.LoginAccount;
        return new EmployeeListItemDto(employee.Id, employee.EmployeeCode, employee.FullName, employee.PhoneNumber,
            employee.IsActive ? EmployeeStatus.Active : EmployeeStatus.Inactive, employee.UserId,
            user?.Username, GetAccountStatus(employee, _clock.UtcNow), user?.Role,
            user?.LastLoginAtUtc, user?.FailedLoginAttempts ?? 0, employee.UpdatedAtUtc);
    }

    private EmployeeDetailsDto MapDetails(Employee employee)
    {
        var user = employee.LoginAccount;
        return new EmployeeDetailsDto(employee.Id, employee.EmployeeCode, employee.FullName, employee.PhoneNumber, employee.EmailAddress,
            employee.IsActive ? EmployeeStatus.Active : EmployeeStatus.Inactive, employee.UserId, user?.Username,
            GetAccountStatus(employee, _clock.UtcNow), user?.Role, user?.LastLoginAtUtc, user?.FailedLoginAttempts ?? 0,
            user?.IsManuallyLocked ?? false, user?.ForcePasswordChange ?? false, employee.UpdatedAtUtc,
            user is null ? Array.Empty<SystemCapability>() : RolePermissionPolicy.GetEffectivePermissions(user.Role));
    }

    private static Result<T> Failure<T>(string code, string message) => Result.Failure<T>(new AppError(code, message));
    private static Result Failure(string code, string message) => Result.Failure(new AppError(code, message));
    private static Result<T> Conflict<T>() => Failure<T>(ErrorCodes.General.Conflict, "Dữ liệu vừa được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
    private static Result Conflict() => Failure(ErrorCodes.General.Conflict, "Dữ liệu vừa được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
    private static string ConflictMessage(PersistenceConflictException exception) => exception.Target == PersistenceConflictTargets.EmployeeNormalizedCode ? "Mã nhân viên đã tồn tại." : "Dữ liệu vừa được thay đổi hoặc bị trùng.";
}
