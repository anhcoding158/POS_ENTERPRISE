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
public sealed record RoleFilterOption(string DisplayName, Role? Value)
{
    public override string ToString() => DisplayName;
}
public sealed record PermissionGroupViewModel(string DisplayName, IReadOnlyList<string> Permissions);

public sealed class EmployeeRowViewModel
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public EmployeeRowViewModel(EmployeeListItemDto dto)
    {
        Id = dto.Id;
        EmployeeCode = dto.EmployeeCode;
        FullName = dto.FullName;
        PhoneNumber = dto.PhoneNumber;
        EmployeeStatus = dto.EmployeeStatus;
        Username = dto.Username;
        AccountStatus = dto.AccountStatus;
        Role = dto.Role;
        LastSuccessfulLoginUtc = dto.LastSuccessfulLoginUtc;
        FailedLoginAttempts = dto.FailedLoginAttempts;
        UpdatedAtUtc = dto.UpdatedAtUtc;
    }

    public int Id { get; }
    public string EmployeeCode { get; }
    public string FullName { get; }
    public string Initials => GetInitials(FullName);
    public string? PhoneNumber { get; }
    public EmployeeStatus EmployeeStatus { get; }
    public string EmployeeStatusText => EmployeeStatus == EmployeeStatus.Active ? "Đang làm việc" : "Ngừng hoạt động";
    public string? Username { get; }
    public string UsernameText => Username ?? "—";
    public AccountStatus AccountStatus { get; }
    public string AccountStatusText => AccountStatus switch
    {
        AccountStatus.NoAccount => "Chưa có tài khoản",
        AccountStatus.Active => "Đang hoạt động",
        AccountStatus.Locked => "Đã khóa",
        AccountStatus.Disabled => "Đã vô hiệu hóa",
        AccountStatus.ForcePasswordChange => "Chờ đổi mật khẩu",
        _ => "Không xác định"
    };
    public Role? Role { get; }
    public string RoleText => Role is null ? "—" : RolePermissionPolicy.GetRoleDisplayName(Role.Value);
    public DateTimeOffset? LastSuccessfulLoginUtc { get; }
    public string LastLoginText => LastSuccessfulLoginUtc is null ? "Chưa đăng nhập" : LastSuccessfulLoginUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", VietnameseCulture);
    public int FailedLoginAttempts { get; }
    public string FailedLoginText => FailedLoginAttempts.ToString("N0", VietnameseCulture);
    public DateTimeOffset UpdatedAtUtc { get; }

    private static string GetInitials(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return "NV";
        return words.Length == 1 ? words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant() : string.Concat(words[0][0], words[^1][0]).ToUpperInvariant();
    }
}

public sealed class EmployeeManagementViewModel : ViewModelBase, IDisposable
{
    private const int DefaultPageSize = 20;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<EmployeeManagementViewModel> _logger;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _detailCancellation;
    private string _searchTerm = string.Empty;
    private EmployeeFilterOption _selectedEmployeeFilter;
    private AccountFilterOption _selectedAccountFilter;
    private RoleFilterOption _selectedRoleFilter;
    private EmployeeRowViewModel? _selectedEmployee;
    private EmployeeDetailsDto? _details;
    private bool _isBusy;
    private bool _isDetailBusy;
    private bool _isCreateMode;
    private bool _isEditing;
    private bool _isCreatingAccount;
    private bool _isLoaded;
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
    private int _selectedPageSize = DefaultPageSize;
    private int _totalEmployees;
    private int _activeEmployees;
    private int _accountsNeedingAttention;
    private int _selectedDetailTabIndex;

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
        RoleFilterOptions = new ObservableCollection<RoleFilterOption>([
            new("Tất cả vai trò", null), new("Quản trị viên", Role.Administrator), new("Quản lý", Role.Manager), new("Thu ngân", Role.Cashier), new("Nhân viên kho", Role.InventoryStaff)]);
        PageSizeOptions = new ObservableCollection<int>([10, 20, 50]);

        _selectedEmployeeFilter = EmployeeFilters[0];
        _selectedAccountFilter = AccountFilters[0];
        _selectedRole = RoleOptions[2];
        _selectedRoleFilter = RoleFilterOptions[0];

