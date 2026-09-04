using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.DTOs.Suppliers;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public sealed record SupplierStatusFilterOption(bool? IsActive, string DisplayName);

public sealed class SupplierListRowViewModel
{
    public SupplierListRowViewModel(SupplierListItemDto value)
    {
        Id = value.Id; Code = value.Code; Name = value.Name; TaxCode = value.TaxCode;
        ContactName = value.ContactName; PhoneNumber = value.PhoneNumber; IsActive = value.IsActive;
        CreatedAtUtc = value.CreatedAtUtc; UpdatedAtUtc = value.UpdatedAtUtc;
    }
    public int Id { get; }
    public string Code { get; }
    public string Name { get; }
    public string? TaxCode { get; }
    public string? ContactName { get; }
    public string? PhoneNumber { get; }
    public bool IsActive { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
    public string StatusText => IsActive ? "Đang hoạt động" : "Ngừng hoạt động";
    public string UpdatedAtText => UpdatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
}

public sealed class SupplierManagementViewModel : ViewModelBase
{
    private const int PageSize = 20;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISupplierDialogService _dialogService;
    private readonly IPermissionService _permissions;
    private readonly ILogger<SupplierManagementViewModel> _logger;
    private string _searchTerm = string.Empty;
    private SupplierStatusFilterOption? _selectedStatusFilter;
    private SupplierListRowViewModel? _selectedSupplier;
    private SupplierDetailsDto? _selectedDetails;
    private bool _isLoading;
    private bool _isError;
    private string _statusMessage = string.Empty;
    private int _pageNumber = 1;
    private int _totalPages;
    private int _totalCount;

