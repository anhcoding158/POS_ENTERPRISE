using System.Collections;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Categories;
using POS.Domain.Constants;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// ViewModel cho cửa sổ thêm và chỉnh sửa danh mục.
///
/// Mỗi thao tác tải hoặc lưu sử dụng một DI scope riêng,
/// vì vậy DbContext không sống cùng cửa sổ WPF.
/// </summary>
public sealed class CategoryEditorViewModel :
    ViewModelBase,
    INotifyDataErrorInfo
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        CategoryEditorViewModel>
        _logger;

    private readonly Dictionary<
        string,
        List<string>>
        _errors =
            new(
                StringComparer.Ordinal);

    private int? _categoryId;

    private string _name =
        string.Empty;

    private string _description =
        string.Empty;

    private string _displayOrderText =
        "0";

    private bool _isActive = true;
    private bool _isBusy;

    private string _statusMessage =
        string.Empty;

    private bool _isStatusError;

    private bool _suppressValidation;

    public CategoryEditorViewModel(
        IServiceScopeFactory scopeFactory,
        ILogger<CategoryEditorViewModel> logger)
    {
        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        SaveCommand =
            new AsyncRelayCommand(
                SaveAsync,
                CanExecuteCommand,
                HandleCommandException);

        CancelCommand =
            new AsyncRelayCommand(
                CancelAsync,
                CanExecuteCommand,
                HandleCommandException);
    }

    public event EventHandler<
        DataErrorsChangedEventArgs>?
        ErrorsChanged;

    public event Action<bool?>?
        RequestClose;

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand CancelCommand { get; }

    public int? CategoryId
    {
        get => _categoryId;

        private set
        {
            if (!SetProperty(
                    ref _categoryId,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(IsEditMode));

            OnPropertyChanged(
                nameof(WindowTitle));

            OnPropertyChanged(
                nameof(HeaderTitle));

            OnPropertyChanged(
                nameof(HeaderDescription));

            OnPropertyChanged(
                nameof(SaveButtonText));

            OnPropertyChanged(
                nameof(ModeBadgeText));
        }
    }

    public bool IsEditMode =>
        CategoryId.HasValue;

    public string WindowTitle =>
        IsEditMode
            ? "Chỉnh sửa danh mục"
            : "Thêm danh mục";

    public string HeaderTitle =>
        IsEditMode
            ? "Cập nhật danh mục"
            : "Tạo danh mục mới";

    public string HeaderDescription =>
        IsEditMode
            ? "Cập nhật tên, mô tả, thứ tự hiển thị và trạng thái."
            : "Tạo nhóm sản phẩm để sắp xếp thực đơn và màn hình bán hàng.";

    public string SaveButtonText =>
        IsEditMode
            ? "Lưu thay đổi"
            : "Tạo danh mục";

    public string ModeBadgeText =>
        IsEditMode
            ? "EDIT CATEGORY"
            : "NEW CATEGORY";

    public string Name
    {
        get => _name;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _name,
                    normalized))
            {
                return;
            }

            ValidateWhenEnabled(
                ValidateName);
        }
    }

    public string Description
    {
        get => _description;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _description,
                    normalized))
            {
                return;
            }

            OnPropertyChanged(
                nameof(DescriptionLengthText));

            ValidateWhenEnabled(
                ValidateDescription);
        }
    }

    public string DescriptionLengthText =>
        $"{Description.Length:N0} / " +
        $"{BusinessRules.Categories.DescriptionMaxLength:N0}";

    public string DisplayOrderText
    {
        get => _displayOrderText;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _displayOrderText,
                    normalized))
            {
                return;
            }

            ValidateWhenEnabled(
                ValidateDisplayOrder);
        }
    }

    public bool IsActive
    {
        get => _isActive;

        set => SetProperty(
            ref _isActive,
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

            SaveCommand
                .NotifyCanExecuteChanged();

            CancelCommand
                .NotifyCanExecuteChanged();
        }
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

    public bool HasErrors =>
        _errors.Count > 0;

    public string? NameError =>
        GetFirstError(
            nameof(Name));

    public string? DescriptionError =>
        GetFirstError(
            nameof(Description));

    public string? DisplayOrderError =>
        GetFirstError(
            nameof(DisplayOrderText));

    public IEnumerable GetErrors(
        string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(
                propertyName))
        {
            return _errors
                .Values
                .SelectMany(
                    errors =>
                        errors)
                .ToArray();
        }

        return _errors.TryGetValue(
            propertyName,
            out var propertyErrors)
                ? propertyErrors
                : Array.Empty<string>();
    }

    public async Task InitializeAsync(
        int? categoryId)
    {
        _suppressValidation = true;

        ClearAllErrors();

        CategoryId =
            categoryId;

        IsBusy = true;
        IsStatusError = false;

        StatusMessage =
            IsEditMode
                ? "Đang tải dữ liệu danh mục..."
                : string.Empty;

        try
        {
            if (!IsEditMode)
            {
                Name =
                    string.Empty;

                Description =
                    string.Empty;

                DisplayOrderText =
                    "0";

                IsActive =
                    true;

                return;
            }

            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var categoryService =
                scope.ServiceProvider
                    .GetRequiredService<
                        ICategoryService>();

            var result =
                await categoryService
                    .GetByIdAsync(
                        CategoryId!.Value);

            if (result.IsFailure)
            {
                ShowError(
                    result.Error.Message);

                return;
            }

            ApplyCategory(
                result.Value);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể khởi tạo CategoryEditor.");

            ShowError(
                "Không thể tải danh mục. " +
                exception
                    .GetBaseException()
                    .Message);
        }
        finally
        {
            _suppressValidation = false;

            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        ValidateAll();

        if (HasErrors)
        {
            ShowError(
                "Vui lòng kiểm tra lại những trường " +
                "đang được đánh dấu.");

            return;
        }

        if (!TryParseDisplayOrder(
                DisplayOrderText,
                out var displayOrder))
        {
            ShowError(
                "Thứ tự hiển thị không hợp lệ.");

            return;
        }

        IsBusy = true;
        IsStatusError = false;

        StatusMessage =
            IsEditMode
                ? "Đang lưu thay đổi..."
                : "Đang tạo danh mục...";

        try
        {
            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var categoryService =
                scope.ServiceProvider
                    .GetRequiredService<
                        ICategoryService>();

            Result<CategoryDetailsDto> result;

            if (IsEditMode)
            {
                result =
                    await categoryService
                        .UpdateAsync(
                            new UpdateCategoryRequest(
                                categoryId:
                                    CategoryId!.Value,

                                name:
                                    Name,

                                displayOrder:
                                    displayOrder,

                                isActive:
                                    IsActive,

                                description:
                                    Description));
            }
            else
            {
                result =
                    await categoryService
                        .CreateAsync(
                            new CreateCategoryRequest(
                                name:
                                    Name,

                                displayOrder:
                                    displayOrder,

                                description:
                                    Description));
            }

            if (result.IsFailure)
            {
                ApplyServiceError(
                    result.Error);

                return;
            }

            IsStatusError = false;

            StatusMessage =
                IsEditMode
                    ? "Đã cập nhật danh mục."
                    : "Đã tạo danh mục.";

            RequestClose?.Invoke(
                true);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể lưu Category.");

            ShowError(
                "Không thể lưu danh mục. " +
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

    private void ApplyCategory(
        CategoryDetailsDto category)
    {
        Name =
            category.Name;

        Description =
            category.Description ??
            string.Empty;

        DisplayOrderText =
            category.DisplayOrder.ToString(
                CultureInfo.InvariantCulture);

        IsActive =
            category.IsActive;

        IsStatusError = false;
        StatusMessage = string.Empty;

        ClearAllErrors();
    }

    private void ValidateAll()
    {
        ValidateName();
        ValidateDescription();
        ValidateDisplayOrder();
    }

    private void ValidateName()
    {
        var normalized =
            Name.Trim();

        string? message =
            string.IsNullOrWhiteSpace(
                normalized)
                ? "Tên danh mục không được để trống."
                : normalized.Length >
                  BusinessRules.Categories
                      .NameMaxLength
                    ? $"Tên danh mục tối đa " +
                      $"{BusinessRules.Categories.NameMaxLength} ký tự."
                    : null;

        SetError(
            nameof(Name),
            message);
    }

    private void ValidateDescription()
    {
        var normalized =
            Description.Trim();

        var message =
            normalized.Length >
            BusinessRules.Categories
                .DescriptionMaxLength
                ? $"Mô tả tối đa " +
                  $"{BusinessRules.Categories.DescriptionMaxLength} ký tự."
                : null;

        SetError(
            nameof(Description),
            message);
    }

    private void ValidateDisplayOrder()
    {
        string? message;

        if (!TryParseDisplayOrder(
                DisplayOrderText,
                out var value))
        {
            message =
                "Thứ tự hiển thị phải là số nguyên.";
        }
        else if (value < 0)
        {
            message =
                "Thứ tự hiển thị không được âm.";
        }
        else if (value >
                 BusinessRules.Categories
                     .MaximumDisplayOrder)
        {
            message =
                "Thứ tự hiển thị vượt quá giới hạn hệ thống.";
        }
        else
        {
            message = null;
        }

        SetError(
            nameof(DisplayOrderText),
            message);
    }

    private void ApplyServiceError(
        Error error)
    {
        ShowError(
            error.Message);

        if (string.Equals(
                error.Code,
                ErrorCodes.Categories
                    .NameAlreadyExists,
                StringComparison.Ordinal))
        {
            SetError(
                nameof(Name),
                error.Message);
        }
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
            "Lệnh CategoryEditor thất bại.");

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

    private void ValidateWhenEnabled(
        Action validationAction)
    {
        if (_suppressValidation)
        {
            return;
        }

        validationAction();
    }

    private void SetError(
        string propertyName,
        string? error)
    {
        var existingError =
            GetFirstError(
                propertyName);

        if (string.IsNullOrWhiteSpace(
                error))
        {
            if (!_errors.Remove(
                    propertyName))
            {
                return;
            }
        }
        else
        {
            if (string.Equals(
                    existingError,
                    error,
                    StringComparison.Ordinal))
            {
                return;
            }

            _errors[propertyName] =
            [
                error
            ];
        }

        ErrorsChanged?.Invoke(
            this,
            new DataErrorsChangedEventArgs(
                propertyName));

        OnPropertyChanged(
            nameof(HasErrors));

        NotifyErrorProperty(
            propertyName);
    }

    private void ClearAllErrors()
    {
        if (_errors.Count == 0)
        {
            return;
        }

        var propertyNames =
            _errors.Keys
                .ToArray();

        _errors.Clear();

        foreach (var propertyName
                 in propertyNames)
        {
            ErrorsChanged?.Invoke(
                this,
                new DataErrorsChangedEventArgs(
                    propertyName));

            NotifyErrorProperty(
                propertyName);
        }

        OnPropertyChanged(
            nameof(HasErrors));
    }

    private string? GetFirstError(
        string propertyName)
    {
        return _errors.TryGetValue(
                propertyName,
                out var errors)
            ? errors.FirstOrDefault()
            : null;
    }

    private void NotifyErrorProperty(
        string propertyName)
    {
        var errorPropertyName =
            propertyName switch
            {
                nameof(Name) =>
                    nameof(NameError),

                nameof(Description) =>
                    nameof(DescriptionError),

                nameof(DisplayOrderText) =>
                    nameof(DisplayOrderError),

                _ =>
                    null
            };

        if (errorPropertyName is not null)
        {
            OnPropertyChanged(
                errorPropertyName);
        }
    }

    private static bool TryParseDisplayOrder(
        string? text,
        out int value)
    {
        return int.TryParse(
            text?.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }
}