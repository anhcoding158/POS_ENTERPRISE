using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authentication;
using POS.Application.DTOs.Authentication;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// ViewModel đăng nhập.
///
/// Password được giữ nội bộ và không expose thành
/// property bindable hoặc đưa vào log.
/// </summary>
public sealed class LoginViewModel :
    ViewModelBase
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        LoginViewModel>
        _logger;

    private string _username =
        string.Empty;

    private string _password =
        string.Empty;

    private string _usernameError =
        string.Empty;

    private string _passwordError =
        string.Empty;

    private string _statusMessage =
        string.Empty;

    private bool _isStatusError;
    private bool _isBusy;

    public LoginViewModel(
        IServiceScopeFactory scopeFactory,
        ILogger<LoginViewModel> logger)
    {
        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        LoginCommand =
            new AsyncRelayCommand(
                LoginAsync,
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

    public event Action?
        RequestPasswordClear;

    public AsyncRelayCommand LoginCommand { get; }

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
        }
    }

    public string UsernameError
    {
        get => _usernameError;

        private set => SetProperty(
            ref _usernameError,
            value);
    }

    public string PasswordError
    {
        get => _passwordError;

        private set => SetProperty(
            ref _passwordError,
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

            LoginCommand
                .NotifyCanExecuteChanged();

            CancelCommand
                .NotifyCanExecuteChanged();
        }
    }

    public void UpdatePassword(
        string? password)
    {
        _password =
            password ??
            string.Empty;

        ValidatePassword();
    }

    private async Task LoginAsync()
    {
        ValidateUsername();
        ValidatePassword();

        if (!string.IsNullOrWhiteSpace(
                UsernameError) ||
            !string.IsNullOrWhiteSpace(
                PasswordError))
        {
            ShowError(
                "Vui lòng nhập đầy đủ thông tin đăng nhập.");

            return;
        }

        IsBusy = true;
        IsStatusError = false;

        StatusMessage =
            "Đang xác thực tài khoản...";

        try
        {
            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var authService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IAuthService>();

            var result =
                await authService.LoginAsync(
                    new LoginRequest(
                        username:
                            Username,

                        password:
                            _password));

            if (result.IsFailure)
            {
                ShowError(
                    result.Error.Message);

                RequestPasswordClear?
                    .Invoke();

                return;
            }

            IsStatusError = false;

            StatusMessage =
                $"Xin chào {result.Value.FullName}.";

            RequestClose?.Invoke(
                true);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Đăng nhập không thể hoàn thành.");

            ShowError(
                "Không thể đăng nhập. " +
                exception
                    .GetBaseException()
                    .Message);

            RequestPasswordClear?
                .Invoke();
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

    private void ValidateUsername()
    {
        UsernameError =
            string.IsNullOrWhiteSpace(
                Username)
                ? "Vui lòng nhập tên đăng nhập."
                : string.Empty;
    }

    private void ValidatePassword()
    {
        PasswordError =
            string.IsNullOrEmpty(
                _password)
                ? "Vui lòng nhập mật khẩu."
                : string.Empty;
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
            "Lệnh đăng nhập thất bại.");

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
}