    public SupplierManagementViewModel(IServiceScopeFactory scopeFactory, ISupplierDialogService dialogService, IPermissionService permissions, ILogger<SupplierManagementViewModel> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        StatusFilters = [new(null, "Tất cả trạng thái"), new(true, "Đang hoạt động"), new(false, "Ngừng hoạt động")];
        _selectedStatusFilter = StatusFilters[0];
        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsLoading, HandleCommandException);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading, HandleCommandException);
        AddCommand = new AsyncRelayCommand(AddAsync, () => CanManageSuppliers && !IsLoading, HandleCommandException);
        EditCommand = new AsyncRelayCommand(EditAsync, () => CanManageSuppliers && SelectedSupplier is not null && !IsLoading, HandleCommandException);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => CanManageSuppliers && SelectedSupplier is not null && !IsLoading, HandleCommandException);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => !IsLoading && PageNumber > 1, HandleCommandException);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => !IsLoading && PageNumber < TotalPages, HandleCommandException);
    }

    public ObservableCollection<SupplierListRowViewModel> Suppliers { get; } = [];
    public IReadOnlyList<SupplierStatusFilterOption> StatusFilters { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AddCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand ToggleActiveCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public string SearchTerm { get => _searchTerm; set => SetProperty(ref _searchTerm, value ?? string.Empty); }
    public SupplierStatusFilterOption? SelectedStatusFilter { get => _selectedStatusFilter; set => SetProperty(ref _selectedStatusFilter, value); }
    public SupplierListRowViewModel? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (!SetProperty(ref _selectedSupplier, value)) return;
            SelectedDetails = null;
            OnPropertyChanged(nameof(SelectedInitials));
            NotifyCommands();
            if (value is not null) _ = LoadDetailsAsync(value.Id);
        }
    }

    public SupplierDetailsDto? SelectedDetails
    {
        get => _selectedDetails;
        private set
        {
            if (!SetProperty(ref _selectedDetails, value)) return;
            OnPropertyChanged(nameof(HasSelectedDetails));
            OnPropertyChanged(nameof(SelectedStatusText));
            OnPropertyChanged(nameof(IsSelectedInactive));
            OnPropertyChanged(nameof(SelectedInitials));
            OnPropertyChanged(nameof(SelectedUpdatedAtText));
        }
    }
    public bool HasSelectedDetails => SelectedDetails is not null;
    public bool CanManageSuppliers => _permissions.HasPermission(SystemCapability.ManageSuppliers);
    public bool IsLoading { get => _isLoading; private set { if (SetProperty(ref _isLoading, value)) NotifyCommands(); } }
    public bool IsError { get => _isError; private set => SetProperty(ref _isError, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public int PageNumber => _pageNumber;
    public int TotalPages => _totalPages;
    public int TotalCount => _totalCount;
    public string PageText => _totalPages == 0 ? "Chưa có trang" : $"Trang {_pageNumber:N0} / {_totalPages:N0}";
    public string TotalCountText => _totalCount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    public string ToggleActiveText => SelectedSupplier?.IsActive == true ? "Ngừng hoạt động" : "Kích hoạt lại";
    public string SelectedStatusText => SelectedDetails?.IsActive == true ? "Đang hoạt động" : SelectedDetails is null ? "—" : "Ngừng hoạt động";
    public bool IsSelectedInactive => SelectedDetails is not null && !SelectedDetails.IsActive;
    public string SelectedInitials => BuildInitials(SelectedDetails?.Name ?? SelectedSupplier?.Name);
    public string SelectedUpdatedAtText => SelectedDetails is null
        ? string.Empty
        : $"Cập nhật: {SelectedDetails.UpdatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}";

    public Task InitializeAsync() => LoadPageAsync();
    private Task SearchAsync() { _pageNumber = 1; OnPageChanged(); return LoadPageAsync(); }
    private Task RefreshAsync() => LoadPageAsync(SelectedSupplier?.Id);
    private async Task AddAsync() { var id = await _dialogService.ShowCreateAsync(global::System.Windows.Application.Current?.MainWindow!); if (id.HasValue) await LoadPageAsync(id); }
    private async Task EditAsync() { if (SelectedSupplier is null) return; var id = SelectedSupplier.Id; if (await _dialogService.ShowEditAsync(global::System.Windows.Application.Current?.MainWindow!, id)) await LoadPageAsync(id); }
    private async Task ToggleActiveAsync()
    {
        if (SelectedSupplier is null || SelectedDetails is null) return;
        var id = SelectedSupplier.Id;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISupplierService>().SetActiveStateAsync(new SetSupplierActiveStateRequest(id, !SelectedDetails.IsActive, SelectedDetails.UpdatedAtUtc));
        if (result.IsFailure) { IsError = true; StatusMessage = result.AppError.Message; return; }
        await LoadPageAsync(id);
    }
    private async Task ChangePageAsync(int delta) { _pageNumber += delta; OnPageChanged(); await LoadPageAsync(SelectedSupplier?.Id); }
    private async Task LoadDetailsAsync(int id)
    {
        try { await using var scope = _scopeFactory.CreateAsyncScope(); var result = await scope.ServiceProvider.GetRequiredService<ISupplierService>().GetByIdAsync(id); if (result.IsSuccess) { SelectedDetails = result.Value; OnPropertyChanged(nameof(HasSelectedDetails)); } } catch { }
    }
    private async Task LoadPageAsync(int? preserveId = null)
    {
        IsLoading = true; IsError = false; StatusMessage = "Đang tải dữ liệu nhà cung cấp...";
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var request = new SupplierSearchRequest(SearchTerm, SelectedStatusFilter?.IsActive, PageNumber, PageSize);
            var result = await scope.ServiceProvider.GetRequiredService<ISupplierService>().SearchAsync(request);
            if (result.IsFailure) { IsError = true; StatusMessage = result.AppError.Message; return; }
            Suppliers.Clear(); foreach (var item in result.Value.Items) Suppliers.Add(new SupplierListRowViewModel(item));
            _totalCount = result.Value.TotalCount; _totalPages = result.Value.TotalPages;
            if (_totalPages > 0 && _pageNumber > _totalPages) { _pageNumber = _totalPages; OnPageChanged(); }
            SelectedSupplier = preserveId.HasValue ? Suppliers.FirstOrDefault(item => item.Id == preserveId.Value) : Suppliers.FirstOrDefault();
            StatusMessage = Suppliers.Count == 0 ? "Chưa có nhà cung cấp phù hợp." : $"Đã tải {Suppliers.Count:N0} nhà cung cấp.";
        }
        catch (Exception exception) { IsError = true; StatusMessage = "Không thể tải danh sách nhà cung cấp. Vui lòng thử lại."; POS.Application.Common.PosLog.Error(_logger, exception, "Supplier list failed."); }
        finally { IsLoading = false; OnPageChanged(); }
    }
    private void OnPageChanged() { OnPropertyChanged(nameof(PageNumber)); OnPropertyChanged(nameof(TotalPages)); OnPropertyChanged(nameof(PageText)); OnPropertyChanged(nameof(TotalCount)); OnPropertyChanged(nameof(TotalCountText)); NotifyCommands(); }
    private void NotifyCommands() { SearchCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged(); AddCommand.NotifyCanExecuteChanged(); EditCommand.NotifyCanExecuteChanged(); ToggleActiveCommand.NotifyCanExecuteChanged(); PreviousPageCommand.NotifyCanExecuteChanged(); NextPageCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(ToggleActiveText)); OnPropertyChanged(nameof(SelectedInitials)); }
    private void HandleCommandException(Exception exception) { IsError = true; StatusMessage = "Đã xảy ra lỗi. Vui lòng thử lại."; POS.Application.Common.PosLog.Error(_logger, exception, "Supplier UI command failed."); }

    private static string BuildInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "NCC";

        var ignoredWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "nhà", "cung", "cấp" };
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => !ignoredWords.Contains(word))
            .ToArray();
        if (words.Length == 0) return "NCC";
        if (words.Length > 1) return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));

        var word = words[0];
        var uppercaseLetters = word.Where(char.IsUpper).Take(2).ToArray();
        if (uppercaseLetters.Length == 2) return new string(uppercaseLetters);
        return word.Length == 1 ? word.ToUpperInvariant() : word[..2].ToUpperInvariant();
    }
}
