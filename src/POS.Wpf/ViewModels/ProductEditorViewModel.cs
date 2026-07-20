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
using POS.Domain.Constants;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// ViewModel của cửa sổ thêm và chỉnh sửa sản phẩm.
///
/// Nguyên tắc tồn kho:
///
/// Create:
/// - cho nhập tồn đầu kỳ;
/// - ProductService sẽ tạo OpeningBalance;
/// - Product và movement nằm trong cùng transaction.
///
/// Edit:
/// - chỉ hiển thị tồn hiện tại;
/// - tuyệt đối không đưa tồn kho vào UpdateProductRequest;
/// - mọi thay đổi tồn phải đi qua InventoryService.
///
/// ViewModel không giữ DbContext.
/// Mỗi thao tác tải hoặc lưu tạo một DI scope ngắn.
/// </summary>
public sealed class ProductEditorViewModel :
    ViewModelBase,
    INotifyDataErrorInfo
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        ProductEditorViewModel>
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

    private string _code =
        string.Empty;

    private string _barcode =
        string.Empty;

    private string _name =
        string.Empty;

    private string _description =
        string.Empty;

    private string _unitName =
        "Cái";

    private string _imagePath =
        string.Empty;

    private string _costPriceText =
        "0";

    private string _salePriceText =
        "0";

    private string _initialStockQuantityText =
        "0";

    private string _minimumStockText =
        "0";

    private int _currentStockQuantity;

    private bool _trackInventory = true;
    private bool _allowNegativeStock;
    private bool _isActive = true;

    private bool _isBusy;
    private bool _suppressValidation;

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

            NotifyEditorModePresentation();
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
            ? "Cập nhật thông tin, giá bán và chính sách kho. " +
              "Tồn thực tế không được chỉnh trực tiếp tại đây."
            : "Khai báo sản phẩm mới. Tồn đầu kỳ sẽ được " +
              "ghi thành lịch sử kho ngay khi tạo.";

    public string SaveButtonText =>
        IsEditMode
            ? "Lưu thay đổi"
            : "Tạo sản phẩm";

    public string InventorySectionTitle =>
        IsEditMode
            ? "Chính sách và trạng thái kho"
            : "Thiết lập tồn kho ban đầu";

    public string InventoryModeBadgeText =>
        IsEditMode
            ? "TỒN KHO ĐƯỢC KHÓA"
            : "OPENING BALANCE";

    public string StockQuantityLabel =>
        IsEditMode
            ? "Tồn hiện tại"
            : "Tồn đầu kỳ";

    public string StockQuantityHint
    {
        get
        {
            if (IsEditMode)
            {
                return TrackInventory
                    ? "Đây là số tồn đã lưu trong hệ thống. " +
                      "Hãy dùng Điều chỉnh kho để thay đổi."
                    : "Sản phẩm hiện không theo dõi tồn kho.";
            }

            if (!TrackInventory)
            {
                return
                    "Sản phẩm không theo dõi kho sẽ được tạo với tồn bằng 0.";
            }

            return
                "Giá trị khác 0 sẽ tạo đúng một movement Tồn đầu kỳ.";
        }
    }

    public string InventoryTrackingHint
    {
        get
        {
            if (!IsEditMode)
            {
                return
                    "Bật để quản lý nhập, xuất, kiểm kê và cảnh báo tồn.";
            }

            if (CurrentStockQuantity != 0)
            {
                return
                    "Không thể tắt theo dõi kho khi tồn hiện tại khác 0. " +
                    "Hãy kiểm kê về 0 trước.";
            }

            return
                "Có thể thay đổi chế độ theo dõi vì tồn hiện tại bằng 0.";
        }
    }

    public string OpeningBalancePreviewText
    {
        get
        {
            if (IsEditMode)
            {
                return
                    $"Tồn hiện tại: " +
                    $"{FormatQuantity(CurrentStockQuantity)}";
            }

            if (!TrackInventory)
            {
                return
                    "Không tạo lịch sử tồn đầu kỳ.";
            }

            if (!TryParseInteger(
                    InitialStockQuantityText,
                    out var quantity))
            {
                return
                    "Nhập số nguyên hợp lệ để xem trước.";
            }

            if (quantity == 0)
            {
                return
                    "Tồn bằng 0 — không tạo movement rỗng.";
            }

            var prefix =
                quantity > 0
                    ? "+"
                    : string.Empty;

            return
                $"Sẽ tạo OpeningBalance: " +
                $"{prefix}{FormatQuantity(quantity)}";
        }
    }

    public bool CanEditInitialStock =>
        !IsEditMode &&
        TrackInventory &&
        !IsBusy;

    public bool CanChangeInventoryTracking =>
        !IsBusy &&
        (
            !IsEditMode ||
            CurrentStockQuantity == 0
        );

    public bool CanEditMinimumStock =>
        TrackInventory &&
        !IsBusy;

    public bool CanAllowNegativeStock =>
        TrackInventory &&
        !IsBusy;

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

            ValidateWhenEnabled(
                ValidateCategory);
        }
    }

    public string Code
    {
        get => _code;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _code,
                    normalized))
            {
                return;
            }

            ValidateWhenEnabled(
                ValidateCode);
        }
    }

    public string Barcode
    {
        get => _barcode;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _barcode,
                    normalized))
            {
                return;
            }

            ValidateWhenEnabled(
                ValidateBarcode);
        }
    }

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

            ValidateWhenEnabled(
                ValidateDescription);
        }
    }

    public string UnitName
    {
        get => _unitName;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _unitName,
                    normalized))
            {
                return;
            }

            OnPropertyChanged(
                nameof(OpeningBalancePreviewText));

            ValidateWhenEnabled(
                ValidateUnitName);
        }
    }

    public string ImagePath
    {
        get => _imagePath;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _imagePath,
                    normalized))
            {
                return;
            }

            ValidateWhenEnabled(
                ValidateImagePath);
        }
    }

    public string CostPriceText
    {
        get => _costPriceText;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _costPriceText,
                    normalized))
            {
                return;
            }

            NotifyProfitPresentation();

            ValidateWhenEnabled(
                ValidateCostPrice);
        }
    }

    public string SalePriceText
    {
        get => _salePriceText;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _salePriceText,
                    normalized))
            {
                return;
            }

            NotifyProfitPresentation();

            ValidateWhenEnabled(
                ValidateSalePrice);
        }
    }

    public string InitialStockQuantityText
    {
        get => _initialStockQuantityText;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _initialStockQuantityText,
                    normalized))
            {
                return;
            }

            OnPropertyChanged(
                nameof(OpeningBalancePreviewText));

            ValidateWhenEnabled(
                ValidateInitialStock);
        }
    }

    public string MinimumStockText
    {
        get => _minimumStockText;

        set
        {
            var normalized =
                value ?? string.Empty;

            if (!SetProperty(
                    ref _minimumStockText,
                    normalized))
            {
                return;
            }

            ValidateWhenEnabled(
                ValidateMinimumStock);
        }
    }

    public int CurrentStockQuantity
    {
        get => _currentStockQuantity;

        private set
        {
            if (!SetProperty(
                    ref _currentStockQuantity,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(CanChangeInventoryTracking));

            OnPropertyChanged(
                nameof(InventoryTrackingHint));

            OnPropertyChanged(
                nameof(OpeningBalancePreviewText));
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
            }

            NotifyInventoryPresentation();

            ValidateWhenEnabled(
                ValidateInitialStock);

            ValidateWhenEnabled(
                ValidateMinimumStock);
        }
    }

    public bool AllowNegativeStock
    {
        get => _allowNegativeStock;

        set
        {
            var normalized =
                TrackInventory &&
                value;

            if (!SetProperty(
                    ref _allowNegativeStock,
                    normalized))
            {
                return;
            }

            ValidateWhenEnabled(
                ValidateInitialStock);
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

            NotifyInventoryPresentation();

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
                return
                    "Nhập giá để xem lợi nhuận";
            }

            long profit;

            try
            {
                profit =
                    checked(
                        salePrice -
                        costPrice);
            }
            catch (OverflowException)
            {
                return
                    "Giá trị lợi nhuận vượt giới hạn";
            }

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
        GetFirstError(
            nameof(Code));

    public string? BarcodeError =>
        GetFirstError(
            nameof(Barcode));

    public string? NameError =>
        GetFirstError(
            nameof(Name));

    public string? DescriptionError =>
        GetFirstError(
            nameof(Description));

    public string? UnitNameError =>
        GetFirstError(
            nameof(UnitName));

    public string? ImagePathError =>
        GetFirstError(
            nameof(ImagePath));

    public string? CostPriceError =>
        GetFirstError(
            nameof(CostPriceText));

    public string? SalePriceError =>
        GetFirstError(
            nameof(SalePriceText));

    public string? InitialStockError =>
        GetFirstError(
            nameof(InitialStockQuantityText));

    public string? MinimumStockError =>
        GetFirstError(
            nameof(MinimumStockText));

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

    public bool GetHasErrors()
    {
        return HasErrors;
    }

    public async Task InitializeAsync(
        int? productId)
    {
        _suppressValidation = true;

        ClearAllErrors();

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
                Categories.Add(
                    category);
            }

            if (IsEditMode)
            {
                var productService =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IProductService>();

                var productResult =
                    await productService
                        .GetByIdAsync(
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
                CurrentStockQuantity = 0;

                SelectedCategory =
                    Categories.FirstOrDefault();

                Code = string.Empty;
                Barcode = string.Empty;
                Name = string.Empty;
                Description = string.Empty;
                UnitName = "Cái";
                ImagePath = string.Empty;

                CostPriceText = "0";
                SalePriceText = "0";

                InitialStockQuantityText =
                    "0";

                MinimumStockText =
                    "0";

                TrackInventory = true;
                AllowNegativeStock = false;
                IsActive = true;

                StatusMessage =
                    string.Empty;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể khởi tạo ProductEditor.");

            ShowError(
                "Không thể tải dữ liệu sản phẩm. " +
                exception
                    .GetBaseException()
                    .Message);
        }
        finally
        {
            _suppressValidation = false;

            IsBusy = false;

            NotifyEditorModePresentation();
            NotifyInventoryPresentation();
            NotifyProfitPresentation();
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
                out var salePrice))
        {
            ShowError(
                "Giá vốn hoặc giá bán không hợp lệ.");

            return;
        }

        var minimumStock = 0;

        if (TrackInventory &&
            !TryParseInteger(
                MinimumStockText,
                out minimumStock))
        {
            ShowError(
                "Mức cảnh báo tồn kho không hợp lệ.");

            return;
        }

        var initialStock = 0;

        if (!IsEditMode &&
            TrackInventory &&
            !TryParseInteger(
                InitialStockQuantityText,
                out initialStock))
        {
            ShowError(
                "Tồn đầu kỳ không hợp lệ.");

            return;
        }

        /*
         * Hàng rào giao diện cuối cùng.
         *
         * Checkbox đã bị khóa khi tồn khác 0,
         * nhưng vẫn kiểm tra lại để tránh state bị thay đổi
         * bằng code hoặc binding ngoài ý muốn.
         */
        if (IsEditMode &&
            !TrackInventory &&
            CurrentStockQuantity != 0)
        {
            ShowError(
                "Không thể tắt theo dõi kho khi tồn hiện tại khác 0. " +
                "Hãy kiểm kê về 0 trước.");

            return;
        }

        IsBusy = true;
        IsStatusError = false;

        StatusMessage =
            IsEditMode
                ? "Đang lưu thay đổi..."
                : "Đang tạo sản phẩm và ghi tồn đầu kỳ...";

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

                        code:
                            Code,

                        name:
                            Name,

                        unitName:
                            UnitName,

                        costPrice:
                            costPrice,

                        salePrice:
                            salePrice,

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
                        .UpdateAsync(
                            request);
            }
            else
            {
                var request =
                    new CreateProductRequest(
                        categoryId:
                            SelectedCategory.Id,

                        code:
                            Code,

                        name:
                            Name,

                        unitName:
                            UnitName,

                        costPrice:
                            costPrice,

                        salePrice:
                            salePrice,

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
                        .CreateAsync(
                            request);
            }

            if (result.IsFailure)
            {
                ShowError(
                    result.Error.Message);

                _logger.LogWarning(
                    "Lưu Product thất bại: " +
                    "{ErrorCode} - {ErrorMessage}",
                    result.Error.Code,
                    result.Error.Message);

                return;
            }

            StatusMessage =
                IsEditMode
                    ? "Đã cập nhật sản phẩm."
                    : initialStock == 0 ||
                      !TrackInventory
                        ? "Đã tạo sản phẩm."
                        : "Đã tạo sản phẩm và ghi tồn đầu kỳ.";

            RequestClose?.Invoke(
                true);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể lưu Product.");

            ShowError(
                "Không thể lưu sản phẩm. " +
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

    private void ApplyProduct(
        ProductDetailsDto product)
    {
        _suppressValidation = true;

        try
        {
            SelectedCategory =
                Categories.FirstOrDefault(
                    category =>
                        category.Id ==
                        product.CategoryId);

            Code =
                product.Code;

            Barcode =
                product.Barcode ??
                string.Empty;

            Name =
                product.Name;

            Description =
                product.Description ??
                string.Empty;

            UnitName =
                product.UnitName;

            ImagePath =
                product.ImagePath ??
                string.Empty;

            CostPriceText =
                product.CostPrice.ToString(
                    CultureInfo.InvariantCulture);

            SalePriceText =
                product.SalePrice.ToString(
                    CultureInfo.InvariantCulture);

            CurrentStockQuantity =
                product.StockQuantity;

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

            StatusMessage =
                string.Empty;

            IsStatusError =
                false;

            ClearAllErrors();
        }
        finally
        {
            _suppressValidation = false;

            NotifyInventoryPresentation();
            NotifyProfitPresentation();
        }
    }

    private void ValidateAll()
    {
        ValidateCategory();
        ValidateCode();
        ValidateBarcode();
        ValidateName();
        ValidateDescription();
        ValidateUnitName();
        ValidateImagePath();
        ValidateCostPrice();
        ValidateSalePrice();
        ValidateInitialStock();
        ValidateMinimumStock();
        ValidateTrackingTransition();
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
        var normalized =
            Code.Trim();

        string? message =
            string.IsNullOrWhiteSpace(normalized)
                ? "Mã sản phẩm không được để trống."
                : normalized.Length >
                  BusinessRules.Products.CodeMaxLength
                    ? $"Mã sản phẩm tối đa " +
                      $"{BusinessRules.Products.CodeMaxLength} ký tự."
                    : null;

        SetError(
            nameof(Code),
            message);
    }

    private void ValidateBarcode()
    {
        var normalized =
            Barcode.Trim();

        var message =
            normalized.Length >
            BusinessRules.Products.BarcodeMaxLength
                ? $"Mã vạch tối đa " +
                  $"{BusinessRules.Products.BarcodeMaxLength} ký tự."
                : null;

        SetError(
            nameof(Barcode),
            message);
    }

    private void ValidateName()
    {
        var normalized =
            Name.Trim();

        string? message =
            string.IsNullOrWhiteSpace(normalized)
                ? "Tên sản phẩm không được để trống."
                : normalized.Length >
                  BusinessRules.Products.NameMaxLength
                    ? $"Tên sản phẩm tối đa " +
                      $"{BusinessRules.Products.NameMaxLength} ký tự."
                    : null;

        SetError(
            nameof(Name),
            message);
    }

    private void ValidateDescription()
    {
        var message =
            Description.Trim().Length >
            BusinessRules.Products.DescriptionMaxLength
                ? $"Mô tả tối đa " +
                  $"{BusinessRules.Products.DescriptionMaxLength} ký tự."
                : null;

        SetError(
            nameof(Description),
            message);
    }

    private void ValidateUnitName()
    {
        var normalized =
            UnitName.Trim();

        string? message =
            string.IsNullOrWhiteSpace(normalized)
                ? "Đơn vị tính không được để trống."
                : normalized.Length >
                  BusinessRules.Products.UnitNameMaxLength
                    ? $"Đơn vị tính tối đa " +
                      $"{BusinessRules.Products.UnitNameMaxLength} ký tự."
                    : null;

        SetError(
            nameof(UnitName),
            message);
    }

    private void ValidateImagePath()
    {
        var message =
            ImagePath.Trim().Length >
            BusinessRules.Products.ImagePathMaxLength
                ? $"Đường dẫn ảnh tối đa " +
                  $"{BusinessRules.Products.ImagePathMaxLength} ký tự."
                : null;

        SetError(
            nameof(ImagePath),
            message);
    }

    private void ValidateCostPrice()
    {
        string? message;

        if (!TryParseMoney(
                CostPriceText,
                out var value))
        {
            message =
                "Giá vốn phải là số nguyên.";
        }
        else if (value < 0)
        {
            message =
                "Giá vốn không được âm.";
        }
        else if (value >
                 BusinessRules.Products.MaximumPrice)
        {
            message =
                "Giá vốn vượt quá giới hạn hệ thống.";
        }
        else
        {
            message = null;
        }

        SetError(
            nameof(CostPriceText),
            message);
    }

    private void ValidateSalePrice()
    {
        string? message;

        if (!TryParseMoney(
                SalePriceText,
                out var value))
        {
            message =
                "Giá bán phải là số nguyên.";
        }
        else if (value < 0)
        {
            message =
                "Giá bán không được âm.";
        }
        else if (value >
                 BusinessRules.Products.MaximumPrice)
        {
            message =
                "Giá bán vượt quá giới hạn hệ thống.";
        }
        else
        {
            message = null;
        }

        SetError(
            nameof(SalePriceText),
            message);
    }

    private void ValidateInitialStock()
    {
        /*
         * Edit mode chỉ hiển thị tồn hiện tại.
         * Không dùng trường này để cập nhật Product.
         */
        if (IsEditMode ||
            !TrackInventory)
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
                "Tồn đầu kỳ phải là số nguyên.");

            return;
        }

        if (value >
                BusinessRules.Products
                    .MaximumStockQuantity ||
            value <
                -BusinessRules.Products
                    .MaximumStockQuantity)
        {
            SetError(
                nameof(
                    InitialStockQuantityText),
                "Tồn đầu kỳ vượt quá giới hạn hệ thống.");

            return;
        }

        if (!AllowNegativeStock &&
            value < 0)
        {
            SetError(
                nameof(
                    InitialStockQuantityText),
                "Sản phẩm không cho phép tồn kho âm.");

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

        string? message;

        if (!TryParseInteger(
                MinimumStockText,
                out var value))
        {
            message =
                "Mức cảnh báo phải là số nguyên.";
        }
        else if (value < 0)
        {
            message =
                "Mức cảnh báo không được âm.";
        }
        else if (value >
                 BusinessRules.Products
                     .MaximumStockQuantity)
        {
            message =
                "Mức cảnh báo vượt quá giới hạn hệ thống.";
        }
        else
        {
            message = null;
        }

        SetError(
            nameof(MinimumStockText),
            message);
    }

    private void ValidateTrackingTransition()
    {
        if (IsEditMode &&
            !TrackInventory &&
            CurrentStockQuantity != 0)
        {
            SetError(
                nameof(TrackInventory),
                "Phải kiểm kê tồn về 0 trước khi tắt theo dõi kho.");

            return;
        }

        SetError(
            nameof(TrackInventory),
            null);
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
            "Lệnh ProductEditor không thể hoàn thành.");

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
        var hadError =
            _errors.ContainsKey(
                propertyName);

        if (string.IsNullOrWhiteSpace(error))
        {
            if (!hadError)
            {
                return;
            }

            _errors.Remove(
                propertyName);
        }
        else
        {
            if (hadError &&
                _errors[propertyName]
                    .Count == 1 &&
                string.Equals(
                    _errors[propertyName][0],
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

        NotifyErrorPresentation(
            propertyName);
    }

    private void ClearAllErrors()
    {
        if (_errors.Count == 0)
        {
            return;
        }

        var propertyNames =
            _errors.Keys.ToArray();

        _errors.Clear();

        foreach (var propertyName
                 in propertyNames)
        {
            ErrorsChanged?.Invoke(
                this,
                new DataErrorsChangedEventArgs(
                    propertyName));

            NotifyErrorPresentation(
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

    private void NotifyErrorPresentation(
        string propertyName)
    {
        var presentationPropertyName =
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

                nameof(Description) =>
                    nameof(DescriptionError),

                nameof(UnitName) =>
                    nameof(UnitNameError),

                nameof(ImagePath) =>
                    nameof(ImagePathError),

                nameof(CostPriceText) =>
                    nameof(CostPriceError),

                nameof(SalePriceText) =>
                    nameof(SalePriceError),

                nameof(InitialStockQuantityText) =>
                    nameof(InitialStockError),

                nameof(MinimumStockText) =>
                    nameof(MinimumStockError),

                _ =>
                    null
            };

        if (presentationPropertyName is not null)
        {
            OnPropertyChanged(
                presentationPropertyName);
        }
    }

    private void NotifyEditorModePresentation()
    {
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
            nameof(InventorySectionTitle));

        OnPropertyChanged(
            nameof(InventoryModeBadgeText));

        OnPropertyChanged(
            nameof(StockQuantityLabel));

        NotifyInventoryPresentation();
    }

    private void NotifyInventoryPresentation()
    {
        OnPropertyChanged(
            nameof(CanEditInitialStock));

        OnPropertyChanged(
            nameof(CanChangeInventoryTracking));

        OnPropertyChanged(
            nameof(CanEditMinimumStock));

        OnPropertyChanged(
            nameof(CanAllowNegativeStock));

        OnPropertyChanged(
            nameof(StockQuantityHint));

        OnPropertyChanged(
            nameof(InventoryTrackingHint));

        OnPropertyChanged(
            nameof(OpeningBalancePreviewText));
    }

    private void NotifyProfitPresentation()
    {
        OnPropertyChanged(
            nameof(ProfitPreviewText));

        OnPropertyChanged(
            nameof(IsProfitNegative));
    }

    private string FormatQuantity(
        int quantity)
    {
        var formatted =
            quantity.ToString(
                "N0",
                VietnameseCulture);

        return string.IsNullOrWhiteSpace(
            UnitName)
                ? formatted
                : $"{formatted} {UnitName.Trim()}";
    }

    private static bool TryParseMoney(
        string? text,
        out long value)
    {
        var normalized =
            NormalizeNumericText(
                text);

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
            NormalizeNumericText(
                text);

        return int.TryParse(
            normalized,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string NormalizeNumericText(
        string? text)
    {
        return
            (text ?? string.Empty)
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