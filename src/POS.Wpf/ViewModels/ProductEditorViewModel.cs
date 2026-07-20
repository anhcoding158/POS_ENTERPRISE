using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Categories;
using POS.Application.DTOs.Products;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// ViewModel cho form thêm và sửa sản phẩm.
///
/// Mỗi thao tác tải/lưu tạo một DI scope riêng,
/// vì vậy DbContext không sống cùng cửa sổ editor.
/// </summary>
public sealed class ProductEditorViewModel :
    ViewModelBase,
    INotifyDataErrorInfo
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo("vi-VN");

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<ProductEditorViewModel>
        _logger;

    private readonly Dictionary<
        string,
        List<string>>
        _errors =
            new(
                StringComparer.Ordinal);

    private int? _productId;

    private CategoryOptionDto?
        _selectedCategory;

    private string _code = string.Empty;
    private string _barcode = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _unitName = "Cái";
    private string _imagePath = string.Empty;

    private string _costPriceText = "0";
    private string _salePriceText = "0";
    private string _initialStockQuantityText = "0";
    private string _minimumStockText = "0";

    private bool _trackInventory = true;
    private bool _allowNegativeStock;
    private bool _isActive = true;
    private bool _isBusy;

    private string _statusMessage =
        string.Empty;

    private bool _isStatusError;

    public ProductEditorViewModel(
        IServiceScopeFactory scopeFactory,
        ILogger<ProductEditorViewModel> logger)
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

    public ObservableCollection<
        CategoryOptionDto>
        Categories
    { get; } = [];

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand CancelCommand { get; }

    public int? ProductId
    {
        get => _productId;

        private set
        {
            if (!SetProperty(
                    ref _productId,
                    value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderDescription));
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(CanEditInitialStock));
        }
    }

    public bool IsEditMode =>
        ProductId.HasValue;

    public string WindowTitle =>
        IsEditMode
            ? "Chỉnh sửa sản phẩm"
            : "Thêm sản phẩm";

    public string HeaderTitle =>
        IsEditMode
            ? "Cập nhật sản phẩm"
            : "Tạo sản phẩm mới";

    public string HeaderDescription =>
        IsEditMode
            ? "Chỉnh sửa thông tin, giá bán và cấu hình tồn kho."
            : "Khai báo sản phẩm mới cho danh mục bán hàng.";

    public string SaveButtonText =>
        IsEditMode
            ? "Lưu thay đổi"
            : "Tạo sản phẩm";

    public CategoryOptionDto?
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

            ValidateCategory();
        }
    }

    public string Code
    {
        get => _code;

        set
        {
            if (!SetProperty(
                    ref _code,
                    value))
            {
                return;
            }

            ValidateCode();
        }
    }

    public string Barcode
    {
        get => _barcode;

        set
        {
            if (!SetProperty(
                    ref _barcode,
                    value))
            {
                return;
            }

            ValidateBarcode();
        }
    }

    public string Name
    {
        get => _name;

        set
        {
            if (!SetProperty(
                    ref _name,
                    value))
            {
                return;
            }

            ValidateName();
        }
    }

    public string Description
    {
        get => _description;

        set => SetProperty(
            ref _description,
            value);
    }

    public string UnitName
    {
        get => _unitName;

        set
        {
            if (!SetProperty(
                    ref _unitName,
                    value))
            {
                return;
            }

            ValidateUnitName();
        }
    }

    public string ImagePath
    {
        get => _imagePath;

        set => SetProperty(
            ref _imagePath,
            value);
    }

    public string CostPriceText
    {
        get => _costPriceText;

        set
        {
            if (!SetProperty(
                    ref _costPriceText,
                    value))
            {
                return;
            }

            ValidateCostPrice();
            NotifyProfitPreviewChanged();
        }
    }

    public string SalePriceText
    {
        get => _salePriceText;

        set
        {
            if (!SetProperty(
                    ref _salePriceText,
                    value))
            {
                return;
            }

            ValidateSalePrice();
            NotifyProfitPreviewChanged();
        }
    }

    public string InitialStockQuantityText
    {
        get => _initialStockQuantityText;

        set
        {
            if (!SetProperty(
                    ref _initialStockQuantityText,
                    value))
            {
                return;
            }

            ValidateInitialStock();
        }
    }

    public string MinimumStockText
    {
        get => _minimumStockText;

        set
        {
            if (!SetProperty(
                    ref _minimumStockText,
                    value))
            {
                return;
            }

            ValidateMinimumStock();
        }
    }

    public bool TrackInventory
    {
        get => _trackInventory;

        set
        {
            if (!SetProperty(
                    ref _trackInventory,
                    value))
            {
                return;
            }

            if (!value)
            {
                AllowNegativeStock = false;
                MinimumStockText = "0";

                if (!IsEditMode)
                {
                    InitialStockQuantityText = "0";
                }
            }

            OnPropertyChanged(
                nameof(CanEditInitialStock));

            ValidateInitialStock();
            ValidateMinimumStock();
        }
    }

    public bool AllowNegativeStock
    {
        get => _allowNegativeStock;

        set
        {
            var normalizedValue =
                TrackInventory &&
                value;

            if (!SetProperty(
                    ref _allowNegativeStock,
                    normalizedValue))
            {
                return;
            }

            ValidateInitialStock();
        }
    }

    public bool IsActive
    {
        get => _isActive;

        set => SetProperty(
            ref _isActive,
            value);
    }

    public bool CanEditInitialStock =>
        !IsEditMode &&
        TrackInventory;

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

            SaveCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
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

    public string ProfitPreviewText
    {
        get
        {
            if (!TryParseMoney(
                    CostPriceText,
                    out var costPrice) ||
                !TryParseMoney(
                    SalePriceText,
                    out var salePrice))
            {
                return "Nhập giá để xem lợi nhuận";
            }

            var profit =
                salePrice - costPrice;

            var percentage =
                salePrice > 0
                    ? profit /
                      (decimal)salePrice *
                      100m
                    : 0m;

            return
                $"{FormatMoney(profit)} " +
                $"({percentage:N1}%)";
        }
    }

    public bool IsProfitNegative
    {
        get
        {
            return
                TryParseMoney(
                    CostPriceText,
                    out var costPrice) &&
                TryParseMoney(
                    SalePriceText,
                    out var salePrice) &&
                salePrice < costPrice;
        }
    }

    public string? CategoryError =>
        GetFirstError(
            nameof(SelectedCategory));

    public string? CodeError =>
        GetFirstError(nameof(Code));

    public string? BarcodeError =>
        GetFirstError(nameof(Barcode));

    public string? NameError =>
        GetFirstError(nameof(Name));

    public string? UnitNameError =>
        GetFirstError(nameof(UnitName));

    public string? CostPriceError =>
        GetFirstError(nameof(CostPriceText));

    public string? SalePriceError =>
        GetFirstError(nameof(SalePriceText));

    public string? InitialStockError =>
        GetFirstError(
            nameof(InitialStockQuantityText));

    public string? MinimumStockError =>
        GetFirstError(
            nameof(MinimumStockText));

    public bool GetHasErrors()
    {
        return HasErrors;
    }

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
        int? productId)
    {
        ProductId = productId;

        IsBusy = true;
        IsStatusError = false;

        StatusMessage =
            IsEditMode
                ? "Đang tải dữ liệu sản phẩm..."
                : "Đang tải danh mục...";

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var categoryService =
                scope.ServiceProvider
                    .GetRequiredService<
                        ICategoryService>();

            var categoryResult =
                await categoryService
                    .ListActiveAsync();

            if (categoryResult.IsFailure)
            {
                ShowError(
                    categoryResult.Error.Message);

                return;
            }

            Categories.Clear();

            foreach (var category in
                     categoryResult.Value)
            {
                Categories.Add(category);
            }

            if (IsEditMode)
            {
                var productService =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IProductService>();

                var productResult =
                    await productService.GetByIdAsync(
                        ProductId!.Value);

                if (productResult.IsFailure)
                {
                    ShowError(
                        productResult.Error.Message);

                    return;
                }

                ApplyProduct(
                    productResult.Value);
            }
            else
            {
                SelectedCategory =
                    Categories.FirstOrDefault();

                StatusMessage = string.Empty;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể khởi tạo ProductEditor.");

            ShowError(
                "Không thể tải dữ liệu sản phẩm. " +
                exception.GetBaseException().Message);
        }
        finally
        {
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

        if (SelectedCategory is null)
        {
            ShowError(
                "Vui lòng chọn danh mục sản phẩm.");

            return;
        }

        if (!TryParseMoney(
                CostPriceText,
                out var costPrice) ||
            !TryParseMoney(
                SalePriceText,
                out var salePrice) ||
            !TryParseInteger(
                MinimumStockText,
                out var minimumStock) ||
            !TryParseInteger(
                InitialStockQuantityText,
                out var initialStock))
        {
            ShowError(
                "Một hoặc nhiều giá trị số không hợp lệ.");

            return;
        }

        IsBusy = true;
        IsStatusError = false;
        StatusMessage = "Đang lưu sản phẩm...";

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var productService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IProductService>();

            Result<ProductDetailsDto> result;

            if (IsEditMode)
            {
                var request =
                    new UpdateProductRequest(
                        productId:
                            ProductId!.Value,
                        categoryId:
                            SelectedCategory.Id,
                        code: Code,
                        name: Name,
                        unitName: UnitName,
                        costPrice: costPrice,
                        salePrice: salePrice,
                        minimumStock:
                            TrackInventory
                                ? minimumStock
                                : 0,
                        trackInventory:
                            TrackInventory,
                        allowNegativeStock:
                            AllowNegativeStock,
                        isActive:
                            IsActive,
                        barcode:
                            Barcode,
                        description:
                            Description,
                        imagePath:
                            ImagePath);

                result =
                    await productService
                        .UpdateAsync(request);
            }
            else
            {
                var request =
                    new CreateProductRequest(
                        categoryId:
                            SelectedCategory.Id,
                        code: Code,
                        name: Name,
                        unitName: UnitName,
                        costPrice: costPrice,
                        salePrice: salePrice,
                        initialStockQuantity:
                            TrackInventory
                                ? initialStock
                                : 0,
                        minimumStock:
                            TrackInventory
                                ? minimumStock
                                : 0,
                        trackInventory:
                            TrackInventory,
                        allowNegativeStock:
                            AllowNegativeStock,
                        barcode:
                            Barcode,
                        description:
                            Description,
                        imagePath:
                            ImagePath);

                result =
                    await productService
                        .CreateAsync(request);
            }

            if (result.IsFailure)
            {
                ApplyServiceError(
                    result.Error);

                return;
            }

            StatusMessage =
                IsEditMode
                    ? "Cập nhật sản phẩm thành công."
                    : "Tạo sản phẩm thành công.";

            IsStatusError = false;

            RequestClose?.Invoke(true);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể lưu sản phẩm.");

            ShowError(
                "Không thể lưu sản phẩm. " +
                exception.GetBaseException().Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task CancelAsync()
    {
        RequestClose?.Invoke(false);

        return Task.CompletedTask;
    }

    private void ApplyProduct(
        ProductDetailsDto product)
    {
        var selectedCategory =
            Categories.FirstOrDefault(
                category =>
                    category.Id ==
                    product.CategoryId);

        if (selectedCategory is null)
        {
            selectedCategory =
                new CategoryOptionDto(
                    product.CategoryId,
                    product.CategoryName,
                    int.MaxValue);

            Categories.Add(
                selectedCategory);
        }

        SelectedCategory =
            selectedCategory;

        Code = product.Code;
        Barcode = product.Barcode ?? string.Empty;
        Name = product.Name;
        Description =
            product.Description ?? string.Empty;

        UnitName = product.UnitName;
        ImagePath =
            product.ImagePath ?? string.Empty;

        CostPriceText =
            product.CostPrice.ToString(
                CultureInfo.InvariantCulture);

        SalePriceText =
            product.SalePrice.ToString(
                CultureInfo.InvariantCulture);

        InitialStockQuantityText =
            product.StockQuantity.ToString(
                CultureInfo.InvariantCulture);

        MinimumStockText =
            product.MinimumStock.ToString(
                CultureInfo.InvariantCulture);

        TrackInventory =
            product.TrackInventory;

        AllowNegativeStock =
            product.AllowNegativeStock;

        IsActive =
            product.IsActive;

        StatusMessage = string.Empty;
    }

    private void ValidateAll()
    {
        ValidateCategory();
        ValidateCode();
        ValidateBarcode();
        ValidateName();
        ValidateUnitName();
        ValidateCostPrice();
        ValidateSalePrice();
        ValidateInitialStock();
        ValidateMinimumStock();
    }

    private void ValidateCategory()
    {
        SetError(
            nameof(SelectedCategory),
            SelectedCategory is null
                ? "Vui lòng chọn danh mục."
                : null);
    }

    private void ValidateCode()
    {
        SetError(
            nameof(Code),
            string.IsNullOrWhiteSpace(Code)
                ? "Mã sản phẩm không được để trống."
                : null);
    }

    private void ValidateBarcode()
    {
        var normalized =
            Barcode?.Trim();

        SetError(
            nameof(Barcode),
            normalized is not null &&
            normalized.Length > 100
                ? "Mã vạch quá dài."
                : null);
    }

    private void ValidateName()
    {
        SetError(
            nameof(Name),
            string.IsNullOrWhiteSpace(Name)
                ? "Tên sản phẩm không được để trống."
                : null);
    }

    private void ValidateUnitName()
    {
        SetError(
            nameof(UnitName),
            string.IsNullOrWhiteSpace(UnitName)
                ? "Đơn vị tính không được để trống."
                : null);
    }

    private void ValidateCostPrice()
    {
        var message =
            !TryParseMoney(
                CostPriceText,
                out var value)
                ? "Giá vốn phải là số nguyên."
                : value < 0
                    ? "Giá vốn không được âm."
                    : null;

        SetError(
            nameof(CostPriceText),
            message);
    }

    private void ValidateSalePrice()
    {
        var message =
            !TryParseMoney(
                SalePriceText,
                out var value)
                ? "Giá bán phải là số nguyên."
                : value < 0
                    ? "Giá bán không được âm."
                    : null;

        SetError(
            nameof(SalePriceText),
            message);
    }

    private void ValidateInitialStock()
    {
        if (!TrackInventory)
        {
            SetError(
                nameof(
                    InitialStockQuantityText),
                null);

            return;
        }

        if (!TryParseInteger(
                InitialStockQuantityText,
                out var value))
        {
            SetError(
                nameof(
                    InitialStockQuantityText),
                "Tồn kho phải là số nguyên.");

            return;
        }

        if (!AllowNegativeStock &&
            value < 0)
        {
            SetError(
                nameof(
                    InitialStockQuantityText),
                "Tồn kho không được âm.");

            return;
        }

        SetError(
            nameof(
                InitialStockQuantityText),
            null);
    }

    private void ValidateMinimumStock()
    {
        if (!TrackInventory)
        {
            SetError(
                nameof(MinimumStockText),
                null);

            return;
        }

        var message =
            !TryParseInteger(
                MinimumStockText,
                out var value)
                ? "Mức cảnh báo phải là số nguyên."
                : value < 0
                    ? "Mức cảnh báo không được âm."
                    : null;

        SetError(
            nameof(MinimumStockText),
            message);
    }

    private void ApplyServiceError(
        Error error)
    {
        IsStatusError = true;
        StatusMessage = error.Message;

        if (string.Equals(
                error.Code,
                ErrorCodes.Products.CodeAlreadyExists,
                StringComparison.Ordinal))
        {
            SetError(
                nameof(Code),
                error.Message);

            return;
        }

        if (string.Equals(
                error.Code,
                ErrorCodes.Products
                    .BarcodeAlreadyExists,
                StringComparison.Ordinal))
        {
            SetError(
                nameof(Barcode),
                error.Message);

            return;
        }

        if (string.Equals(
                error.Code,
                ErrorCodes.Products
                    .ConcurrencyConflict,
                StringComparison.Ordinal))
        {
            StatusMessage =
                error.Message;
        }
    }

    private void SetError(
        string propertyName,
        string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            if (!_errors.Remove(propertyName))
            {
                return;
            }
        }
        else
        {
            _errors[propertyName] = [error];
        }

        ErrorsChanged?.Invoke(
            this,
            new DataErrorsChangedEventArgs(
                propertyName));

        OnPropertyChanged(nameof(HasErrors));

        NotifyErrorProperty(
            propertyName);
    }

    private void NotifyErrorProperty(
        string propertyName)
    {
        var errorPropertyName =
            propertyName switch
            {
                nameof(SelectedCategory) =>
                    nameof(CategoryError),

                nameof(Code) =>
                    nameof(CodeError),

                nameof(Barcode) =>
                    nameof(BarcodeError),

                nameof(Name) =>
                    nameof(NameError),

                nameof(UnitName) =>
                    nameof(UnitNameError),

                nameof(CostPriceText) =>
                    nameof(CostPriceError),

                nameof(SalePriceText) =>
                    nameof(SalePriceError),

                nameof(
                    InitialStockQuantityText) =>
                    nameof(InitialStockError),

                nameof(MinimumStockText) =>
                    nameof(MinimumStockError),

                _ => null
            };

        if (errorPropertyName is not null)
        {
            OnPropertyChanged(
                errorPropertyName);
        }
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

    private void ShowError(
        string message)
    {
        IsStatusError = true;
        StatusMessage = message;
    }

    private void HandleCommandException(
        Exception exception)
    {
        _logger.LogError(
            exception,
            "Lệnh ProductEditor thất bại.");

        ShowError(
            "Thao tác không thể hoàn thành. " +
            exception.GetBaseException().Message);
    }

    private bool CanExecuteCommand()
    {
        return !IsBusy;
    }

    private void NotifyProfitPreviewChanged()
    {
        OnPropertyChanged(
            nameof(ProfitPreviewText));

        OnPropertyChanged(
            nameof(IsProfitNegative));
    }

    private static bool TryParseMoney(
        string? text,
        out long value)
    {
        var normalized =
            NormalizeNumericText(text);

        return long.TryParse(
            normalized,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryParseInteger(
        string? text,
        out int value)
    {
        var normalized =
            NormalizeNumericText(text);

        return int.TryParse(
            normalized,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string NormalizeNumericText(
        string? text)
    {
        return (text ?? string.Empty)
            .Trim()
            .Replace(
                ".",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                ",",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                " ",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                "₫",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                "đ",
                string.Empty,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatMoney(
        long amount)
    {
        return
            $"{amount.ToString(
                "N0",
                VietnameseCulture)} ₫";
    }
}