using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Audit;
using POS.Domain.Enums;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

public sealed class AuditLogRowViewModel
{
    public AuditLogRowViewModel(AuditListItemDto dto)
    {
        Id = dto.Id; OccurredAtUtc = dto.OccurredAtUtc; Actor = dto.Actor; Action = dto.Action;
        BusinessArea = dto.BusinessArea; Target = dto.Target; Result = dto.Result; TerminalId = dto.TerminalId; OperationId = dto.OperationId;
    }
    public int Id { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public string LocalTimeText => OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("vi-VN"));
    public string Actor { get; }
    public SecurityAuditAction Action { get; }
    public string ActionText => Action switch
    {
        SecurityAuditAction.EmployeeCreated => "Tạo nhân viên",
        SecurityAuditAction.EmployeeUpdated => "Cập nhật nhân viên",
        SecurityAuditAction.EmployeeDeactivated => "Ngừng hoạt động nhân viên",
        SecurityAuditAction.EmployeeReactivated => "Kích hoạt lại nhân viên",
        SecurityAuditAction.AccountCreated => "Tạo tài khoản",
        SecurityAuditAction.PasswordReset => "Đặt lại mật khẩu",
        SecurityAuditAction.AccountLocked => "Khóa tài khoản",
        SecurityAuditAction.AccountUnlocked => "Mở khóa tài khoản",
        SecurityAuditAction.RoleChanged => "Thay đổi vai trò",
        SecurityAuditAction.ForcedPasswordChangeCompleted => "Hoàn tất đổi mật khẩu",
        _ => "Hoạt động hệ thống"
    };
    public string BusinessArea { get; }
    public string Target { get; }
    public string Result { get; }
    public string TerminalId { get; }
    public Guid OperationId { get; }
}

public sealed class AuditActionOption(SecurityAuditAction? action, string displayName)
{
    public SecurityAuditAction? Action { get; } = action;
    public string DisplayName { get; } = displayName;
}

public sealed class AuditLogViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private string _actorFilter = string.Empty;
    private string _businessAreaFilter = string.Empty;
    private AuditActionOption? _selectedAction;
    private DateTime? _fromDate = DateTime.Today.AddDays(-6);
    private DateTime? _toDate = DateTime.Today;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private int _pageNumber = 1;
    private int _totalPages;
    private int _totalCount;
    private AuditLogRowViewModel? _selectedAudit;
    private AuditDetailsDto? _details;

    public AuditLogViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        ActionOptions = new([new(null, "Tất cả hành động"), .. Enum.GetValues<SecurityAuditAction>().Select(action => new AuditActionOption(action, ActionText(action)))]);
        SelectedAction = ActionOptions[0];
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(true), () => !IsBusy, HandleException);
        ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync, () => !IsBusy, HandleException);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => !IsBusy && PageNumber > 1, HandleException);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => !IsBusy && PageNumber < TotalPages, HandleException);
    }

    public ObservableCollection<AuditLogRowViewModel> Audits { get; } = [];
    public ObservableCollection<AuditActionOption> ActionOptions { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ClearFiltersCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public AuditLogRowViewModel? SelectedAudit { get => _selectedAudit; set { if (SetProperty(ref _selectedAudit, value)) _ = LoadDetailsAsync(); } }
    public AuditDetailsDto? Details { get => _details; private set => SetProperty(ref _details, value); }
    public string ActorFilter { get => _actorFilter; set => SetProperty(ref _actorFilter, value ?? string.Empty); }
    public string BusinessAreaFilter { get => _businessAreaFilter; set => SetProperty(ref _businessAreaFilter, value ?? string.Empty); }
    public AuditActionOption? SelectedAction { get => _selectedAction; set => SetProperty(ref _selectedAction, value); }
    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) { RefreshCommand.NotifyCanExecuteChanged(); ClearFiltersCommand.NotifyCanExecuteChanged(); PreviousPageCommand.NotifyCanExecuteChanged(); NextPageCommand.NotifyCanExecuteChanged(); } } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public int PageNumber { get => _pageNumber; private set => SetProperty(ref _pageNumber, value); }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) { NextPageCommand.NotifyCanExecuteChanged(); PreviousPageCommand.NotifyCanExecuteChanged(); } } }
    public int TotalCount { get => _totalCount; private set => SetProperty(ref _totalCount, value); }
    public string PageText => TotalCount == 0 ? "Không có hoạt động" : $"Trang {PageNumber}/{TotalPages} · {TotalCount:N0} hoạt động";

    public Task InitializeAsync() => LoadAsync(true);

    private async Task LoadAsync(bool resetPage)
    {
        if (resetPage) PageNumber = 1;
        IsBusy = true; StatusMessage = "Đang tải nhật ký hoạt động...";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var request = new AuditSearchRequest(ToUtc(FromDate, false), ToUtc(ToDate, true), ActorFilter, BusinessAreaFilter, SelectedAction?.Action, null, null, PageNumber, 25);
            var result = await service.SearchAsync(request);
            if (result.IsFailure) { StatusMessage = result.AppError.Message; return; }
            Audits.Clear(); foreach (var audit in result.Value.Items) Audits.Add(new AuditLogRowViewModel(audit));
            TotalCount = result.Value.TotalCount; TotalPages = result.Value.TotalPages; OnPropertyChanged(nameof(PageText));
            SelectedAudit = Audits.FirstOrDefault(); StatusMessage = TotalCount == 0 ? "Chưa có hoạt động nào được ghi nhận." : string.Empty;
        }
        finally { IsBusy = false; }
    }

    private async Task LoadDetailsAsync()
    {
        if (SelectedAudit is null) { Details = null; return; }
        using var scope = _scopeFactory.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAuditLogService>().GetDetailsAsync(SelectedAudit.Id);
        Details = result.IsSuccess ? result.Value : null;
    }

    private Task ChangePageAsync(int delta) { PageNumber += delta; return LoadAsync(false); }
    private async Task ClearFiltersAsync() { ActorFilter = string.Empty; BusinessAreaFilter = string.Empty; SelectedAction = ActionOptions[0]; FromDate = DateTime.Today.AddDays(-6); ToDate = DateTime.Today; await LoadAsync(true); }
    private void HandleException(Exception exception) { StatusMessage = "Không thể tải nhật ký hoạt động. Vui lòng thử lại."; global::System.Diagnostics.Trace.TraceError("AuditLogViewModel failed: {0}", exception.GetType().FullName); }
    private static DateTimeOffset? ToUtc(DateTime? value, bool endOfDay) => value is null ? null : new DateTimeOffset(value.Value.Date.AddDays(endOfDay ? 1 : 0), TimeSpan.Zero).AddTicks(endOfDay ? -1 : 0);
    private static string ActionText(SecurityAuditAction action) => new AuditLogRowViewModel(new AuditListItemDto(0, default, string.Empty, action, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty)).ActionText;
}
