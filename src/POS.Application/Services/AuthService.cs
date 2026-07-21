using System.Security.Cryptography;
using System.Text;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Domain.Entities;

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
        AccountLockDuration =
            TimeSpan.FromMinutes(15);

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
            NullRememberedLoginStore.Instance)
    {
    }

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IClock clock,
        IRememberedLoginStore
            rememberedLoginStore)
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
            return Failure<
                AuthenticatedUserDto>(
                    ErrorCodes.Authentication
                        .AccountInactive,
                    "Tài khoản đã ngừng hoạt động. " +
                    "Vui lòng liên hệ quản trị viên.");
        }

        if (user.IsLocked(
                utcNow))
        {
            return Failure<
                AuthenticatedUserDto>(
                    ErrorCodes.Authentication
                        .AccountLocked,
                    "Tài khoản đang bị khóa tạm thời do " +
                    "đăng nhập sai nhiều lần.");
        }

        var passwordIsValid =
            _passwordHasher.VerifyPassword(
                request.Password,
                user.PasswordHash);

        if (!passwordIsValid)
        {
            user.RegisterFailedLogin(
                utcNow,
                AccountLockDuration);

            var saveResult =
                await SaveAuthenticationStateAsync(
                    cancellationToken);

            if (saveResult.IsFailure)
            {
                return Result.Failure<
                    AuthenticatedUserDto>(
                        saveResult.Error);
            }

            if (user.IsLocked(
                    utcNow))
            {
                return Failure<
                    AuthenticatedUserDto>(
                        ErrorCodes.Authentication
                            .AccountLocked,
                        "Tài khoản đã bị khóa 15 phút do " +
                        "đăng nhập sai quá nhiều lần.");
            }

            return InvalidCredentials();
        }

        user.RegisterSuccessfulLogin(
            utcNow);

        var successfulLoginSaveResult =
            await SaveAuthenticationStateAsync(
                cancellationToken);

        if (successfulLoginSaveResult.IsFailure)
        {
            return Result.Failure<
                AuthenticatedUserDto>(
                    successfulLoginSaveResult.Error);
        }

        var rememberedLoginResult =
            ConfigureRememberedLogin(
                request.RememberLogin,
                user,
                utcNow);

        if (rememberedLoginResult.IsFailure)
        {
            return Result.Failure<
                AuthenticatedUserDto>(
                    rememberedLoginResult.Error);
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
                cancellationToken);

        if (saveResult.IsFailure)
        {
            _rememberedLoginStore
                .TryDelete();

            return Result.Failure<bool>(
                saveResult.Error);
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
                new Error(
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
                    new Error(
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
                new Error(
                    ErrorCodes.General.Unexpected,
                    "Đăng nhập thành công nhưng không thể " +
                    "lưu phiên 30 ngày trên máy này. " +
                    "Hãy bỏ chọn duy trì đăng nhập rồi thử lại."));
        }

        return Result.Success();
    }

    private async Task<Result>
        SaveAuthenticationStateAsync(
            CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork
                .SaveChangesAsync(
                    cancellationToken);

            return Result.Success();
        }
        catch (
            PersistenceConflictException)
        {
            return Result.Failure(
                new Error(
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
                utcNow);
    }

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
            new Error(
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