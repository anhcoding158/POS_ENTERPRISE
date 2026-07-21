using System.Text;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

/// <summary>
/// Tạo Administrator trong lần chạy đầu tiên.
///
/// Không sử dụng tài khoản hoặc mật khẩu mặc định.
/// Người dùng bắt buộc tự thiết lập thông tin riêng.
/// </summary>
public sealed class InitialSetupService :
    IInitialSetupService
{
    private const int
        MinimumPasswordLength = 10;

    private const int
        MaximumPasswordUtf8Bytes = 72;

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

    public InitialSetupService(
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

    public async Task<Result<bool>>
        IsSetupRequiredAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var hasUsers =
            await _userRepository.AnyAsync(
                cancellationToken);

        return Result.Success(
            !hasUsers);
    }

    public async Task<
        Result<AuthenticatedUserDto>>
        CreateInitialAdministratorAsync(
            InitialAdministratorRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        var validationResult =
            ValidateRequest(
                request);

        if (validationResult.IsFailure)
        {
            return Result.Failure<
                AuthenticatedUserDto>(
                    validationResult.Error);
        }

        /*
         * Chỉ cho phép bootstrap khi chưa tồn tại
         * bất kỳ tài khoản nào.
         */
        if (await _userRepository.AnyAsync(
                cancellationToken))
        {
            return Failure(
                ErrorCodes.General.Conflict,
                "Thiết lập ban đầu đã hoàn tất. " +
                "Không thể tạo thêm quản trị viên bằng chức năng này.");
        }

        var normalizedUsername =
            request.Username
                .Trim()
                .ToUpperInvariant();

        if (await _userRepository
            .NormalizedUsernameExistsAsync(
                normalizedUsername,
                cancellationToken:
                    cancellationToken))
        {
            return Failure(
                ErrorCodes.General.Conflict,
                "Tên đăng nhập đã tồn tại.");
        }

        string passwordHash;

        try
        {
            passwordHash =
                _passwordHasher.HashPassword(
                    request.Password);
        }
        catch (ArgumentException exception)
        {
            return Failure(
                ErrorCodes.General.Validation,
                exception.Message);
        }

        var utcNow =
            _clock.UtcNow;

        User administrator;

        try
        {
            administrator =
                new User(
                    username:
                        request.Username,

                    passwordHash:
                        passwordHash,

                    fullName:
                        request.FullName,

                    role:
                        Role.Administrator,

                    utcNow:
                        utcNow);
        }
        catch (DomainException exception)
        {
            return Failure(
                ErrorCodes.General.Validation,
                exception.Message);
        }

        try
        {
            await _userRepository.AddAsync(
                administrator,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
        catch (
            PersistenceConflictException exception)
        {
            var message =
                string.Equals(
                    exception.Target,
                    PersistenceConflictTargets
                        .UserNormalizedUsername,
                    StringComparison.Ordinal)
                    ? "Tên đăng nhập đã tồn tại."
                    : "Thiết lập ban đầu bị xung đột " +
                      "với một thao tác khác.";

            return Failure(
                ErrorCodes.General.Conflict,
                message);
        }

        var authenticatedUser =
            new AuthenticatedUserDto(
                id:
                    administrator.Id,

                username:
                    administrator.Username,

                fullName:
                    administrator.FullName,

                role:
                    administrator.Role,

                authenticatedAtUtc:
                    utcNow);

        /*
         * Administrator đầu tiên được đăng nhập
         * ngay sau khi lưu thành công.
         */
        _currentUserService.SetCurrentUser(
            authenticatedUser);

        return Result.Success(
            authenticatedUser);
    }

    private static Result ValidateRequest(
        InitialAdministratorRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                request.Username))
        {
            return ValidationFailure(
                "Vui lòng nhập tên đăng nhập.");
        }

        if (string.IsNullOrWhiteSpace(
                request.FullName))
        {
            return ValidationFailure(
                "Vui lòng nhập họ tên quản trị viên.");
        }

        if (string.IsNullOrEmpty(
                request.Password))
        {
            return ValidationFailure(
                "Vui lòng nhập mật khẩu.");
        }

        if (!string.Equals(
                request.Password,
                request.ConfirmPassword,
                StringComparison.Ordinal))
        {
            return ValidationFailure(
                "Mật khẩu xác nhận không khớp.");
        }

        if (request.Password.Length <
            MinimumPasswordLength)
        {
            return ValidationFailure(
                $"Mật khẩu phải có ít nhất " +
                $"{MinimumPasswordLength} ký tự.");
        }

        if (Encoding.UTF8.GetByteCount(
                request.Password) >
            MaximumPasswordUtf8Bytes)
        {
            return ValidationFailure(
                $"Mật khẩu không được vượt quá " +
                $"{MaximumPasswordUtf8Bytes} byte UTF-8.");
        }

        if (request.Password.Any(
                char.IsWhiteSpace))
        {
            return ValidationFailure(
                "Mật khẩu không được chứa khoảng trắng.");
        }

        if (!request.Password.Any(
                char.IsUpper))
        {
            return ValidationFailure(
                "Mật khẩu phải có ít nhất một chữ hoa.");
        }

        if (!request.Password.Any(
                char.IsLower))
        {
            return ValidationFailure(
                "Mật khẩu phải có ít nhất một chữ thường.");
        }

        if (!request.Password.Any(
                char.IsDigit))
        {
            return ValidationFailure(
                "Mật khẩu phải có ít nhất một chữ số.");
        }

        if (!request.Password.Any(
                character =>
                    !char.IsLetterOrDigit(
                        character)))
        {
            return ValidationFailure(
                "Mật khẩu phải có ít nhất một ký tự đặc biệt.");
        }

        if (request.Password.Contains(
                request.Username,
                StringComparison.OrdinalIgnoreCase))
        {
            return ValidationFailure(
                "Mật khẩu không được chứa tên đăng nhập.");
        }

        return Result.Success();
    }

    private static Result
        ValidationFailure(
            string message)
    {
        return Result.Failure(
            new Error(
                ErrorCodes.General.Validation,
                message));
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