using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;

namespace POS.Application.Services;

/// <summary>
/// Xử lý đăng nhập, khóa tài khoản và phiên người dùng.
///
/// Service không phụ thuộc WPF, EF Core hoặc BCrypt cụ thể.
/// </summary>
public sealed class AuthService :
    IAuthService
{
    private static readonly TimeSpan
        AccountLockDuration =
            TimeSpan.FromMinutes(15);

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

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IClock clock)
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
            return Failure(
                ErrorCodes.Authentication
                    .UsernameRequired,
                "Vui lòng nhập tên đăng nhập.");
        }

        if (string.IsNullOrEmpty(
                request.Password))
        {
            return Failure(
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
         * Không trả UserNotFound ra giao diện để tránh
         * xác nhận một username có tồn tại hay không.
         */
        if (user is null)
        {
            return InvalidCredentials();
        }

        var utcNow =
            _clock.UtcNow;

        if (!user.IsActive)
        {
            return Failure(
                ErrorCodes.Authentication
                    .AccountInactive,
                "Tài khoản đã ngừng hoạt động. " +
                "Vui lòng liên hệ quản trị viên.");
        }

        if (user.IsLocked(
                utcNow))
        {
            return Failure(
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

            /*
             * Lần nhập sai đạt giới hạn sẽ trả AccountLocked
             * ngay lập tức thay vì InvalidCredentials.
             */
            if (user.IsLocked(
                    utcNow))
            {
                return Failure(
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

        var authenticatedUser =
            new AuthenticatedUserDto(
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

        /*
         * Chỉ thiết lập session sau khi trạng thái đăng nhập
         * đã được lưu thành công vào database.
         */
        _currentUserService.SetCurrentUser(
            authenticatedUser);

        return Result.Success(
            authenticatedUser);
    }

    public Result Logout()
    {
        _currentUserService.Clear();

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

    private static Result<
        AuthenticatedUserDto>
        InvalidCredentials()
    {
        return Failure(
            ErrorCodes.Authentication
                .InvalidCredentials,
            "Tên đăng nhập hoặc mật khẩu không chính xác.");
    }

    private static Result<
        AuthenticatedUserDto>
        Failure(
            string errorCode,
            string errorMessage)
    {
        return Result.Failure<
            AuthenticatedUserDto>(
                new Error(
                    errorCode,
                    errorMessage));
    }
}