        SearchCommand = new AsyncRelayCommand(() => LoadAsync(resetPage: true), CanLoad, HandleException);
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(), CanLoad, HandleException);
        ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync, CanLoad, HandleException);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => CanLoad() && PageNumber > 1, HandleException);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => CanLoad() && PageNumber < TotalPages, HandleException);
        FirstPageCommand = new AsyncRelayCommand(() => GoToPageAsync(1), () => CanLoad() && PageNumber > 1, HandleException);
        LastPageCommand = new AsyncRelayCommand(() => GoToPageAsync(TotalPages), () => CanLoad() && PageNumber < TotalPages, HandleException);
        NewEmployeeCommand = new AsyncRelayCommand(NewEmployeeAsync, () => CanLoad() && CanManageEmployees, HandleException);
        EditProfileCommand = new AsyncRelayCommand(BeginEditAsync, () => CanLoad() && CanManageEmployees && HasSelection && !IsEditing, HandleException);
        CancelEditCommand = new AsyncRelayCommand(CancelEditAsync, () => !IsBusy && (IsEditing || IsDirty), HandleException);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanLoad() && CanManageEmployees && (IsCreateMode || IsEditing), HandleException);
        BeginCreateAccountCommand = new AsyncRelayCommand(BeginCreateAccountAsync, () => CanLoad() && CanManageAccounts && HasSelection && !HasAccount, HandleException);
        CreateAccountCommand = new AsyncRelayCommand(CreateAccountAsync, () => CanLoad() && CanManageAccounts && HasSelection && IsCreatingAccount && !HasAccount, HandleException);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => CanLoad() && CanResetPasswords && HasAccount, HandleException);
        ToggleLockCommand = new AsyncRelayCommand(ToggleLockAsync, () => CanLoad() && CanLockAccounts && HasAccount, HandleException);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => CanLoad() && CanManageEmployees && HasSelection, HandleException);
        ChangeRoleCommand = new AsyncRelayCommand(ChangeRoleAsync, () => CanLoad() && CanAssignRoles && HasAccount, HandleException);
    }

    public ObservableCollection<EmployeeRowViewModel> Employees { get; } = [];
    public ObservableCollection<EmployeeFilterOption> EmployeeFilters { get; }
    public ObservableCollection<AccountFilterOption> AccountFilters { get; }
    public ObservableCollection<RoleOption> RoleOptions { get; }
    public ObservableCollection<RoleFilterOption> RoleFilterOptions { get; }
    public ObservableCollection<int> PageSizeOptions { get; }
    public ObservableCollection<PermissionGroupViewModel> PermissionGroups { get; } = [];
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ClearFiltersCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public AsyncRelayCommand FirstPageCommand { get; }
    public AsyncRelayCommand LastPageCommand { get; }
    public AsyncRelayCommand NewEmployeeCommand { get; }
    public AsyncRelayCommand EditProfileCommand { get; }
    public AsyncRelayCommand CancelEditCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand BeginCreateAccountCommand { get; }
    public AsyncRelayCommand CreateAccountCommand { get; }
    public AsyncRelayCommand ResetPasswordCommand { get; }
    public AsyncRelayCommand ToggleLockCommand { get; }
    public AsyncRelayCommand ToggleActiveCommand { get; }
    public AsyncRelayCommand ChangeRoleCommand { get; }

    public string SearchTerm { get => _searchTerm; set { if (SetProperty(ref _searchTerm, value ?? string.Empty)) NotifyFilterState(); } }
    public EmployeeFilterOption SelectedEmployeeFilter { get => _selectedEmployeeFilter; set { if (SetProperty(ref _selectedEmployeeFilter, value ?? EmployeeFilters[0])) NotifyFilterState(); } }
    public AccountFilterOption SelectedAccountFilter { get => _selectedAccountFilter; set { if (SetProperty(ref _selectedAccountFilter, value ?? AccountFilters[0])) NotifyFilterState(); } }
    public RoleFilterOption SelectedRoleFilter { get => _selectedRoleFilter; set { if (SetProperty(ref _selectedRoleFilter, value ?? RoleFilterOptions[0])) NotifyFilterState(); } }
    public EmployeeRowViewModel? SelectedEmployee { get => _selectedEmployee; set => SetProperty(ref _selectedEmployee, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) { OnPropertyChanged(nameof(IsLoadingState)); NotifyCommands(); } } }
    public bool IsDetailBusy { get => _isDetailBusy; private set { if (SetProperty(ref _isDetailBusy, value)) OnPropertyChanged(nameof(IsLoadingState)); } }
    public bool IsLoaded { get => _isLoaded; private set => SetProperty(ref _isLoaded, value); }
    public bool IsDirty { get => _isDirty; private set { if (SetProperty(ref _isDirty, value)) { OnPropertyChanged(nameof(DirtyStateText)); NotifyCommands(); } } }
    public bool IsCreateMode { get => _isCreateMode; private set { if (SetProperty(ref _isCreateMode, value)) { OnPropertyChanged(nameof(EditorTitle)); OnPropertyChanged(nameof(HasDetail)); NotifyCommands(); } } }
    public bool IsEditing { get => _isEditing; private set { if (SetProperty(ref _isEditing, value)) { OnPropertyChanged(nameof(IsReadOnly)); NotifyCommands(); } } }
    public bool IsReadOnly => !IsEditing;
    public bool IsCreatingAccount { get => _isCreatingAccount; private set { if (SetProperty(ref _isCreatingAccount, value)) { OnPropertyChanged(nameof(ShowAccountEditor)); NotifyCommands(); } } }
    public bool HasSelection => _details is not null;
    public bool HasDetail => IsCreateMode || SelectedEmployee is not null;
    public bool HasDetailContent => IsCreateMode || HasSelection;
    public bool HasAccount => _details?.UserId is not null;
    public bool HasEmployees => Employees.Count > 0;
    public bool HasActiveSearchOrFilter => !string.IsNullOrWhiteSpace(SearchTerm) || SelectedEmployeeFilter.Value is not null || SelectedAccountFilter.Value is not null || SelectedRoleFilter.Value is not null;
    public bool HasSearchOrFilter => HasActiveSearchOrFilter;
    public bool IsTrueEmployeeDatabaseEmpty => IsLoaded && !IsBusy && GlobalEmployeeCount == 0;
    public bool IsFilteredNoResult => IsLoaded && !IsBusy && GlobalEmployeeCount > 0 && FilteredResultCount == 0 && HasActiveSearchOrFilter;
    public bool IsEmptyState => IsTrueEmployeeDatabaseEmpty;
    public bool IsNoResultState => IsFilteredNoResult;
    public bool IsLoadingState => IsBusy || IsDetailBusy;
    public bool IsStatusVisible => !string.IsNullOrWhiteSpace(StatusMessage);
    public string DirtyStateText => IsDirty ? "Có thay đổi chưa lưu" : "Đã lưu";
    public string EditorTitle => IsCreateMode ? "Thêm nhân viên" : "Thông tin nhân viên";
    public string EmployeeCode { get => _employeeCode; set { if (SetProperty(ref _employeeCode, value ?? string.Empty)) MarkDirty(); } }
    public string FullName { get => _fullName; set { if (SetProperty(ref _fullName, value ?? string.Empty)) MarkDirty(); } }
    public string PhoneNumber { get => _phoneNumber; set { if (SetProperty(ref _phoneNumber, value ?? string.Empty)) { OnPropertyChanged(nameof(PhoneDisplayText)); MarkDirty(); } } }
    public string EmailAddress { get => _emailAddress; set { if (SetProperty(ref _emailAddress, value ?? string.Empty)) { OnPropertyChanged(nameof(EmailDisplayText)); MarkDirty(); } } }
    public string PhoneDisplayText => string.IsNullOrWhiteSpace(PhoneNumber) ? "Chưa cập nhật" : PhoneNumber;
    public string EmailDisplayText => string.IsNullOrWhiteSpace(EmailAddress) ? "Chưa cập nhật" : EmailAddress;
    public string Username { get => _username; set { if (SetProperty(ref _username, value ?? string.Empty)) MarkDirty(); } }
    public bool CreateAccount { get => _createAccount; set { if (SetProperty(ref _createAccount, value)) { MarkDirty(); OnPropertyChanged(nameof(ShowAccountEditor)); } } }
    public bool ShowAccountEditor => IsCreatingAccount || (IsCreateMode && CreateAccount);
    public RoleOption SelectedRole { get => _selectedRole; set { if (SetProperty(ref _selectedRole, value ?? RoleOptions[2]) && (IsCreateMode || IsCreatingAccount)) MarkDirty(); } }
    public string StatusMessage { get => _statusMessage; private set { if (SetProperty(ref _statusMessage, value)) OnPropertyChanged(nameof(IsStatusVisible)); } }
    public bool IsStatusError { get => _isStatusError; private set => SetProperty(ref _isStatusError, value); }
    public int PageNumber { get => _pageNumber; private set { if (SetProperty(ref _pageNumber, value)) NotifyCommands(); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) NotifyCommands(); } }
    public int TotalCount { get => _totalCount; private set { if (SetProperty(ref _totalCount, value)) NotifyListState(); } }
    public int FilteredResultCount => TotalCount;
    public int SelectedPageSize { get => _selectedPageSize; set { if (SetProperty(ref _selectedPageSize, value) && IsLoaded && !IsBusy) _ = LoadAsync(resetPage: true); } }
    public int GlobalEmployeeCount => TotalEmployees;
    public int TotalEmployees { get => _totalEmployees; private set { if (SetProperty(ref _totalEmployees, value)) NotifyListState(); } }
    public int ActiveEmployees { get => _activeEmployees; private set => SetProperty(ref _activeEmployees, value); }
    public int AccountsNeedingAttention { get => _accountsNeedingAttention; private set => SetProperty(ref _accountsNeedingAttention, value); }
    public int SelectedDetailTabIndex { get => _selectedDetailTabIndex; set => SetProperty(ref _selectedDetailTabIndex, Math.Max(0, value)); }
    public string PageText => TotalCount == 0 ? "Không có nhân viên" : $"Hiển thị {(PageNumber - 1) * SelectedPageSize + 1:N0}–{Math.Min(PageNumber * SelectedPageSize, TotalCount):N0} trên {TotalCount:N0} nhân viên";
    public string PageNumberText => $"Trang {PageNumber} / {Math.Max(1, TotalPages)}";
    public string AccountStatusText => _details?.AccountStatus switch
    {
        AccountStatus.NoAccount => "Chưa có tài khoản", AccountStatus.Active => "Đang hoạt động", AccountStatus.Locked => "Đã khóa",
        AccountStatus.Disabled => "Đã vô hiệu hóa", AccountStatus.ForcePasswordChange => "Chờ đổi mật khẩu", _ => "—"
    };
    public string EmployeeStatusText => _details?.EmployeeStatus == EmployeeStatus.Active ? "Đang làm việc" : "Ngừng hoạt động";
    public string LastLoginText => _details?.LastSuccessfulLoginUtc is null ? "Chưa đăng nhập" : _details.LastSuccessfulLoginUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
    public string FailedLoginText => $"{(_details?.FailedLoginAttempts ?? 0).ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} lần";
    public string ForcePasswordChangeText => _details?.ForcePasswordChange == true ? "Đang yêu cầu đổi mật khẩu" : "Không yêu cầu đổi mật khẩu";
    public string EffectivePermissionsText => PermissionGroups.Count == 0
        ? "Chưa có"
        : string.Join(", ", PermissionGroups.SelectMany(group => group.Permissions));
    public string RoleText => _details?.Role is Role role ? RolePermissionPolicy.GetRoleDisplayName(role) : "Chưa có vai trò";
    public string UsernameText => _details?.Username ?? "Chưa có tài khoản";
    public string SelectedInitials => _details is null ? "NV" : GetInitials(_details.FullName);
    public string ToggleActiveText => _details?.EmployeeStatus == EmployeeStatus.Active ? "Ngừng hoạt động" : "Kích hoạt lại";
    public string ToggleLockText => _details?.IsManuallyLocked == true ? "Mở khóa tài khoản" : "Khóa tài khoản";
    public bool CanManageEmployees => _permissionService.HasPermission(SystemCapability.ManageEmployees);
    public bool CanManageAccounts => _permissionService.HasPermission(SystemCapability.ManageAccounts);
    public bool CanResetPasswords => _permissionService.HasPermission(SystemCapability.ResetPasswords);
    public bool CanLockAccounts => _permissionService.HasPermission(SystemCapability.LockUnlockAccounts);
    public bool CanAssignRoles => _permissionService.HasPermission(SystemCapability.AssignRolesPermissions);

    public async Task InitializeAsync() => await LoadAsync();
    public async Task ApplyFiltersAsync() => await LoadAsync(resetPage: true);
    public void SetAccountPassword(string? password) => _accountPassword = password ?? string.Empty;
    public void SetResetPassword(string? password) => _resetPassword = password ?? string.Empty;

    public async Task SelectEmployeeAsync(int employeeId)
    {
        var row = Employees.FirstOrDefault(employee => employee.Id == employeeId);
        if (row is not null) SelectedEmployee = row;
        await LoadSelectedAsync(employeeId);
    }

    public void RestoreSelection(EmployeeRowViewModel? row) => SelectedEmployee = row;

    private async Task LoadAsync(bool resetPage = false, int? preferredSelectionId = null, bool allowWhenBusy = false)
    {
        if (!allowWhenBusy && !CanLoad()) return;
        if (resetPage) PageNumber = 1;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;
        IsBusy = true; IsLoaded = false; IsStatusError = false; StatusMessage = "Đang tải danh sách nhân viên...";
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>();
            var result = await service.SearchAsync(new EmployeeSearchRequest
            {
                SearchTerm = SearchTerm, EmployeeStatus = SelectedEmployeeFilter.Value, AccountStatus = SelectedAccountFilter.Value,
                Role = SelectedRoleFilter?.Value, PageNumber = PageNumber, PageSize = SelectedPageSize
            }, token);
            token.ThrowIfCancellationRequested();
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            var summary = await service.GetSummaryAsync(token);
            token.ThrowIfCancellationRequested();
            if (summary.IsFailure)
            {
                ShowError(summary.AppError.Message);
                return;
            }

            TotalEmployees = summary.Value.TotalEmployees;
            ActiveEmployees = summary.Value.ActiveEmployees;
            AccountsNeedingAttention = summary.Value.AccountsNeedingAttention;

            var oldSelectedId = preferredSelectionId ?? SelectedEmployee?.Id;
            Employees.Clear();
            foreach (var item in result.Value.Items) Employees.Add(new EmployeeRowViewModel(item));
            TotalCount = result.Value.TotalCount; TotalPages = Math.Max(1, result.Value.TotalPages); IsLoaded = true;
            OnPropertyChanged(nameof(HasEmployees)); NotifyListState();

            var nextSelection = Employees.FirstOrDefault(employee => employee.Id == oldSelectedId) ?? Employees.FirstOrDefault();
            IsCreateMode = false; IsEditing = false; IsCreatingAccount = false; _details = null; PermissionGroups.Clear(); NotifyDetailState();
            SelectedEmployee = nextSelection;
            if (nextSelection is not null) await LoadSelectedAsync(nextSelection.Id);
            else StatusMessage = string.Empty;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) { HandleException(exception); }
        finally { if (!token.IsCancellationRequested) IsBusy = false; }
    }

    private async Task LoadSelectedAsync(int employeeId)
    {
        _detailCancellation?.Cancel(); _detailCancellation?.Dispose(); _detailCancellation = new CancellationTokenSource();
        var token = _detailCancellation.Token;
        IsDetailBusy = true; _details = null; PermissionGroups.Clear(); NotifyDetailState();
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().GetAsync(employeeId, token);
            token.ThrowIfCancellationRequested();
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            _details = result.Value; IsCreateMode = false; IsEditing = false; IsCreatingAccount = false; AssignDetails(result.Value); NotifyDetailState();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) { HandleException(exception); }
        finally { if (!token.IsCancellationRequested) IsDetailBusy = false; }
    }

    private Task NewEmployeeAsync()
    {
        _details = null; SelectedEmployee = null; IsCreateMode = true; IsEditing = true; IsCreatingAccount = false; SelectedDetailTabIndex = 0; _suppressDirty = true;
        try
        {
            EmployeeCode = string.Empty; FullName = string.Empty; PhoneNumber = string.Empty; EmailAddress = string.Empty; Username = string.Empty;
            _accountPassword = string.Empty; CreateAccount = false; SelectedRole = RoleOptions[2];
        }
        finally { _suppressDirty = false; }
        IsDirty = false; NotifyDetailState(); return Task.CompletedTask;
    }

    private Task BeginEditAsync() { if (HasSelection) { IsEditing = true; IsDirty = false; } return Task.CompletedTask; }
    private Task BeginCreateAccountAsync() { if (HasSelection && !HasAccount) { IsCreatingAccount = true; SelectedDetailTabIndex = 1; IsDirty = false; } return Task.CompletedTask; }

    private async Task CancelEditAsync()
    {
        if (IsCreatingAccount)
        {
            IsCreatingAccount = false;
            _accountPassword = string.Empty;
            if (_details is not null) AssignDetails(_details);
            IsDirty = false;
            NotifyDetailState();
            return;
        }
        if (IsCreateMode) { IsCreateMode = false; IsEditing = false; IsDirty = false; NotifyDetailState(); return; }
        if (_details is not null) { AssignDetails(_details); IsEditing = false; }
        IsDirty = false; NotifyDetailState(); await Task.CompletedTask;
    }

    private async Task ClearFiltersAsync()
    {
        SearchTerm = string.Empty; SelectedEmployeeFilter = EmployeeFilters[0]; SelectedAccountFilter = AccountFilters[0]; SelectedRoleFilter = RoleFilterOptions[0];
        await LoadAsync(resetPage: true);
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>();
            Result<EmployeeDetailsDto> result;
            var wasCreate = IsCreateMode;
            var continueToAccount = wasCreate && CreateAccount;
            if (IsCreateMode)
            {
                result = await service.CreateEmployeeAsync(new CreateEmployeeRequest
                {
                    EmployeeCode = EmployeeCode, FullName = FullName, PhoneNumber = PhoneNumber, EmailAddress = EmailAddress,
                    CreateAccount = false, Username = null, TemporaryPassword = null, Role = SelectedRole.Value
                });
            }
            else if (_details is not null)
            {
                result = await service.UpdateEmployeeAsync(new UpdateEmployeeRequest
                {
                    EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, EmployeeCode = EmployeeCode, FullName = FullName,
                    PhoneNumber = PhoneNumber, EmailAddress = EmailAddress
                });
            }
            else return;
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            var savedId = result.Value.Id; _details = result.Value; IsCreateMode = false; IsEditing = false; AssignDetails(result.Value);
            if (wasCreate && HasActiveSearchOrFilter)
            {
                SearchTerm = string.Empty;
                SelectedEmployeeFilter = EmployeeFilters[0];
                SelectedAccountFilter = AccountFilters[0];
                SelectedRoleFilter = RoleFilterOptions[0];
            }
            ShowSuccess("Thông tin nhân viên đã được lưu."); await LoadAsync(preferredSelectionId: savedId, allowWhenBusy: true);
            if (continueToAccount && HasSelection && !HasAccount)
            {
                IsCreatingAccount = true;
                SelectedDetailTabIndex = 1;
                IsDirty = false;
                NotifyDetailState();
            }
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
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().CreateAccountAsync(new CreateEmployeeAccountRequest
            {
                EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, Username = Username, TemporaryPassword = _accountPassword, Role = SelectedRole.Value
            });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            _details = result.Value; AssignDetails(result.Value); IsCreatingAccount = false; ShowSuccess("Tài khoản đã được tạo và yêu cầu đổi mật khẩu lần đầu.");
            await LoadAsync(preferredSelectionId: result.Value.Id, allowWhenBusy: true);
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
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().ResetPasswordAsync(new ResetEmployeePasswordRequest
            {
                EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, TemporaryPassword = _resetPassword
            });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            _resetPassword = string.Empty; ShowSuccess("Mật khẩu tạm thời đã được đặt lại; tài khoản phải đổi mật khẩu khi đăng nhập."); await LoadAsync(preferredSelectionId: _details.Id, allowWhenBusy: true);
        }
        catch (Exception exception) { HandleException(exception); }
        finally { IsBusy = false; }
    }

    private async Task ToggleLockAsync()
    {
        if (_details is null) return; IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope(); var wasLocked = _details.IsManuallyLocked;
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().SetAccountLockAsync(new SetAccountLockRequest
            {
                EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, Locked = !wasLocked
            });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            ShowSuccess(wasLocked ? "Tài khoản đã được mở khóa." : "Tài khoản đã được khóa."); await LoadAsync(preferredSelectionId: _details.Id, allowWhenBusy: true);
        }
        catch (Exception exception) { HandleException(exception); }
        finally { IsBusy = false; }
    }

    private async Task ToggleActiveAsync()
    {
        if (_details is null) return; IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope(); var active = _details.EmployeeStatus != EmployeeStatus.Active;
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().SetEmployeeActiveAsync(new SetEmployeeActiveRequest
            {
                EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, Active = active
            });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            ShowSuccess(active ? "Nhân viên đã được kích hoạt." : "Nhân viên đã ngừng hoạt động; tài khoản đã bị vô hiệu hóa."); await LoadAsync(preferredSelectionId: _details.Id, allowWhenBusy: true);
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
            var result = await scope.ServiceProvider.GetRequiredService<IEmployeeAccountService>().ChangeRoleAsync(new ChangeEmployeeRoleRequest
            {
                EmployeeId = _details.Id, ExpectedUpdatedAtUtc = _details.UpdatedAtUtc, Role = SelectedRole.Value
            });
            if (result.IsFailure) { ShowError(result.AppError.Message); return; }
            ShowSuccess("Vai trò tài khoản đã được cập nhật."); await LoadAsync(preferredSelectionId: _details.Id, allowWhenBusy: true);
        }
        catch (Exception exception) { HandleException(exception); }
        finally { IsBusy = false; }
    }

    private async Task ChangePageAsync(int delta) { PageNumber = Math.Clamp(PageNumber + delta, 1, Math.Max(1, TotalPages)); await LoadAsync(); }
    private async Task GoToPageAsync(int page) { PageNumber = Math.Clamp(page, 1, Math.Max(1, TotalPages)); await LoadAsync(); }
    private bool CanLoad() => !IsBusy;

    private void AssignDetails(EmployeeDetailsDto details)
    {
        _suppressDirty = true;
        try
        {
            EmployeeCode = details.EmployeeCode; FullName = details.FullName; PhoneNumber = details.PhoneNumber ?? string.Empty; EmailAddress = details.EmailAddress ?? string.Empty;
            Username = details.Username ?? string.Empty; SelectedRole = RoleOptions.FirstOrDefault(option => option.Value == details.Role) ?? RoleOptions[2];
        }
        finally { _suppressDirty = false; }
        IsDirty = false; BuildPermissionGroups(details.EffectivePermissions); OnPropertyChanged(nameof(ShowAccountEditor));
    }

    private void BuildPermissionGroups(IReadOnlyList<SystemCapability> permissions)
    {
        PermissionGroups.Clear();
        var groups = new (string Name, SystemCapability[] Capabilities)[]
        {
            ("Bán hàng", [SystemCapability.UseCheckout, SystemCapability.ApplySalesDiscount, SystemCapability.ProcessReturns]),
            ("Hàng hóa", [SystemCapability.ViewProductCatalog, SystemCapability.ManageProducts, SystemCapability.ManageCategories, SystemCapability.ViewInventoryHistory, SystemCapability.AdjustInventory]),
            ("Đơn hàng", [SystemCapability.ViewReports]),
            ("Nhân viên và tài khoản", [SystemCapability.ViewEmployees, SystemCapability.ManageEmployees, SystemCapability.ManageAccounts, SystemCapability.ResetPasswords, SystemCapability.LockUnlockAccounts, SystemCapability.AssignRolesPermissions, SystemCapability.ViewSecurityStatus]),
            ("Cấu hình cửa hàng", [SystemCapability.ManageStoreSetup]), ("Sao lưu và khôi phục", [])
        };
        foreach (var group in groups)
        {
            var names = group.Capabilities.Where(permissions.Contains).Select(RolePermissionPolicy.GetDisplayName).ToArray();
            if (names.Length > 0) PermissionGroups.Add(new PermissionGroupViewModel(group.Name, names));
        }
    }

    public void DiscardUnsavedChanges() { _accountPassword = string.Empty; _resetPassword = string.Empty; IsDirty = false; }
    private void MarkDirty() { if (!_suppressDirty) IsDirty = true; }
    private void NotifyListState()
    {
        OnPropertyChanged(nameof(PageText)); OnPropertyChanged(nameof(PageNumberText)); OnPropertyChanged(nameof(HasSearchOrFilter));
        OnPropertyChanged(nameof(HasActiveSearchOrFilter)); OnPropertyChanged(nameof(GlobalEmployeeCount)); OnPropertyChanged(nameof(FilteredResultCount));
        OnPropertyChanged(nameof(IsTrueEmployeeDatabaseEmpty)); OnPropertyChanged(nameof(IsFilteredNoResult));
        OnPropertyChanged(nameof(IsEmptyState)); OnPropertyChanged(nameof(IsNoResultState));
    }
    private void NotifyFilterState()
    {
        OnPropertyChanged(nameof(HasSearchOrFilter)); OnPropertyChanged(nameof(HasActiveSearchOrFilter));
        OnPropertyChanged(nameof(IsTrueEmployeeDatabaseEmpty)); OnPropertyChanged(nameof(IsFilteredNoResult));
        OnPropertyChanged(nameof(IsEmptyState)); OnPropertyChanged(nameof(IsNoResultState));
    }
    private void NotifyDetailState()
    {
        OnPropertyChanged(nameof(HasSelection)); OnPropertyChanged(nameof(HasAccount)); OnPropertyChanged(nameof(HasDetail)); OnPropertyChanged(nameof(HasDetailContent)); OnPropertyChanged(nameof(AccountStatusText));
        OnPropertyChanged(nameof(EmployeeStatusText)); OnPropertyChanged(nameof(LastLoginText)); OnPropertyChanged(nameof(FailedLoginText)); OnPropertyChanged(nameof(ForcePasswordChangeText));
        OnPropertyChanged(nameof(RoleText)); OnPropertyChanged(nameof(UsernameText)); OnPropertyChanged(nameof(PhoneDisplayText)); OnPropertyChanged(nameof(EmailDisplayText)); OnPropertyChanged(nameof(SelectedInitials)); OnPropertyChanged(nameof(EffectivePermissionsText)); OnPropertyChanged(nameof(ToggleActiveText)); OnPropertyChanged(nameof(ToggleLockText));
        OnPropertyChanged(nameof(ShowAccountEditor)); NotifyCommands();
    }
    private void NotifyCommands()
    {
        foreach (var command in new[] { SearchCommand, RefreshCommand, ClearFiltersCommand, PreviousPageCommand, NextPageCommand, FirstPageCommand, LastPageCommand, NewEmployeeCommand, EditProfileCommand, CancelEditCommand, SaveCommand, BeginCreateAccountCommand, CreateAccountCommand, ResetPasswordCommand, ToggleLockCommand, ToggleActiveCommand, ChangeRoleCommand }) command.NotifyCanExecuteChanged();
        NotifyListState();
    }
    private void ShowSuccess(string message) { IsStatusError = false; StatusMessage = message; }
    private void ShowError(string message) { IsStatusError = true; StatusMessage = message; }
    private void HandleException(Exception exception) { PosLog.Error(_logger, exception, "Thao tác quản lý nhân viên thất bại."); ShowError("Thao tác không thể hoàn thành. Vui lòng thử lại."); IsBusy = false; }
    public void Dispose()
    {
        var loadCancellation = Interlocked.Exchange(ref _loadCancellation, null);
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        var detailCancellation = Interlocked.Exchange(ref _detailCancellation, null);
        detailCancellation?.Cancel();
        detailCancellation?.Dispose();
    }
    private static string GetInitials(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return "NV";
        return words.Length == 1 ? words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant() : string.Concat(words[0][0], words[^1][0]).ToUpperInvariant();
    }
}
