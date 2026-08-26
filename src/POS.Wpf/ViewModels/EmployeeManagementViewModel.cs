using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Employees;
using POS.Application.Authentication;
using POS.Domain.Enums;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

public sealed record EmployeeFilterOption(string DisplayName, EmployeeStatus? Value);
public sealed record AccountFilterOption(string DisplayName, AccountStatus? Value);
public sealed record RoleOption(string DisplayName, Role Value);

public sealed class EmployeeRowViewModel
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    public EmployeeRowViewModel(EmployeeListItemDto dto)
    {
        Id = dto.Id; EmployeeCode = dto.EmployeeCode; FullName = dto.FullName; PhoneNumber = dto.PhoneNumber;
        EmployeeStatus = dto.EmployeeStatus; Username = dto.Username; AccountStatus = dto.AccountStatus; Role = dto.Role;
        LastSuccessfulLoginUtc = dto.LastSuccessfulLoginUtc; FailedLoginAttempts = dto.FailedLoginAttempts; UpdatedAtUtc = dto.UpdatedAtUtc;
    }
    public int Id { get; }
    public string EmployeeCode { get; }
    public string FullName { get; }
    public string? PhoneNumber { get; }
    public EmployeeStatus EmployeeStatus { get; }
    public string EmployeeStatusText => EmployeeStatus == EmployeeStatus.Active ? "Đang làm việc" : "Ngừng hoạt động";
    public string? Username { get; }
    public AccountStatus AccountStatus { get; }
    public string AccountStatusText => AccountStatus switch
    {
        AccountStatus.NoAccount => "Chưa có tài khoản",
        AccountStatus.Active => "Đang hoạt động",
        AccountStatus.Locked => "Đang bị khóa",
        AccountStatus.Disabled => "Đã vô hiệu hóa",
        AccountStatus.ForcePasswordChange => "Chờ đổi mật khẩu",
        _ => "Không xác định"
    };
    public Role? Role { get; }
    public string RoleText => Role is null ? "—" : RolePermissionPolicy.GetRoleDisplayName(Role.Value);
    public DateTimeOffset? LastSuccessfulLoginUtc { get; }
    public string LastLoginText => LastSuccessfulLoginUtc is null ? "Chưa đăng nhập" : LastSuccessfulLoginUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", VietnameseCulture);
    public int FailedLoginAttempts { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
}

