using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Domain.Entities;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Application.Services;

/// <summary>
/// Xử lý đăng nhập, khóa tài khoản, phiên hiện tại
/// và đăng nhập được ghi nhớ.
///
/// Service không phụ thuộc WPF, EF Core, BCrypt hoặc
/// Windows DPAPI cụ thể.
/// </summary>
public sealed class AuthService :
    IAuthService
{
    private static readonly TimeSpan
        RememberedLoginDuration =
            TimeSpan.FromDays(30);

    private readonly IUserRepository
        _userRepository;

    private readonly IPasswordHasher
        _passwordHasher;

    private readonly IUnitOfWork
        _unitOfWork;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IClock
        _clock;

    private readonly IRememberedLoginStore
        _rememberedLoginStore;

    private readonly ISecurityAuditRepository?
        _auditRepository;

    /// <summary>
    /// Constructor tương thích với các test cũ.
    ///
    /// Khi không cung cấp store, chức năng ghi nhớ
    /// được coi như không bật.
    /// </summary>
    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IClock clock)
        : this(
            userRepository,
            passwordHasher,
            unitOfWork,
            currentUserService,
            clock,
            NullRememberedLoginStore.Instance,
            null)
    {
    }

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IClock clock,
        IRememberedLoginStore
            rememberedLoginStore,
        ISecurityAuditRepository? auditRepository = null)
    {
        _userRepository =
            userRepository ??
            throw new ArgumentNullException(
                nameof(userRepository));

        _passwordHasher =
            passwordHasher ??
            throw new ArgumentNullException(
                nameof(passwordHasher));

        _unitOfWork =
            unitOfWork ??
            throw new ArgumentNullException(
                nameof(unitOfWork));

        _currentUserService =
            currentUserService ??
            throw new ArgumentNullException(
                nameof(currentUserService));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));

        _rememberedLoginStore =
            rememberedLoginStore ??
            throw new ArgumentNullException(
                nameof(rememberedLoginStore));
        _auditRepository = auditRepository;
    }

    public async Task<
        Result<AuthenticatedUserDto>>
        LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(
                request.Username))
        {
            return Failure<
                AuthenticatedUserDto>(
                    ErrorCodes.Authentication
                        .UsernameRequired,
                    "Vui lòng nhập tên đăng nhập.");
        }

        if (string.IsNullOrEmpty(
                request.Password))
        {
            return Failure<
                AuthenticatedUserDto>(
                    ErrorCodes.Authentication
                        .PasswordRequired,
                    "Vui lòng nhập mật khẩu.");
        }

        var normalizedUsername =
            request.Username
                .Trim()
                .ToUpperInvariant();

        var user =
            await _userRepository
                .GetByNormalizedUsernameAsync(
                    normalizedUsername,
                    cancellationToken);

        /*
         * Không xác nhận username có tồn tại hay không.
         */
        if (user is null)
        {
            return InvalidCredentials();
        }

        var utcNow =
            _clock.UtcNow;

        if (!user.IsActive)
        {
            return InvalidCredentials();
        }

        if (user.IsLocked(
                utcNow))
        {
            return Failure<
                AuthenticatedUserDto>(
                    ErrorCodes.Authentication
                        .AccountLocked,
                    "Thông tin đăng nhập không hợp lệ hoặc tài khoản tạm thời " +
                    "chưa thể sử dụng. Vui lòng thử lại sau.");
        }

        var passwordIsValid =
            _passwordHasher.VerifyPassword(
                request.Password,
                user.PasswordHash);

        if (!passwordIsValid)
        {
            var beforeAttempts = user.FailedLoginAttempts;
            var beforeLocked = user.IsLocked(utcNow);
            var beforeLockedUntil = user.LockedUntilUtc;
            user.RegisterFailedLogin(
                utcNow,
                BusinessRules.Users.FailedLoginLockDuration);

            var changes = new List<SecurityAuditChange>();
            AddChange(changes, "Sai liên tiếp", beforeAttempts.ToString(CultureInfo.InvariantCulture), user.FailedLoginAttempts.ToString(CultureInfo.InvariantCulture));
            AddChange(changes, "Trạng thái tài khoản", AccountStateText(user, utcNow, beforeLocked), AccountStateText(user, utcNow));
            AddChange(changes, "Khóa đến", FormatTimestamp(beforeLockedUntil), FormatTimestamp(user.LockedUntilUtc));

            var saveResult =
                await SaveAuthenticationStateAsync(
                    user,
                    auditFailure: true,
                    cancellationToken,
                    changes);

            if (saveResult.IsFailure)
            {
                return Result.Failure<
                    AuthenticatedUserDto>(
                        saveResult.AppError);
            }

            if (user.IsLocked(
                    utcNow))
            {
            return Failure<
                AuthenticatedUserDto>(
                    ErrorCodes.Authentication
                        .AccountLocked,
                    $"Thông tin đăng nhập không hợp lệ hoặc tài khoản tạm thời " +
                    $"chưa thể sử dụng. Vui lòng thử lại sau {BusinessRules.Users.FailedLoginLockDuration.TotalMinutes:0} phút.");
            }

            return InvalidCredentials();
        }

        user.RegisterSuccessfulLogin(
            utcNow);

        var successfulLoginSaveResult =
            await SaveAuthenticationStateAsync(
                null,
                auditFailure: false,
                cancellationToken);

        if (successfulLoginSaveResult.IsFailure)
        {
            return Result.Failure<
                AuthenticatedUserDto>(
                    successfulLoginSaveResult.AppError);
        }

        var rememberedLoginResult =
            ConfigureRememberedLogin(
                user.ForcePasswordChange ? false : request.RememberLogin,
                user,
                utcNow);

        if (rememberedLoginResult.IsFailure)
        {
            return Result.Failure<
                AuthenticatedUserDto>(
                    rememberedLoginResult.AppError);
        }

        var authenticatedUser =
            CreateAuthenticatedUser(
                user,
                utcNow);

        /*
         * Chỉ tạo session sau khi database và trạng thái
         * ghi nhớ đã hoàn thành thành công.
         */
        _currentUserService.SetCurrentUser(
            authenticatedUser);

        return Result.Success(
            authenticatedUser);
    }

    public async Task<Result<bool>>
        TryRestoreRememberedLoginAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var credential =
            _rememberedLoginStore
                .Load();

        if (credential is null)
        {
            return Result.Success(
                false);
        }

        var utcNow =
            _clock.UtcNow;

        if (credential.Version !=
                RememberedLoginCredential
                    .CurrentVersion ||
            credential.ExpiresAtUtc
                .ToUniversalTime() <=
            utcNow.ToUniversalTime())
        {
            _rememberedLoginStore
                .TryDelete();

            return Result.Success(
                false);
        }

        var user =
            await _userRepository
                .GetByIdAsync(
                    credential.UserId,
                    cancellationToken);

        if (user is null ||
            !user.IsActive ||
            user.IsLocked(
                utcNow) ||
            user.ForcePasswordChange ||
            !PasswordHashFingerprintMatches(
                user.PasswordHash,
                credential
                    .PasswordHashFingerprint))
        {
            _rememberedLoginStore
                .TryDelete();

            return Result.Success(
                false);
        }

        /*
         * Khôi phục phiên hợp lệ cũng được tính là
         * một lần đăng nhập thành công.
         */
        user.RegisterSuccessfulLogin(
            utcNow);

        var saveResult =
            await SaveAuthenticationStateAsync(
                null,
                auditFailure: false,
                cancellationToken);

        if (saveResult.IsFailure)
        {
            _rememberedLoginStore
                .TryDelete();

            return Result.Failure<bool>(
                saveResult.AppError);
        }

        var authenticatedUser =
            CreateAuthenticatedUser(
                user,
                utcNow);

        _currentUserService.SetCurrentUser(
            authenticatedUser);

        return Result.Success(
            true);
    }

    public Result Logout()
    {
        /*
         * Phải xóa credential trước.
         *
         * Nếu Windows không cho phép xóa file, không đóng
         * Shell để tránh lần mở sau tự đăng nhập ngoài ý muốn.
         */
        if (!_rememberedLoginStore
            .TryDelete())
        {
            return Result.Failure(
                new AppError(
                    ErrorCodes.General.Unexpected,
                    "Không thể xóa phiên đăng nhập đã ghi nhớ. " +
                    "Vui lòng đóng các tiến trình đang sử dụng " +
                    "file bảo mật rồi thử lại."));
        }

        _currentUserService.Clear();

        return Result.Success();
    }

    private Result ConfigureRememberedLogin(
        bool rememberLogin,
        User user,
        DateTimeOffset utcNow)
    {
        if (!rememberLogin)
        {
            if (!_rememberedLoginStore
                .TryDelete())
            {
                return Result.Failure(
                    new AppError(
                        ErrorCodes.General.Unexpected,
                        "Không thể xóa phiên đăng nhập cũ " +
                        "trên máy hiện tại."));
            }

            return Result.Success();
        }

        var credential =
            new RememberedLoginCredential(
                Version:
                    RememberedLoginCredential
                        .CurrentVersion,

                UserId:
                    user.Id,

                PasswordHashFingerprint:
                    CreatePasswordHashFingerprint(
                        user.PasswordHash),

                ExpiresAtUtc:
                    utcNow
                        .ToUniversalTime()
                        .Add(
                            RememberedLoginDuration));

        if (!_rememberedLoginStore
            .TrySave(
                credential))
        {
            return Result.Failure(
                new AppError(
                    ErrorCodes.General.Unexpected,
                    "Đăng nhập thành công nhưng không thể " +
                    "lưu phiên 30 ngày trên máy này. " +
                    "Hãy bỏ chọn duy trì đăng nhập rồi thử lại."));
        }

        return Result.Success();
    }

    private async Task<Result> SaveAuthenticationStateAsync(
        User? user,
        bool auditFailure,
        CancellationToken cancellationToken,
        IEnumerable<SecurityAuditChange>? changes = null)
    {
        try
        {
            if (user is not null && auditFailure && _auditRepository is not null)
            {
                await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _auditRepository.AddAsync(new SecurityAuditEvent(
                    actorUserId: null,
                    targetEmployeeId: null,
                    targetUserId: user.Id,
                    action: SecurityAuditAction.LoginFailed,
                    result: "Failed",
                    operationId: Guid.NewGuid(),
                    utcNow: _clock.UtcNow,
                    actorDisplayNameSnapshot: null,
                    targetDisplayNameSnapshot: user.FullName,
                    businessArea: "Nhân viên và tài khoản",
                    targetType: "Tài khoản",
                    terminalId: "Không xác định",
                    changes: changes), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }
        catch (
            PersistenceConflictException)
        {
            return Result.Failure(
                new AppError(
                    ErrorCodes.General.Conflict,
                    "Trạng thái tài khoản vừa được thay đổi " +
                    "bởi một thao tác khác. Vui lòng thử lại."));
        }
    }

    private static AuthenticatedUserDto
        CreateAuthenticatedUser(
            User user,
            DateTimeOffset utcNow)
    {
        return new AuthenticatedUserDto(
            id:
                user.Id,

            username:
                user.Username,

            fullName:
                user.FullName,

            role:
                user.Role,

            authenticatedAtUtc:
                utcNow,

            forcePasswordChange:
                user.ForcePasswordChange);
    }

    private static void AddChange(List<SecurityAuditChange> changes, string field, string? before, string? after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
            changes.Add(new SecurityAuditChange(field, before, after));
    }

    private static string AccountStateText(User user, DateTimeOffset now, bool? wasLocked = null)
    {
        if (!user.IsActive) return "Ngừng hoạt động";
        if (wasLocked ?? user.IsLocked(now)) return "Đang khóa";
        if (user.ForcePasswordChange) return "Chờ nhân viên đổi mật khẩu lần đầu";
        return "Đang hoạt động";
    }

    private static string FormatTimestamp(DateTimeOffset? value) =>
        value?.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("vi-VN")) ?? "—";

    private static string
        CreatePasswordHashFingerprint(
            string passwordHash)
    {
        var inputBytes =
            Encoding.UTF8.GetBytes(
                passwordHash);

        try
        {
            var digest =
                SHA256.HashData(
                    inputBytes);

            return Convert.ToHexString(
                digest);
        }
        finally
        {
            CryptographicOperations
                .ZeroMemory(
                    inputBytes);
        }
    }

    private static bool
        PasswordHashFingerprintMatches(
            string passwordHash,
            string expectedFingerprint)
    {
        byte[] expectedBytes;

        try
        {
            expectedBytes =
                Convert.FromHexString(
                    expectedFingerprint);
        }
        catch (FormatException)
        {
            return false;
        }

        var inputBytes =
            Encoding.UTF8.GetBytes(
                passwordHash);

        try
        {
            var actualBytes =
                SHA256.HashData(
                    inputBytes);

            return
                actualBytes.Length ==
                expectedBytes.Length &&

                CryptographicOperations
                    .FixedTimeEquals(
                        actualBytes,
                        expectedBytes);
        }
        finally
        {
            CryptographicOperations
                .ZeroMemory(
                    inputBytes);

            CryptographicOperations
                .ZeroMemory(
                    expectedBytes);
        }
    }

    private static Result<
        AuthenticatedUserDto>
        InvalidCredentials()
    {
        return Failure<
            AuthenticatedUserDto>(
                ErrorCodes.Authentication
                    .InvalidCredentials,
                "Tên đăng nhập hoặc mật khẩu không chính xác.");
    }

    private static Result<TValue>
        Failure<TValue>(
            string errorCode,
            string errorMessage)
    {
        return Result.Failure<TValue>(
            new AppError(
                errorCode,
                errorMessage));
    }

    /// <summary>
    /// Store rỗng để giữ tương thích constructor cũ
    /// trong các unit test đã tồn tại.
    /// </summary>
    private sealed class
        NullRememberedLoginStore :
            IRememberedLoginStore
    {
        public static
            NullRememberedLoginStore
            Instance
        { get; } = new();

        public RememberedLoginCredential?
            Load()
        {
            return null;
        }

        public bool TrySave(
            RememberedLoginCredential credential)
        {
            ArgumentNullException.ThrowIfNull(
                credential);

            return true;
        }

        public bool TryDelete()
        {
            return true;
        }
    }
}
