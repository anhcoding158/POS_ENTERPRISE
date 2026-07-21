using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authentication;
using POS.Application.DTOs.Authentication;
using POS.Domain.Constants;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// ViewModel thiết lập Administrator trong lần chạy đầu tiên.
///
/// Password không được expose thành property bindable.
/// PasswordBox truyền giá trị thông qua UpdatePassword.
/// </summary>
public sealed class FirstRunSetupViewModel :
    ViewModelBase
{
    private const int MinimumPasswordLength = 10;
    private const int MaximumPasswordUtf8Bytes = 72;

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        FirstRunSetupViewModel>
        _logger;

    private string _username =
        string.Empty;

    private string _fullName =
        string.Empty;

    private string _password =
        string.Empty;

    private string _confirmPassword =
        string.Empty;

    private string _usernameError =
        string.Empty;

    private string _fullNameError =
        string.Empty;

    private string _passwordError =
        string.Empty;

    private string _confirmPasswordError =
        string.Empty;

    private string _statusMessage =
        string.Empty;

    private bool _isStatusError;
    private bool _isBusy;

    public FirstRunSetupViewModel(
        IServiceScopeFactory scopeFactory,
        ILogger<FirstRunSetupViewModel> logger)
    {
        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        CreateAdministratorCommand =
            new AsyncRelayCommand(
                CreateAdministratorAsync,
                CanExecuteCommand,
                HandleCommandException);

        CancelCommand =
            new AsyncRelayCommand(
                CancelAsync,
                CanExecuteCommand,
                HandleCommandException);
    }

    public event Action<bool?>?
        RequestClose;

    public AsyncRelayCommand
        CreateAdministratorCommand
    { get; }

    public AsyncRelayCommand CancelCommand { get; }

    public string Username
    {
        get => _username;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _username,
                    normalized))
            {
                return;
            }

            ValidateUsername();
            ValidatePassword();

            NotifyPasswordRequirements();
        }
    }

    public string FullName
    {
        get => _fullName;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _fullName,
                    normalized))
            {
                return;
            }

            ValidateFullName();
        }
    }

    public string UsernameError
    {
        get => _usernameError;

        private set => SetProperty(
            ref _usernameError,
            value);
    }

    public string FullNameError
    {
        get => _fullNameError;

        private set => SetProperty(
            ref _fullNameError,
            value);
    }

    public string PasswordError
    {
        get => _passwordError;

        private set => SetProperty(
            ref _passwordError,
            value);
    }

    public string ConfirmPasswordError
    {
        get => _confirmPasswordError;

        private set => SetProperty(
            ref _confirmPasswordError,
            value);
    }

    public string StatusMessage
    {
        get => _statusMessage;

        private set
        {
            if (!SetProperty(
                    ref _statusMessage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage =>
        !string.IsNullOrWhiteSpace(
            StatusMessage);

    public bool IsStatusError
    {
        get => _isStatusError;

        private set => SetProperty(
            ref _isStatusError,
            value);
    }

    public bool IsBusy
    {
        get => _isBusy;

        private set
        {
            if (!SetProperty(
                    ref _isBusy,
                    value))
            {
                return;
            }

            CreateAdministratorCommand
                .NotifyCanExecuteChanged();

            CancelCommand
                .NotifyCanExecuteChanged();
        }
    }

    public bool PasswordHasMinimumLength =>
        _password.Length >=
        MinimumPasswordLength;

    public bool PasswordHasUppercase =>
        _password.Any(
            char.IsUpper);

    public bool PasswordHasLowercase =>
        _password.Any(
            char.IsLower);

    public bool PasswordHasDigit =>
        _password.Any(
            char.IsDigit);

    public bool PasswordHasSpecialCharacter =>
        _password.Any(
            character =>
                !char.IsLetterOrDigit(
                    character));

    public bool PasswordHasNoWhitespace =>
        !_password.Any(
            char.IsWhiteSpace);

    public bool PasswordIsWithinBcryptLimit =>
        Encoding.UTF8.GetByteCount(
            _password) <=
        MaximumPasswordUtf8Bytes;

    public bool PasswordDoesNotContainUsername
    {
        get
        {
            var normalizedUsername =
                Username.Trim();

            return
                normalizedUsername.Length == 0 ||
                !_password.Contains(
                    normalizedUsername,
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool PasswordConfirmationMatches =>
        _password.Length > 0 &&
        string.Equals(
            _password,
            _confirmPassword,
            StringComparison.Ordinal);

    public bool IsPasswordStrong =>
        PasswordHasMinimumLength &&
        PasswordHasUppercase &&
        PasswordHasLowercase &&
        PasswordHasDigit &&
        PasswordHasSpecialCharacter &&
        PasswordHasNoWhitespace &&
        PasswordIsWithinBcryptLimit &&
        PasswordDoesNotContainUsername;

    public string PasswordStrengthText =>
        IsPasswordStrong
            ? "Mật khẩu đạt yêu cầu bảo mật."
            : "Mật khẩu chưa đáp ứng đầy đủ yêu cầu.";

    public void UpdatePassword(
        string? password)
    {
        _password =
            password ??
            string.Empty;

        ValidatePassword();
        ValidateConfirmPassword();

        NotifyPasswordRequirements();
    }

    public void UpdateConfirmPassword(
        string? confirmPassword)
    {
        _confirmPassword =
            confirmPassword ??
            string.Empty;

        ValidateConfirmPassword();

        OnPropertyChanged(
            nameof(
                PasswordConfirmationMatches));
    }

    private async Task
        CreateAdministratorAsync()
    {
        ValidateAll();

        if (HasValidationErrors())
        {
            ShowError(
                "Vui lòng kiểm tra lại các trường được đánh dấu.");

            return;
        }

        IsBusy = true;
        IsStatusError = false;

        StatusMessage =
            "Đang bảo vệ mật khẩu và tạo tài khoản quản trị...";

        try
        {
            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var setupService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IInitialSetupService>();

            var request =
                new InitialAdministratorRequest(
                    username:
                        Username,

                    fullName:
                        FullName,

                    password:
                        _password,

                    confirmPassword:
                        _confirmPassword);

            var result =
                await setupService
                    .CreateInitialAdministratorAsync(
                        request);

            if (result.IsFailure)
            {
                ShowError(
                    result.Error.Message);

                return;
            }

            IsStatusError = false;

            StatusMessage =
                "Administrator đã được tạo thành công.";

            RequestClose?.Invoke(
                true);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể tạo Administrator đầu tiên.");

            ShowError(
                "Không thể hoàn tất thiết lập ban đầu. " +
                exception
                    .GetBaseException()
                    .Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task CancelAsync()
    {
        RequestClose?.Invoke(
            false);

        return Task.CompletedTask;
    }

    private void ValidateAll()
    {
        ValidateUsername();
        ValidateFullName();
        ValidatePassword();
        ValidateConfirmPassword();
    }

    private void ValidateUsername()
    {
        var normalized =
            Username.Trim();

        if (normalized.Length == 0)
        {
            UsernameError =
                "Vui lòng nhập tên đăng nhập.";

            return;
        }

        if (normalized.Length <
                BusinessRules.Users
                    .UsernameMinLength ||
            normalized.Length >
                BusinessRules.Users
                    .UsernameMaxLength)
        {
            UsernameError =
                $"Tên đăng nhập phải có từ " +
                $"{BusinessRules.Users.UsernameMinLength} đến " +
                $"{BusinessRules.Users.UsernameMaxLength} ký tự.";

            return;
        }

        if (!normalized.All(
                IsAllowedUsernameCharacter))
        {
            UsernameError =
                "Chỉ dùng chữ, số, dấu chấm, " +
                "gạch dưới hoặc gạch ngang.";

            return;
        }

        UsernameError =
            string.Empty;
    }

    private void ValidateFullName()
    {
        var normalized =
            FullName.Trim();

        if (normalized.Length == 0)
        {
            FullNameError =
                "Vui lòng nhập họ tên quản trị viên.";

            return;
        }

        if (normalized.Length >
            BusinessRules.Users
                .FullNameMaxLength)
        {
            FullNameError =
                $"Họ tên tối đa " +
                $"{BusinessRules.Users.FullNameMaxLength} ký tự.";

            return;
        }

        FullNameError =
            string.Empty;
    }

    private void ValidatePassword()
    {
        if (_password.Length == 0)
        {
            PasswordError =
                "Vui lòng nhập mật khẩu.";

            return;
        }

        if (!IsPasswordStrong)
        {
            PasswordError =
                "Mật khẩu chưa đáp ứng đầy đủ yêu cầu.";

            return;
        }

        PasswordError =
            string.Empty;
    }

    private void ValidateConfirmPassword()
    {
        if (_confirmPassword.Length == 0)
        {
            ConfirmPasswordError =
                "Vui lòng nhập lại mật khẩu.";

            return;
        }

        if (!PasswordConfirmationMatches)
        {
            ConfirmPasswordError =
                "Mật khẩu xác nhận không khớp.";

            return;
        }

        ConfirmPasswordError =
            string.Empty;
    }

    private bool HasValidationErrors()
    {
        return
            !string.IsNullOrWhiteSpace(
                UsernameError) ||

            !string.IsNullOrWhiteSpace(
                FullNameError) ||

            !string.IsNullOrWhiteSpace(
                PasswordError) ||

            !string.IsNullOrWhiteSpace(
                ConfirmPasswordError);
    }

    private bool CanExecuteCommand()
    {
        return !IsBusy;
    }

    private void HandleCommandException(
        Exception exception)
    {
        _logger.LogError(
            exception,
            "Lệnh thiết lập Administrator thất bại.");

        ShowError(
            "Thao tác không thể hoàn thành. " +
            exception
                .GetBaseException()
                .Message);
    }

    private void ShowError(
        string message)
    {
        IsStatusError = true;
        StatusMessage = message;
    }

    private void NotifyPasswordRequirements()
    {
        OnPropertyChanged(
            nameof(
                PasswordHasMinimumLength));

        OnPropertyChanged(
            nameof(
                PasswordHasUppercase));

        OnPropertyChanged(
            nameof(
                PasswordHasLowercase));

        OnPropertyChanged(
            nameof(
                PasswordHasDigit));

        OnPropertyChanged(
            nameof(
                PasswordHasSpecialCharacter));

        OnPropertyChanged(
            nameof(
                PasswordHasNoWhitespace));

        OnPropertyChanged(
            nameof(
                PasswordIsWithinBcryptLimit));

        OnPropertyChanged(
            nameof(
                PasswordDoesNotContainUsername));

        OnPropertyChanged(
            nameof(
                PasswordConfirmationMatches));

        OnPropertyChanged(
            nameof(
                IsPasswordStrong));

        OnPropertyChanged(
            nameof(
                PasswordStrengthText));
    }

    private static bool
        IsAllowedUsernameCharacter(
            char character)
    {
        return
            char.IsLetterOrDigit(
                character) ||

            character is
                '.' or
                '_' or
                '-';
    }
}