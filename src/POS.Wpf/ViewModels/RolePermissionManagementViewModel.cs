using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Domain.Enums;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

public sealed class PermissionRowViewModel
{
    public PermissionRowViewModel(PermissionDefinition definition, bool granted)
    {
        Definition = definition;
        IsGranted = granted;
    }

    public PermissionDefinition Definition { get; }
    public bool IsGranted { get; }
    public string DisplayName => Definition.DisplayName;
    public string Description => Definition.Description;
    public string RiskText => Definition.Risk switch
    {
        PermissionRisk.Dangerous => "Nguy hiểm",
        PermissionRisk.Elevated => "Nâng cao",
        _ => "Tiêu chuẩn"
    };
    public string BusinessArea => Definition.BusinessArea;
}

public sealed class RoleRowViewModel
{
    public RoleRowViewModel(RolePermissionSnapshot snapshot)
    {
        Snapshot = snapshot;
        Permissions = new(snapshot.Permissions.Select(permission => new PermissionRowViewModel(permission, true)));
        DeniedPermissions = new(snapshot.DeniedPermissions.Select(permission => new PermissionRowViewModel(permission, false)));
    }

    public RolePermissionSnapshot Snapshot { get; }
    public Role Role => Snapshot.Role;
    public string DisplayName => Snapshot.DisplayName;
    public string BuiltInText => Snapshot.IsBuiltIn ? "Vai trò hệ thống" : "Vai trò tùy chỉnh";
    public string ProtectionText => Snapshot.IsProtected
        ? "Vai trò bảo vệ; ánh xạ quyền được quản lý tập trung."
        : "Quyền hiệu lực theo policy hiện tại.";
    public int AccountUsageCount => Snapshot.AccountUsageCount;
    public string AccountUsageText => $"{AccountUsageCount:N0} tài khoản";
    public ObservableCollection<PermissionRowViewModel> Permissions { get; }
    public ObservableCollection<PermissionRowViewModel> DeniedPermissions { get; }
}

public sealed class RolePermissionManagementViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private string _statusMessage = "Chưa tải dữ liệu";
    private bool _isBusy;
    private bool _isStatusError;
    private RoleRowViewModel? _selectedRole;

    public RolePermissionManagementViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy, HandleException);
    }

    public ObservableCollection<RoleRowViewModel> Roles { get; } = [];
    public AsyncRelayCommand RefreshCommand { get; }

    public RoleRowViewModel? SelectedRole
    {
        get => _selectedRole;
        set => SetProperty(ref _selectedRole, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsStatusError { get => _isStatusError; private set => SetProperty(ref _isStatusError, value); }
    public bool IsStatusVisible => !string.IsNullOrWhiteSpace(StatusMessage);
    public string StatusMessage { get => _statusMessage; private set { if (SetProperty(ref _statusMessage, value)) OnPropertyChanged(nameof(IsStatusVisible)); } }

    public async Task InitializeAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        IsBusy = true;
        IsStatusError = false;
        StatusMessage = "Đang tải vai trò và quyền hạn...";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRolePermissionManagementService>();
            var result = await service.GetSnapshotAsync();
            if (result.IsFailure)
            {
                IsStatusError = true;
                StatusMessage = result.AppError.Message;
                return;
            }

            var previous = SelectedRole?.Role;
            Roles.Clear();
            foreach (var snapshot in result.Value)
                Roles.Add(new RoleRowViewModel(snapshot));
            SelectedRole = Roles.FirstOrDefault(row => row.Role == previous) ?? Roles.FirstOrDefault();
            StatusMessage = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void HandleException(Exception exception)
    {
        IsStatusError = true;
        StatusMessage = "Không thể tải vai trò và quyền hạn. Vui lòng thử lại.";
        global::System.Diagnostics.Trace.TraceError(
            "RolePermissionManagementViewModel failed: {0}", exception.GetType().FullName);
    }
}
