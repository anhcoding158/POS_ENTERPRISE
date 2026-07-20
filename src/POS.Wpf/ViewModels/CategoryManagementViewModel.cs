using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Categories;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

/// <summary>
/// Một lựa chọn lọc trạng thái danh mục.
/// </summary>
public sealed record CategoryStatusFilterOption(
    bool? IsActive,
    string DisplayName);

/// <summary>
/// Dữ liệu trình bày một dòng danh mục.
///
/// Tách khỏi DTO Application để WPF có thể bổ sung
/// chuỗi hiển thị mà không làm thay đổi contract nghiệp vụ.
/// </summary>
public sealed class CategoryListRowViewModel
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    public CategoryListRowViewModel(
        CategoryListItemDto category)
    {
        ArgumentNullException.ThrowIfNull(
            category);

        Id =
            category.Id;

        Name =
            category.Name;

        Description =
            category.Description;

        DisplayOrder =
            category.DisplayOrder;

        IsActive =
            category.IsActive;

        CreatedAtUtc =
            category.CreatedAtUtc;

        UpdatedAtUtc =
            category.UpdatedAtUtc;
    }

    public int Id { get; }

    public string Name { get; }

    public string? Description { get; }

    public string DescriptionText =>
        string.IsNullOrWhiteSpace(
            Description)
            ? "Không có mô tả"
            : Description;

    public int DisplayOrder { get; }

    public string DisplayOrderText =>
        DisplayOrder.ToString(
            "N0",
            VietnameseCulture);

    public bool IsActive { get; }

    public string StatusText =>
        IsActive
            ? "Đang hoạt động"
            : "Ngừng hoạt động";

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public string UpdatedAtText =>
        UpdatedAtUtc
            .ToLocalTime()
            .ToString(
                "dd/MM/yyyy HH:mm",
                VietnameseCulture);
}