public sealed class EmployeeManagementViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<EmployeeManagementViewModel> _logger;
    private string _searchTerm = string.Empty;
    private EmployeeFilterOption _selectedEmployeeFilter;
    private AccountFilterOption _selectedAccountFilter;
    private RoleOption? _selectedRoleFilter;
    private EmployeeRowViewModel? _selectedEmployee;
    private EmployeeDetailsDto? _details;
    private bool _isBusy;
    private bool _isCreateMode;
    private string _employeeCode = string.Empty;
    private string _fullName = string.Empty;
    private string _phoneNumber = string.Empty;
    private string _emailAddress = string.Empty;
    private string _username = string.Empty;
    private string _accountPassword = string.Empty;
    private string _resetPassword = string.Empty;
    private bool _createAccount;
    private RoleOption _selectedRole;
    private string _statusMessage = string.Empty;
    private bool _isStatusError;
    private bool _isDirty;
    private bool _suppressDirty;
    private int _pageNumber = 1;
    private int _totalPages = 1;
    private int _totalCount;

    public EmployeeManagementViewModel(IServiceScopeFactory scopeFactory, IPermissionService permissionService, ILogger<EmployeeManagementViewModel> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        EmployeeFilters = new ObservableCollection<EmployeeFilterOption>([
            new("Tất cả nhân viên", null), new("Đang làm việc", EmployeeStatus.Active), new("Ngừng hoạt động", EmployeeStatus.Inactive)]);
        AccountFilters = new ObservableCollection<AccountFilterOption>([
            new("Tất cả tài khoản", null), new("Chưa có tài khoản", AccountStatus.NoAccount), new("Đang hoạt động", AccountStatus.Active),
            new("Đang bị khóa", AccountStatus.Locked), new("Đã vô hiệu hóa", AccountStatus.Disabled), new("Chờ đổi mật khẩu", AccountStatus.ForcePasswordChange)]);
        RoleOptions = new ObservableCollection<RoleOption>([
            new("Quản trị viên", Role.Administrator), new("Quản lý", Role.Manager), new("Thu ngân", Role.Cashier), new("Nhân viên kho", Role.InventoryStaff)]);
        _selectedEmployeeFilter = EmployeeFilters[0];
        _selectedAccountFilter = AccountFilters[0];
        _selectedRole = RoleOptions[2];

        SearchCommand = new AsyncRelayCommand(LoadAsync, CanLoad, HandleException);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, CanLoad, HandleException);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => CanLoad() && PageNumber > 1, HandleException);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => CanLoad() && PageNumber < TotalPages, HandleException);
        NewEmployeeCommand = new AsyncRelayCommand(NewEmployeeAsync, () => CanLoad() && CanManageEmployees, HandleException);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanLoad() && CanManageEmployees, HandleException);
        CreateAccountCommand = new AsyncRelayCommand(CreateAccountAsync, () => CanLoad() && CanManageAccounts && _details?.UserId is null, HandleException);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => CanLoad() && CanResetPasswords && _details?.UserId is not null, HandleException);
        ToggleLockCommand = new AsyncRelayCommand(ToggleLockAsync, () => CanLoad() && CanLockAccounts && _details?.UserId is not null, HandleException);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => CanLoad() && CanManageEmployees && _details is not null, HandleException);
        ChangeRoleCommand = new AsyncRelayCommand(ChangeRoleAsync, () => CanLoad() && CanAssignRoles && _details?.UserId is not null, HandleException);
    }

    public ObservableCollection<EmployeeRowViewModel> Employees { get; } = [];
    public ObservableCollection<EmployeeFilterOption> EmployeeFilters { get; }
    public ObservableCollection<AccountFilterOption> AccountFilters { get; }
    public ObservableCollection<RoleOption> RoleOptions { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public AsyncRelayCommand NewEmployeeCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CreateAccountCommand { get; }
    public AsyncRelayCommand ResetPasswordCommand { get; }
    public AsyncRelayCommand ToggleLockCommand { get; }
    public AsyncRelayCommand ToggleActiveCommand { get; }
    public AsyncRelayCommand ChangeRoleCommand { get; }

    public string SearchTerm { get => _searchTerm; set => SetProperty(ref _searchTerm, value ?? string.Empty); }
    public EmployeeFilterOption SelectedEmployeeFilter { get => _selectedEmployeeFilter; set => SetProperty(ref _selectedEmployeeFilter, value ?? EmployeeFilters[0]); }
    public AccountFilterOption SelectedAccountFilter { get => _selectedAccountFilter; set => SetProperty(ref _selectedAccountFilter, value ?? AccountFilters[0]); }
    public RoleOption? SelectedRoleFilter { get => _selectedRoleFilter; set => SetProperty(ref _selectedRoleFilter, value); }
    public EmployeeRowViewModel? SelectedEmployee { get => _selectedEmployee; set { if (SetProperty(ref _selectedEmployee, value) && value is not null) _ = LoadSelectedAsync(value.Id); } }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCommands(); } }
    public bool IsDirty { get => _isDirty; private set { if (SetProperty(ref _isDirty, value)) OnPropertyChanged(nameof(DirtyStateText)); } }
    public string DirtyStateText => IsDirty ? "Có thay đổi chưa lưu" : "Đã lưu";
    public bool IsCreateMode { get => _isCreateMode; private set { if (SetProperty(ref _isCreateMode, value)) OnPropertyChanged(nameof(EditorTitle)); } }
    public string EditorTitle => IsCreateMode ? "Thêm nhân viên" : "Thông tin nhân viên";
    public string EmployeeCode { get => _employeeCode; set { if (SetProperty(ref _employeeCode, value ?? string.Empty)) MarkDirty(); } }
    public string FullName { get => _fullName; set { if (SetProperty(ref _fullName, value ?? string.Empty)) MarkDirty(); } }
    public string PhoneNumber { get => _phoneNumber; set { if (SetProperty(ref _phoneNumber, value ?? string.Empty)) MarkDirty(); } }
    public string EmailAddress { get => _emailAddress; set { if (SetProperty(ref _emailAddress, value ?? string.Empty)) MarkDirty(); } }
    public string Username { get => _username; set { if (SetProperty(ref _username, value ?? string.Empty)) MarkDirty(); } }
    public bool CreateAccount { get => _createAccount; set { if (SetProperty(ref _createAccount, value)) { MarkDirty(); OnPropertyChanged(nameof(ShowAccountEditor)); } } }
    public bool ShowAccountEditor => IsCreateMode ? CreateAccount : _details?.UserId is null;
    public RoleOption SelectedRole { get => _selectedRole; set { if (SetProperty(ref _selectedRole, value ?? RoleOptions[2])) MarkDirty(); } }
    public string StatusMessage { get => _statusMessage; private set { if (SetProperty(ref _statusMessage, value)) OnPropertyChanged(nameof(HasStatusMessage)); } }
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool IsStatusError { get => _isStatusError; private set => SetProperty(ref _isStatusError, value); }
    public int PageNumber { get => _pageNumber; private set { if (SetProperty(ref _pageNumber, value)) NotifyCommands(); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) NotifyCommands(); } }
    public int TotalCount { get => _totalCount; private set => SetProperty(ref _totalCount, value); }
    public string PageText => TotalPages == 0 ? "Không có dữ liệu" : $"Trang {PageNumber}/{TotalPages} • {TotalCount:N0} nhân viên";
    public string AccountStatusText => _details?.AccountStatus switch { AccountStatus.NoAccount => "Chưa có tài khoản", AccountStatus.Active => "Đang hoạt động", AccountStatus.Locked => "Đang bị khóa", AccountStatus.Disabled => "Đã vô hiệu hóa", AccountStatus.ForcePasswordChange => "Chờ đổi mật khẩu", _ => "—" };
    public string EmployeeStatusText => _details?.EmployeeStatus == EmployeeStatus.Active ? "Đang làm việc" : "Ngừng hoạt động";
    public string LastLoginText => _details?.LastSuccessfulLoginUtc is null ? "Chưa đăng nhập" : _details.LastSuccessfulLoginUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
    public string FailedLoginText => (_details?.FailedLoginAttempts ?? 0).ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string EffectivePermissionsText => _details is null || _details.EffectivePermissions.Count == 0 ? "Chưa có" : string.Join(", ", _details.EffectivePermissions.Select(RolePermissionPolicy.GetDisplayName));
    public bool CanManageEmployees => _permissionService.HasPermission(SystemCapability.ManageEmployees);
    public bool CanManageAccounts => _permissionService.HasPermission(SystemCapability.ManageAccounts);
    public bool CanResetPasswords => _permissionService.HasPermission(SystemCapability.ResetPasswords);
    public bool CanLockAccounts => _permissionService.HasPermission(SystemCapability.LockUnlockAccounts);
    public bool CanAssignRoles => _permissionService.HasPermission(SystemCapability.AssignRolesPermissions);
    public string ToggleActiveText => _details?.EmployeeStatus == EmployeeStatus.Active ? "Ngừng hoạt động" : "Kích hoạt nhân viên";
    public string ToggleLockText => _details?.IsManuallyLocked == true ? "Mở khóa tài khoản" : "Khóa tài khoản";

    public async Task InitializeAsync() => await LoadAsync();

    public void SetAccountPassword(string? password) => _accountPassword = password ?? string.Empty;
    public void SetResetPassword(string? password) => _resetPassword = password ?? string.Empty;

    public async Task SelectEmployeeAsync(int employeeId)
    {
        await LoadSelectedAsync(employeeId);
    }

    private async Task LoadAsync()
    {
        if (!CanLoad()) return;
        IsBusy = true; IsStatusError = false; StatusMessage = "Đang tải danh sách nhân viên...";
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>();
            var result = await service.SearchAsync(new EmployeeSearchRequest
            {
                SearchTerm = SearchTerm, EmployeeStatus = SelectedEmployeeFilter.Value, AccountStatus = SelectedAccountFilter.Value,
                Role = SelectedRoleFilter?.Value, PageNumber = PageNumber, PageSize = 20
            });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            Employees.Clear(); foreach (var item in result.Value.Items) Employees.Add(new EmployeeRowViewModel(item));
            TotalCount = result.Value.TotalCount; TotalPages = Math.Max(1, result.Value.TotalPages); OnPropertyChanged(nameof(PageText));
            StatusMessage = Employees.Count == 0 ? "Không tìm thấy nhân viên phù hợp." : $"Đã tải {Employees.Count:N0} nhân viên.";
        }
        catch (Exception exception) { HandleException(exception); }
        finally { IsBusy = false; }
    }

    private async Task LoadSelectedAsync(int employeeId)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().GetAsync(employeeId);
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            _details = result.Value; IsCreateMode = false; AssignDetails(result.Value); NotifyDetailState();
        }
        catch (Exception exception) { HandleException(exception); }
    }

    private Task NewEmployeeAsync()
    {
        _details = null;
        _suppressDirty = true;
        try
        {
            IsCreateMode = true; EmployeeCode = string.Empty; FullName = string.Empty; PhoneNumber = string.Empty; EmailAddress = string.Empty; Username = string.Empty; _accountPassword = string.Empty; CreateAccount = false; SelectedRole = RoleOptions[2];
        }
        finally
        {
            _suppressDirty = false;
        }
        IsDirty = false;
        NotifyDetailState();
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>();
            Result<EmployeeDetailsDto> result;
            if (IsCreateMode)
            {
                result = await service.CreateEmployeeAsync(new CreateEmployeeRequest { EmployeeCode = EmployeeCode, FullName = FullName, PhoneNumber = PhoneNumber, EmailAddress = EmailAddress, CreateAccount = CreateAccount, Username = CreateAccount ? Username : null, TemporaryPassword = CreateAccount ? _accountPassword : null, Role = SelectedRole.Value });
            }
            else if (_details is not null)
            {
                result = await service.UpdateEmployeeAsync(new UpdateEmployeeRequest { EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, EmployeeCode = EmployeeCode, FullName = FullName, PhoneNumber = PhoneNumber, EmailAddress = EmailAddress });
            }
            else return;
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            _details = result.Value; IsCreateMode = false; AssignDetails(result.Value); ShowSuccess("Thông tin nhân viên đã được lưu."); await LoadAsync();
        }
        catch (Exception exception) { HandleException(exception); }
        finally { IsBusy = false; }
    }

    private async Task CreateAccountAsync()
    {
        if (_details is null) return; IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().CreateAccountAsync(new CreateEmployeeAccountRequest { EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, Username = Username, TemporaryPassword = _accountPassword, Role = SelectedRole.Value });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            _details = result.Value; AssignDetails(result.Value); ShowSuccess("Tài khoản đã được tạo và yêu cầu đổi mật khẩu lần đầu."); await LoadAsync();
        }
        catch (Exception exception) { HandleException(exception); }
        finally { IsBusy = false; }
    }

    private async Task ResetPasswordAsync()
    {
        if (_details is null) return; IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().ResetPasswordAsync(new ResetEmployeePasswordRequest { EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, TemporaryPassword = _resetPassword });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            _resetPassword = string.Empty; ShowSuccess("Mật khẩu tạm thời đã được đặt lại; tài khoản phải đổi mật khẩu khi đăng nhập."); await LoadSelectedAsync(_details.Id); await LoadAsync();
        }
        catch (Exception exception) { HandleException(exception); }
        finally { IsBusy = false; }
    }

    private async Task ToggleLockAsync()
    {
        if (_details is null) return; IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().SetAccountLockAsync(new SetAccountLockRequest { EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, Locked = !_details.IsManuallyLocked });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            ShowSuccess(_details.IsManuallyLocked ? "Tài khoản đã được mở khóa." : "Tài khoản đã được khóa."); await LoadSelectedAsync(_details.Id); await LoadAsync();
        }
        catch (Exception exception) { HandleException(exception); }
        finally { IsBusy = false; }
    }

    private async Task ToggleActiveAsync()
    {
        if (_details is null) return; IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var active = _details.EmployeeStatus != EmployeeStatus.Active;
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().SetEmployeeActiveAsync(new SetEmployeeActiveRequest { EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, Active = active });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            ShowSuccess(active ? "Nhân viên đã được kích hoạt." : "Nhân viên đã ngừng hoạt động và tài khoản đã bị vô hiệu hóa."); await LoadSelectedAsync(_details.Id); await LoadAsync();
        }
        catch (Exception exception) { HandleException(exception); }
        finally { IsBusy = false; }
    }

    private async Task ChangeRoleAsync()
    {
        if (_details is null) return; IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().ChangeRoleAsync(new ChangeEmployeeRoleRequest { EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, Role = SelectedRole.Value });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            ShowSuccess("Vai trò tài khoản đã được cập nhật."); await LoadSelectedAsync(_details.Id); await LoadAsync();
        }
        catch (Exception exception) { HandleException(exception); }
        finally { IsBusy = false; }
    }

    private async Task ChangePageAsync(int delta) { PageNumber += delta; await LoadAsync(); }
    private bool CanLoad() => !IsBusy;

    private void AssignDetails(EmployeeDetailsDto details)
    {
        _suppressDirty = true;
        try
        {
            EmployeeCode = details.EmployeeCode; FullName = details.FullName; PhoneNumber = details.PhoneNumber ?? string.Empty; EmailAddress = details.EmailAddress ?? string.Empty; Username = details.Username ?? string.Empty; SelectedRole = RoleOptions.FirstOrDefault(option => option.Value == details.Role) ?? RoleOptions[2];
        }
        finally
        {
            _suppressDirty = false;
        }
        IsDirty = false;
        OnPropertyChanged(nameof(ShowAccountEditor));
    }

    private void NotifyDetailState()
    {
        OnPropertyChanged(nameof(ShowAccountEditor)); OnPropertyChanged(nameof(AccountStatusText)); OnPropertyChanged(nameof(EmployeeStatusText)); OnPropertyChanged(nameof(LastLoginText)); OnPropertyChanged(nameof(FailedLoginText)); OnPropertyChanged(nameof(EffectivePermissionsText)); OnPropertyChanged(nameof(ToggleActiveText)); OnPropertyChanged(nameof(ToggleLockText)); OnPropertyChanged(nameof(DirtyStateText)); NotifyCommands();
    }

    public void DiscardUnsavedChanges()
    {
        _accountPassword = string.Empty;
        _resetPassword = string.Empty;
        IsDirty = false;
    }

    private void MarkDirty()
    {
        if (!_suppressDirty)
            IsDirty = true;
    }

    private void NotifyCommands()
    {
        foreach (var command in new[] { SearchCommand, RefreshCommand, PreviousPageCommand, NextPageCommand, NewEmployeeCommand, SaveCommand, CreateAccountCommand, ResetPasswordCommand, ToggleLockCommand, ToggleActiveCommand, ChangeRoleCommand }) command.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PageText));
    }

    private void ShowSuccess(string message) { IsStatusError = false; StatusMessage = message; }
    private void ShowError(string message) { IsStatusError = true; StatusMessage = message; }
    private void HandleException(Exception exception) { global::POS.Application.Common.PosLog.Error(_logger, exception, "Thao tác quản lý nhân viên thất bại."); ShowError("Thao tác không thể hoàn thành. Vui lòng thử lại."); }
}
