using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Common;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Audit;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

public sealed class AuditLogRowViewModel
{
    public AuditLogRowViewModel(AuditListItemDto dto)
    {
        Id = dto.Id; OccurredAtUtc = dto.OccurredAtUtc; Actor = dto.Actor; Action = dto.Action;
        BusinessArea = dto.BusinessArea; Target = dto.Target; Result = dto.Result; TerminalId = dto.TerminalId; OperationId = dto.OperationId;
        TechnicalTarget = dto.TechnicalTarget;
    }
    public int Id { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public string LocalTimeText => OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("vi-VN"));
    public string Actor { get; }
    public SecurityAuditAction Action { get; }
    public string ActionText => AuditPresentationResolver.ActionText(Action);
    public string BusinessArea { get; }
    public string Target { get; }
    public string Result { get; }
    public string ResultText => AuditPresentationResolver.ResultText(Result);
    public string TerminalId { get; }
    public Guid OperationId { get; }
    public string TechnicalTarget { get; }
}

public sealed class AuditActionOption(SecurityAuditAction? action, string displayName)
{
    public SecurityAuditAction? Action { get; } = action;
    public string DisplayName { get; } = displayName;
}

public sealed class AuditLogViewModel : ViewModelBase, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogViewModel>? _logger;
    private CancellationTokenSource? _loadSource;
    private CancellationTokenSource? _detailSource;
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
    private bool _hasLoaded;
    private bool _hasError;

    public AuditLogViewModel(IServiceScopeFactory scopeFactory, ILogger<AuditLogViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger;
        ActionOptions = new([new(null, "Tất cả hành động"), .. Enum.GetValues<SecurityAuditAction>().Select(action => new AuditActionOption(action, ActionText(action)))]);
        SelectedAction = ActionOptions[0];
        SearchCommand = new AsyncRelayCommand(() => LoadAsync(true), () => !IsBusy, HandleException);
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(true), () => !IsBusy, HandleException);
        RetryCommand = new AsyncRelayCommand(() => LoadAsync(true), () => !IsBusy, HandleException);
        ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync, () => !IsBusy, HandleException);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => !IsBusy && PageNumber > 1, HandleException);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => !IsBusy && PageNumber < TotalPages, HandleException);
    }

    public ObservableCollection<AuditLogRowViewModel> Audits { get; } = [];
    public ObservableCollection<AuditActionOption> ActionOptions { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand RetryCommand { get; }
    public AsyncRelayCommand ClearFiltersCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public AuditLogRowViewModel? SelectedAudit
    {
        get => _selectedAudit;
        set
        {
            if (!SetProperty(ref _selectedAudit, value))
                return;

            OnPropertyChanged(nameof(IsNoSelection));
            _ = LoadDetailsAsync(value);
        }
    }
    public AuditDetailsDto? Details
    {
        get => _details;
        private set
        {
            if (SetProperty(ref _details, value))
                OnPropertyChanged(nameof(HasDetails));
        }
    }
    public string ActorFilter { get => _actorFilter; set { if (SetProperty(ref _actorFilter, value ?? string.Empty)) NotifyFilterState(); } }
    public string BusinessAreaFilter { get => _businessAreaFilter; set { if (SetProperty(ref _businessAreaFilter, value ?? string.Empty)) NotifyFilterState(); } }
    public AuditActionOption? SelectedAction { get => _selectedAction; set { if (SetProperty(ref _selectedAction, value)) NotifyFilterState(); } }
    public DateTime? FromDate { get => _fromDate; set { if (SetProperty(ref _fromDate, value)) NotifyFilterState(); } }
    public DateTime? ToDate { get => _toDate; set { if (SetProperty(ref _toDate, value)) NotifyFilterState(); } }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) { OnPropertyChanged(nameof(IsLoadingState)); NotifyCommands(); } } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public int PageNumber { get => _pageNumber; private set { if (SetProperty(ref _pageNumber, value)) OnPropertyChanged(nameof(PageText)); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) { NextPageCommand.NotifyCanExecuteChanged(); PreviousPageCommand.NotifyCanExecuteChanged(); } } }
    public int TotalCount { get => _totalCount; private set { if (SetProperty(ref _totalCount, value)) { OnPropertyChanged(nameof(PageText)); NotifyState(); } } }
    public string PageText => TotalCount == 0 ? "Không có hoạt động" : $"Trang {PageNumber}/{TotalPages} · {TotalCount:N0} hoạt động";
    public bool IsLoadingState => IsBusy;
    public bool HasError => _hasError;
    public bool IsDatabaseEmpty => _hasLoaded && !_hasError && TotalCount == 0 && !HasActiveFilters;
    public bool IsFilteredNoResult => _hasLoaded && !_hasError && TotalCount == 0 && HasActiveFilters;
    public bool IsNoSelection => _hasLoaded && !_hasError && SelectedAudit is null;
    public bool HasDetails => Details is not null;
    public bool HasActiveFilters => !string.IsNullOrWhiteSpace(ActorFilter)
        || !string.IsNullOrWhiteSpace(BusinessAreaFilter)
        || SelectedAction?.Action is not null
        || FromDate?.Date != DefaultFromDate
        || ToDate?.Date != DateTime.Today;

    public Task InitializeAsync() => LoadAsync(true);

    private async Task LoadAsync(bool resetPage)
    {
        if (!TryBuildRequest(out var request))
            return;

        if (resetPage) PageNumber = 1;
        var source = ReplaceSource(ref _loadSource);
        IsBusy = true;
        _hasError = false;
        _hasLoaded = false;
        StatusMessage = "Đang tải nhật ký hoạt động...";
        Audits.Clear();
        TotalCount = 0;
        TotalPages = 0;
        SelectedAudit = null;
        Details = null;
        NotifyState();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var result = await service.SearchAsync(request, source.Token);
            if (source.IsCancellationRequested)
                return;
            if (result.IsFailure)
            {
                SetFailure();
                return;
            }

            Audits.Clear(); foreach (var audit in result.Value.Items) Audits.Add(new AuditLogRowViewModel(audit));
            TotalCount = result.Value.TotalCount;
            TotalPages = result.Value.TotalPages;
            _hasLoaded = true;
            StatusMessage = string.Empty;
            NotifyState();
            SelectedAudit = Audits.FirstOrDefault();
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetFailure(exception);
        }
        finally
        {
            if (ReferenceEquals(_loadSource, source))
                IsBusy = false;
        }
    }

    private async Task LoadDetailsAsync(AuditLogRowViewModel? selection)
    {
        _detailSource?.Cancel();
        _detailSource?.Dispose();
        _detailSource = new CancellationTokenSource();
        var source = _detailSource;
        if (selection is null)
        {
            Details = null;
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IAuditLogService>().GetDetailsAsync(selection.Id, source.Token);
            if (source.IsCancellationRequested || !ReferenceEquals(_selectedAudit, selection))
                return;
            Details = result.IsSuccess ? result.Value : null;
        }
        catch (Exception exception)
        {
            Details = null;
            if (!source.IsCancellationRequested)
                SetFailure(exception);
        }
    }

    private Task ChangePageAsync(int delta) { PageNumber += delta; return LoadAsync(false); }
    private async Task ClearFiltersAsync() { ActorFilter = string.Empty; BusinessAreaFilter = string.Empty; SelectedAction = ActionOptions[0]; FromDate = DefaultFromDate; ToDate = DateTime.Today; await LoadAsync(true); }
    private bool TryBuildRequest(out AuditSearchRequest request)
    {
        if (FromDate?.Date > ToDate?.Date)
        {
            request = new AuditSearchRequest();
            SetFailure("Từ ngày phải nhỏ hơn hoặc bằng Đến ngày. Vui lòng chọn lại khoảng thời gian.");
            return false;
        }

        request = new AuditSearchRequest(ToUtc(FromDate, false), ToUtc(ToDate, true), ActorFilter, BusinessAreaFilter, SelectedAction?.Action, null, null, PageNumber, 25);
        return true;
    }

    private void SetFailure(Exception exception) => SetFailure("Không thể tải nhật ký hoạt động. Vui lòng thử lại.", exception);

    private void SetFailure() => SetFailure("Không thể tải nhật ký hoạt động. Vui lòng thử lại.");

    private void SetFailure(string message, Exception? exception = null)
    {
        _hasError = true;
        _hasLoaded = false;
        StatusMessage = message;
        Audits.Clear();
        TotalCount = 0;
        TotalPages = 0;
        SelectedAudit = null;
        Details = null;
        NotifyState();
        if (exception is null)
            return;

        var chain = FormatExceptionChain(exception);
        if (_logger is not null)
            PosLog.Error(_logger, exception, "Không thể tải nhật ký hoạt động. ExceptionChain={ExceptionChain}", chain);
        else
            global::System.Diagnostics.Trace.TraceError("AuditLogViewModel failed. ExceptionChain={0}", chain);
    }

    private void HandleException(Exception exception) => SetFailure(exception);

    private void NotifyFilterState()
    {
        OnPropertyChanged(nameof(HasActiveFilters));
        NotifyState();
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(IsDatabaseEmpty));
        OnPropertyChanged(nameof(IsFilteredNoResult));
        OnPropertyChanged(nameof(IsNoSelection));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(PageText));
    }

    private void NotifyCommands()
    {
        SearchCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        ClearFiltersCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private static string FormatExceptionChain(Exception exception)
    {
        var parts = new List<string>();
        var current = exception;
        var depth = 0;
        while (current is not null && depth++ < 8)
        {
            parts.Add($"{current.GetType().FullName}: {SafeDiagnosticPolicy.SanitizeText(current.Message)}");
            current = current.InnerException!;
        }

        return string.Join(" <- ", parts);
    }

    private static CancellationTokenSource ReplaceSource(ref CancellationTokenSource? source)
    {
        source?.Cancel();
        source?.Dispose();
        source = new CancellationTokenSource();
        return source;
    }
    private static DateTime DefaultFromDate => DateTime.Today.AddDays(-6);
    private static DateTimeOffset? ToUtc(DateTime? value, bool endOfDay)
    {
        if (value is null)
            return null;

        var local = DateTime.SpecifyKind(value.Value.Date.AddDays(endOfDay ? 1 : 0).AddTicks(endOfDay ? -1 : 0), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.Local));
    }
    private static string ActionText(SecurityAuditAction action) => AuditPresentationResolver.ActionText(action);

    public void Dispose()
    {
        _loadSource?.Cancel();
        _loadSource?.Dispose();
        _loadSource = null;
        _detailSource?.Cancel();
        _detailSource?.Dispose();
        _detailSource = null;
    }
}