/// <summary>
/// ViewModel cho màn hình quản lý danh mục.
///
/// Mỗi thao tác dữ liệu tạo một DI scope riêng.
/// DbContext không sống cùng cửa sổ WPF.
/// </summary>
public sealed class CategoryManagementViewModel :
    ViewModelBase
{
    private const int PageSize = 20;

    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ICategoryDialogService
        _categoryDialogService;

    private readonly ILogger<
        CategoryManagementViewModel>
        _logger;

    private string _searchTerm =
        string.Empty;

    private CategoryStatusFilterOption?
        _selectedStatusFilter;

    private CategoryListRowViewModel?
        _selectedCategory;

    private bool _isBusy;

    private bool _isStatusError;

    private string _statusMessage =
        string.Empty;

    private string _lastUpdatedText =
        "Chưa tải dữ liệu";

    private int _pageNumber = 1;

    private int _totalPages = 1;

    private int _totalCount;

    private int _activeOnPage;

    private int _inactiveOnPage;

    public CategoryManagementViewModel(
        IServiceScopeFactory scopeFactory,
        ICategoryDialogService categoryDialogService,
        ILogger<CategoryManagementViewModel> logger)
    {
        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

        _categoryDialogService =
            categoryDialogService ??
            throw new ArgumentNullException(
                nameof(categoryDialogService));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        SearchCommand =
            new AsyncRelayCommand(
                SearchAsync,
                CanExecuteGeneralCommand,
                HandleCommandException);

        ResetFiltersCommand =
            new AsyncRelayCommand(
                ResetFiltersAsync,
                CanExecuteGeneralCommand,
                HandleCommandException);

        RefreshCommand =
            new AsyncRelayCommand(
                RefreshAsync,
                CanExecuteGeneralCommand,
                HandleCommandException);

        AddCommand =
            new AsyncRelayCommand(
                AddAsync,
                CanExecuteGeneralCommand,
                HandleCommandException);

        EditCommand =
            new AsyncRelayCommand(
                EditAsync,
                CanExecuteSelectedCommand,
                HandleCommandException);

        ToggleActiveCommand =
            new AsyncRelayCommand(
                ToggleActiveAsync,
                CanExecuteSelectedCommand,
                HandleCommandException);

        PreviousPageCommand =
            new AsyncRelayCommand(
                PreviousPageAsync,
                CanGoToPreviousPage,
                HandleCommandException);

        NextPageCommand =
            new AsyncRelayCommand(
                NextPageAsync,
                CanGoToNextPage,
                HandleCommandException);
    }

    public ObservableCollection<
        CategoryListRowViewModel>
        Categories
    { get; } = [];

    public ObservableCollection<
        CategoryStatusFilterOption>
        StatusFilters
    { get; } = [];

    public AsyncRelayCommand SearchCommand { get; }

    public AsyncRelayCommand ResetFiltersCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand AddCommand { get; }

    public AsyncRelayCommand EditCommand { get; }

    public AsyncRelayCommand ToggleActiveCommand { get; }

    public AsyncRelayCommand PreviousPageCommand { get; }

    public AsyncRelayCommand NextPageCommand { get; }

    public string SearchTerm
    {
        get => _searchTerm;

        set => SetProperty(
            ref _searchTerm,
            value ?? string.Empty);
    }

    public CategoryStatusFilterOption?
        SelectedStatusFilter
    {
        get => _selectedStatusFilter;

        set => SetProperty(
            ref _selectedStatusFilter,
            value);
    }

    public CategoryListRowViewModel?
        SelectedCategory
    {
        get => _selectedCategory;

        set
        {
            if (!SetProperty(
                    ref _selectedCategory,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(SelectedCategoryTitle));

            OnPropertyChanged(
                nameof(SelectedCategoryDescription));

            OnPropertyChanged(
                nameof(ToggleActiveButtonText));

            NotifyCommandStates();
        }
    }

    public string SelectedCategoryTitle =>
        SelectedCategory is null
            ? "Chưa chọn danh mục"
            : $"{SelectedCategory.Name} • " +
              $"Thứ tự {SelectedCategory.DisplayOrderText}";

    public string SelectedCategoryDescription =>
        SelectedCategory is null
            ? "Chọn một dòng để sửa hoặc thay đổi trạng thái."
            : SelectedCategory.DescriptionText;

    public string ToggleActiveButtonText =>
        SelectedCategory?.IsActive == true
            ? "Ngừng hoạt động"
            : "Kích hoạt";

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

            NotifyCommandStates();
        }
    }

    public bool IsStatusError
    {
        get => _isStatusError;

        private set => SetProperty(
            ref _isStatusError,
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

    public string LastUpdatedText
    {
        get => _lastUpdatedText;

        private set => SetProperty(
            ref _lastUpdatedText,
            value);
    }

    public int PageNumber =>
        _pageNumber;

    public int TotalPages =>
        _totalPages;

    public int TotalCount =>
        _totalCount;

    public string PageText =>
        $"Trang {_pageNumber:N0} / " +
        $"{_totalPages:N0}";

    public string TotalCountText =>
        _totalCount.ToString(
            "N0",
            VietnameseCulture);

    public string ActiveOnPageText =>
        _activeOnPage.ToString(
            "N0",
            VietnameseCulture);

    public string InactiveOnPageText =>
        _inactiveOnPage.ToString(
            "N0",
            VietnameseCulture);

    public async Task InitializeAsync()
    {
        if (StatusFilters.Count == 0)
        {
            StatusFilters.Add(
                new CategoryStatusFilterOption(
                    IsActive: null,
                    DisplayName:
                        "Tất cả trạng thái"));

            StatusFilters.Add(
                new CategoryStatusFilterOption(
                    IsActive: true,
                    DisplayName:
                        "Đang hoạt động"));

            StatusFilters.Add(
                new CategoryStatusFilterOption(
                    IsActive: false,
                    DisplayName:
                        "Ngừng hoạt động"));
        }

        SelectedStatusFilter ??=
            StatusFilters.First();

        _pageNumber = 1;

        await LoadPageAsync();
    }

    private async Task SearchAsync()
    {
        _pageNumber = 1;

        await LoadPageAsync();
    }

    private async Task ResetFiltersAsync()
    {
        SearchTerm =
            string.Empty;

        SelectedStatusFilter =
            StatusFilters.FirstOrDefault();

        _pageNumber = 1;

        await LoadPageAsync();
    }

    private Task RefreshAsync()
    {
        return LoadPageAsync(
            SelectedCategory?.Id);
    }

    private async Task AddAsync()
    {
        var saved =
            await _categoryDialogService
                .ShowCreateAsync();

        if (!saved)
        {
            return;
        }

        _pageNumber = 1;

        await LoadPageAsync();
    }

    private async Task EditAsync()
    {
        var selected =
            SelectedCategory;

        if (selected is null)
        {
            return;
        }

        var saved =
            await _categoryDialogService
                .ShowEditAsync(
                    selected.Id);

        if (!saved)
        {
            return;
        }

        await LoadPageAsync(
            selected.Id);
    }

    private async Task ToggleActiveAsync()
    {
        var selected =
            SelectedCategory;

        if (selected is null)
        {
            return;
        }

        IsBusy = true;
        IsStatusError = false;

        StatusMessage =
            selected.IsActive
                ? "Đang ngừng hoạt động danh mục..."
                : "Đang kích hoạt danh mục...";

        try
        {
            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var categoryService =
                scope.ServiceProvider
                    .GetRequiredService<
                        ICategoryService>();

            var result =
                await categoryService
                    .SetActiveStateAsync(
                        selected.Id,
                        isActive:
                            !selected.IsActive);

            if (result.IsFailure)
            {
                ShowError(
                    result.Error.Message);

                return;
            }

            await LoadPageCoreAsync(
                preferredCategoryId:
                    selected.Id);

            IsStatusError = false;

            StatusMessage =
                selected.IsActive
                    ? "Đã ngừng hoạt động danh mục."
                    : "Đã kích hoạt danh mục.";
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể thay đổi trạng thái Category {CategoryId}.",
                selected.Id);

            ShowError(
                "Không thể thay đổi trạng thái danh mục. " +
                exception
                    .GetBaseException()
                    .Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PreviousPageAsync()
    {
        if (_pageNumber <= 1)
        {
            return;
        }

        _pageNumber--;

        await LoadPageAsync();
    }

    private async Task NextPageAsync()
    {
        if (_pageNumber >=
            _totalPages)
        {
            return;
        }

        _pageNumber++;

        await LoadPageAsync();
    }

    private async Task LoadPageAsync(
        int? preferredCategoryId = null)
    {
        IsBusy = true;
        IsStatusError = false;

        StatusMessage =
            "Đang tải danh mục...";

        try
        {
            await LoadPageCoreAsync(
                preferredCategoryId);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể tải danh sách Category.");

            ShowError(
                "Không thể tải danh sách danh mục. " +
                exception
                    .GetBaseException()
                    .Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadPageCoreAsync(
        int? preferredCategoryId)
    {
        await using var scope =
            _scopeFactory
                .CreateAsyncScope();

        var categoryService =
            scope.ServiceProvider
                .GetRequiredService<
                    ICategoryService>();

        var request =
            CreateSearchRequest();

        var result =
            await categoryService
                .SearchAsync(
                    request);

        /*
         * Khi đang ở trang cuối và bộ lọc làm số trang giảm,
         * lùi về một trang rồi tải lại đúng một lần.
         */
        if (result.IsSuccess &&
            result.Value.Items.Count == 0 &&
            result.Value.TotalCount > 0 &&
            _pageNumber > 1)
        {
            _pageNumber--;

            result =
                await categoryService
                    .SearchAsync(
                        CreateSearchRequest());
        }

        if (result.IsFailure)
        {
            ShowError(
                result.Error.Message);

            return;
        }

        var page =
            result.Value;

        Categories.Clear();

        foreach (var category
                 in page.Items)
        {
            Categories.Add(
                new CategoryListRowViewModel(
                    category));
        }

        _pageNumber =
            page.PageNumber;

        _totalCount =
            page.TotalCount;

        _totalPages =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    page.TotalCount /
                    (double)PageSize));

        _activeOnPage =
            Categories.Count(
                category =>
                    category.IsActive);

        _inactiveOnPage =
            Categories.Count -
            _activeOnPage;

        SelectedCategory =
            preferredCategoryId.HasValue
                ? Categories.FirstOrDefault(
                    category =>
                        category.Id ==
                        preferredCategoryId.Value)
                  ?? Categories.FirstOrDefault()
                : Categories.FirstOrDefault();

        LastUpdatedText =
            $"Cập nhật lúc " +
            $"{DateTimeOffset.Now:HH:mm:ss}";

        IsStatusError = false;

        StatusMessage =
            page.TotalCount == 0
                ? "Không có danh mục phù hợp với bộ lọc."
                : $"Đã tải " +
                  $"{page.Items.Count:N0} / " +
                  $"{page.TotalCount:N0} danh mục.";

        NotifyPaginationPresentation();
    }

    private CategorySearchRequest
        CreateSearchRequest()
    {
        return new CategorySearchRequest(
            searchTerm:
                SearchTerm,

            isActive:
                SelectedStatusFilter?
                    .IsActive,

            pageNumber:
                _pageNumber,

            pageSize:
                PageSize);
    }

    private void ShowError(
        string message)
    {
        IsStatusError = true;
        StatusMessage = message;
    }

    private bool CanExecuteGeneralCommand()
    {
        return !IsBusy;
    }

    private bool CanExecuteSelectedCommand()
    {
        return
            !IsBusy &&
            SelectedCategory is not null;
    }

    private bool CanGoToPreviousPage()
    {
        return
            !IsBusy &&
            _pageNumber > 1;
    }

    private bool CanGoToNextPage()
    {
        return
            !IsBusy &&
            _pageNumber <
            _totalPages;
    }

    private void HandleCommandException(
        Exception exception)
    {
        _logger.LogError(
            exception,
            "Lệnh CategoryManagement thất bại.");

        ShowError(
            "Thao tác không thể hoàn thành. " +
            exception
                .GetBaseException()
                .Message);
    }

    private void NotifyPaginationPresentation()
    {
        OnPropertyChanged(
            nameof(PageNumber));

        OnPropertyChanged(
            nameof(TotalPages));

        OnPropertyChanged(
            nameof(TotalCount));

        OnPropertyChanged(
            nameof(PageText));

        OnPropertyChanged(
            nameof(TotalCountText));

        OnPropertyChanged(
            nameof(ActiveOnPageText));

        OnPropertyChanged(
            nameof(InactiveOnPageText));

        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        SearchCommand
            .NotifyCanExecuteChanged();

        ResetFiltersCommand
            .NotifyCanExecuteChanged();

        RefreshCommand
            .NotifyCanExecuteChanged();

        AddCommand
            .NotifyCanExecuteChanged();

        EditCommand
            .NotifyCanExecuteChanged();

        ToggleActiveCommand
            .NotifyCanExecuteChanged();

        PreviousPageCommand
            .NotifyCanExecuteChanged();

        NextPageCommand
            .NotifyCanExecuteChanged();
    }
}