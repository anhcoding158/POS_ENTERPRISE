using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.Authentication;
using POS.Application.DTOs.Employees;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

public sealed class ForcedPasswordChangeViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ForcedPasswordChangeViewModel> _logger;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private PasswordPolicyValidation _policy = PasswordPolicy.Validate(string.Empty);

    public ForcedPasswordChangeViewModel(IServiceScopeFactory scopeFactory, ILogger<ForcedPasswordChangeViewModel> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ChangePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync, () => !IsBusy, HandleException);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => !IsBusy, HandleException);
    }

    public event Action<bool?>? RequestClose;
    public AsyncRelayCommand ChangePasswordCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public string StatusMessage { get => _statusMessage; private set { if (SetProperty(ref _statusMessage, value)) OnPropertyChanged(nameof(HasStatusMessage)); } }
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) { ChangePasswordCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); } } }
    public bool HasMinimumLength => _policy.HasMinimumLength;
    public bool HasUppercase => _policy.HasUppercase;
    public bool HasLowercase => _policy.HasLowercase;
    public bool HasDigit => _policy.HasDigit;
    public bool HasSpecialCharacter => _policy.HasSpecialCharacter;
    public bool HasNoWhitespace => _policy.HasNoWhitespace;
    public bool PasswordConfirmationMatches => string.Equals(_newPassword, _confirmPassword, StringComparison.Ordinal);

    public void UpdateNewPassword(string? value)
    {
        _newPassword = value ?? string.Empty;
        _policy = PasswordPolicy.Validate(_newPassword);
        NotifyPasswordState();
    }

    public void UpdateConfirmPassword(string? value)
    {
        _confirmPassword = value ?? string.Empty;
        OnPropertyChanged(nameof(PasswordConfirmationMatches));
    }

    private async Task ChangePasswordAsync()
    {
        _policy = PasswordPolicy.Validate(_newPassword);
        NotifyPasswordState();
        if (!_policy.IsValid || !PasswordConfirmationMatches)
        {
            StatusMessage = !PasswordConfirmationMatches ? "Mật khẩu xác nhận không khớp." : _policy.ErrorMessage;
            return;
        }

        IsBusy = true;
        StatusMessage = "Đang cập nhật mật khẩu bảo mật...";
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>();
            var result = await service.CompletePasswordChangeAsync(new CompletePasswordChangeRequest
            {
                NewPassword = _newPassword,
                ConfirmPassword = _confirmPassword
            });
            if (result.IsFailure)
            {
                StatusMessage = result.AppError.Message;
                ClearPasswords();
                return;
            }

            ClearPasswords();
            RequestClose?.Invoke(true);
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger, exception, "Đổi mật khẩu bắt buộc thất bại.");
            StatusMessage = "Không thể đổi mật khẩu. Vui lòng thử lại.";
            ClearPasswords();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task CancelAsync()
    {
        ClearPasswords();
        RequestClose?.Invoke(false);
        return Task.CompletedTask;
    }

    private void ClearPasswords()
    {
        _newPassword = string.Empty;
        _confirmPassword = string.Empty;
        _policy = PasswordPolicy.Validate(string.Empty);
        NotifyPasswordState();
    }

    private void NotifyPasswordState()
    {
        OnPropertyChanged(nameof(HasMinimumLength));
        OnPropertyChanged(nameof(HasUppercase));
        OnPropertyChanged(nameof(HasLowercase));
        OnPropertyChanged(nameof(HasDigit));
        OnPropertyChanged(nameof(HasSpecialCharacter));
        OnPropertyChanged(nameof(HasNoWhitespace));
        OnPropertyChanged(nameof(PasswordConfirmationMatches));
    }

    private void HandleException(Exception exception)
    {
        global::POS.Application.Common.PosLog.Error(_logger, exception, "Lệnh đổi mật khẩu bắt buộc thất bại.");
        StatusMessage = "Thao tác không thể hoàn thành. Vui lòng thử lại.";
    }
}
