using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Inventory;
using POS.Domain.Constants;
using POS.Domain.Enums;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// Một lựa chọn loại biến động hiển thị trên giao diện.
/// </summary>
public sealed record InventoryMovementOption(
    InventoryMovementType Value,
    string DisplayName,
    string Description);

/// <summary>
/// ViewModel của cửa sổ điều chỉnh tồn kho.
///
/// ViewModel không giữ InventoryService hoặc DbContext.
/// Mỗi lần tải/lưu đều tạo một DI scope ngắn.
/// </summary>
public sealed class InventoryAdjustmentViewModel :
    ViewModelBase
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        InventoryAdjustmentViewModel>
        _logger;

    private readonly IReadOnlyList<
        InventoryMovementOption>
        _movementOptions;

    private InventoryMovementOption
        _selectedMovement;

    private int _productId;
    private string _productCode =
        string.Empty;

    private string _productName =
        string.Empty;

    private string _unitName =
        string.Empty;

    private int _currentStock;
    private int _minimumStock;

    private bool _allowNegativeStock;
    private bool _isInitialized;
    private bool _isBusy;

    private string _quantityText =
        "1";

    private string _reason =
        string.Empty;

    private string _referenceType =
        string.Empty;

    private string _referenceId =
        string.Empty;

    private string _errorMessage =
        string.Empty;

    private string _statusMessage =
        string.Empty;

    private string _previewAfterText =
        "—";

    private string _previewDeltaText =
        "Chưa có dữ liệu";

    private string _previewStateText =
        "Nhập số lượng để xem trước";

    private bool _previewIsNegative;

    public InventoryAdjustmentViewModel(
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryAdjustmentViewModel> logger)
    {
        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        _movementOptions =
        [
            new InventoryMovementOption(
                InventoryMovementType.StockIn,
                "Nhập kho",
                "Tăng tồn kho khi nhận hàng hoặc bổ sung nguyên liệu."),

            new InventoryMovementOption(
                InventoryMovementType.StockOut,
                "Xuất kho",
                "Giảm tồn kho do hỏng, hao hụt hoặc sử dụng nội bộ."),

            new InventoryMovementOption(
                InventoryMovementType.Adjustment,
                "Điều chỉnh tăng / giảm",
                "Nhập số dương để tăng hoặc số âm để giảm tồn kho."),

            new InventoryMovementOption(
                InventoryMovementType.Stocktake,
                "Kiểm kê thực tế",
                "Ghi số lượng thực tế đang có sau khi kiểm đếm.")
        ];

        _selectedMovement =
            _movementOptions[0];

        SaveCommand =
            new AsyncRelayCommand(
                SaveAsync,
                CanSave,
                HandleCommandException);
    }

    /// <summary>
    /// Cửa sổ đăng ký event này để đóng sau khi lưu.
    /// </summary>
    public event Action<bool>? CloseRequested;

    public IReadOnlyList<
        InventoryMovementOption>
        MovementOptions =>
            _movementOptions;

    public AsyncRelayCommand SaveCommand { get; }

    public int ProductId
    {
        get => _productId;

        private set => SetProperty(
            ref _productId,
            value);
    }

    public string ProductCode
    {
        get => _productCode;

        private set
        {
            if (!SetProperty(
                    ref _productCode,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(ProductIdentityText));
        }
    }

    public string ProductName
    {
        get => _productName;

        private set => SetProperty(
            ref _productName,
            value);
    }

    public string UnitName
    {
        get => _unitName;

        private set
        {
            if (!SetProperty(
                    ref _unitName,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(ProductIdentityText));

            OnPropertyChanged(
                nameof(CurrentStockText));

            OnPropertyChanged(
                nameof(MinimumStockText));

            OnPropertyChanged(
                nameof(QuantityHint));

            RefreshPreview();
        }
    }

    public int CurrentStock
    {
        get => _currentStock;

        private set
        {
            if (!SetProperty(
                    ref _currentStock,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(CurrentStockText));

            OnPropertyChanged(
                nameof(StockStateText));

            RefreshPreview();
        }
    }

    public int MinimumStock
    {
        get => _minimumStock;

        private set
        {
            if (!SetProperty(
                    ref _minimumStock,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(MinimumStockText));

            OnPropertyChanged(
                nameof(StockStateText));
        }
    }

    public bool AllowNegativeStock
    {
        get => _allowNegativeStock;

        private set
        {
            if (!SetProperty(
                    ref _allowNegativeStock,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(StockPolicyText));

            RefreshPreview();
        }
    }

    public InventoryMovementOption
        SelectedMovement
    {
        get => _selectedMovement;

        set
        {
            ArgumentNullException.ThrowIfNull(
                value);

            if (!SetProperty(
                    ref _selectedMovement,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(MovementDescription));

            OnPropertyChanged(
                nameof(QuantityLabel));

            OnPropertyChanged(
                nameof(QuantityHint));

            /*
             * Đưa ra giá trị khởi đầu phù hợp,
             * nhưng người dùng vẫn có thể sửa ngay.
             */
            QuantityText =
                value.Value ==
                    InventoryMovementType.Stocktake
                    ? CurrentStock.ToString(
                        "N0",
                        VietnameseCulture)
                    : "1";

            ClearMessages();
            RefreshPreview();
        }
    }

    public string QuantityText
    {
        get => _quantityText;

        set
        {
            if (!SetProperty(
                    ref _quantityText,
                    value))
            {
                return;
            }

            ClearError();
            RefreshPreview();
        }
    }

    public string Reason
    {
        get => _reason;

        set
        {
            if (!SetProperty(
                    ref _reason,
                    value))
            {
                return;
            }

            ClearError();

            SaveCommand
                .NotifyCanExecuteChanged();
        }
    }

    public string ReferenceType
    {
        get => _referenceType;

        set
        {
            if (!SetProperty(
                    ref _referenceType,
                    value))
            {
                return;
            }

            ClearError();
        }
    }

    public string ReferenceId
    {
        get => _referenceId;

        set
        {
            if (!SetProperty(
                    ref _referenceId,
                    value))
            {
                return;
            }

            ClearError();
        }
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
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;

        private set
        {
            if (!SetProperty(
                    ref _errorMessage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(HasError));
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
                nameof(HasStatus));
        }
    }

    public string PreviewAfterText
    {
        get => _previewAfterText;

        private set => SetProperty(
            ref _previewAfterText,
            value);
    }

    public string PreviewDeltaText
    {
        get => _previewDeltaText;

        private set => SetProperty(
            ref _previewDeltaText,
            value);
    }

    public string PreviewStateText
    {
        get => _previewStateText;

        private set => SetProperty(
            ref _previewStateText,
            value);
    }

    public bool PreviewIsNegative
    {
        get => _previewIsNegative;

        private set => SetProperty(
            ref _previewIsNegative,
            value);
    }

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    public bool HasStatus =>
        !string.IsNullOrWhiteSpace(
            StatusMessage);

    public string ProductIdentityText =>
        string.IsNullOrWhiteSpace(UnitName)
            ? ProductCode
            : $"{ProductCode}  •  {UnitName}";

    public string CurrentStockText =>
        $"{CurrentStock.ToString(
            "N0",
            VietnameseCulture)} {UnitName}";

    public string MinimumStockText =>
        $"{MinimumStock.ToString(
            "N0",
            VietnameseCulture)} {UnitName}";

    public string StockPolicyText =>
        AllowNegativeStock
            ? "Cho phép tồn kho âm"
            : "Không cho phép tồn kho âm";

    public string StockStateText
    {
        get
        {
            if (CurrentStock <= 0)
            {
                return "Hết hàng hoặc đang âm kho";
            }

            if (CurrentStock <= MinimumStock)
            {
                return "Đang ở mức cảnh báo";
            }

            return "Tồn kho đang ổn định";
        }
    }

    public string MovementDescription =>
        SelectedMovement.Description;

    public string QuantityLabel =>
        SelectedMovement.Value switch
        {
            InventoryMovementType.StockIn =>
                "Số lượng nhập",

            InventoryMovementType.StockOut =>
                "Số lượng xuất",

            InventoryMovementType.Adjustment =>
                "Mức điều chỉnh",

            InventoryMovementType.Stocktake =>
                "Tồn thực tế",

            _ =>
                "Số lượng"
        };

    public string QuantityHint =>
        SelectedMovement.Value switch
        {
            InventoryMovementType.StockIn =>
                $"Nhập số lượng {UnitName} được bổ sung.",

            InventoryMovementType.StockOut =>
                $"Nhập số lượng {UnitName} cần xuất khỏi kho.",

            InventoryMovementType.Adjustment =>
                "Dùng số dương để tăng, số âm để giảm.",

            InventoryMovementType.Stocktake =>
                $"Nhập tổng số {UnitName} thực tế sau kiểm kê.",

            _ =>
                string.Empty
        };

    public async Task<bool> InitializeAsync(
        int productId)
    {
        if (productId <= 0)
        {
            ErrorMessage =
                "Mã sản phẩm không hợp lệ.";

            return false;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var productService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IProductService>();

            var result =
                await productService
                    .GetByIdAsync(
                        productId);

            if (result.IsFailure)
            {
                ErrorMessage =
                    result.Error.Message;

                return false;
            }

            var product =
                result.Value;

            if (!product.TrackInventory)
            {
                ErrorMessage =
                    "Sản phẩm này đang tắt theo dõi tồn kho. " +
                    "Hãy bật theo dõi kho trong thông tin sản phẩm trước.";

                return false;
            }

            ProductId = product.Id;
            ProductCode = product.Code;
            ProductName = product.Name;
            UnitName = product.UnitName;

            CurrentStock =
                product.StockQuantity;

            MinimumStock =
                product.MinimumStock;

            AllowNegativeStock =
                product.AllowNegativeStock;

            _isInitialized = true;

            SelectedMovement =
                MovementOptions[0];

            QuantityText = "1";

            RefreshPreview();

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể khởi tạo cửa sổ điều chỉnh kho " +
                "cho sản phẩm {ProductId}.",
                productId);

            ErrorMessage =
                "Không thể tải thông tin sản phẩm. " +
                exception.GetBaseException().Message;

            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (!TryCreateRequest(
                out var request,
                out var validationMessage))
        {
            ErrorMessage =
                validationMessage;

            return;
        }

        IsBusy = true;
        ClearMessages();

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var inventoryService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IInventoryService>();

            var result =
                await inventoryService
                    .AdjustAsync(
                        request!);

            if (result.IsFailure)
            {
                ErrorMessage =
                    result.Error.Message;

                _logger.LogWarning(
                    "Điều chỉnh kho thất bại: " +
                    "{ErrorCode} - {ErrorMessage}",
                    result.Error.Code,
                    result.Error.Message);

                return;
            }

            CurrentStock =
                result.Value.QuantityAfter;

            StatusMessage =
                $"Đã lưu biến động kho. Tồn mới: " +
                $"{result.Value.QuantityAfter.ToString(
                    "N0",
                    VietnameseCulture)} {UnitName}.";

            CloseRequested?.Invoke(true);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể lưu biến động kho cho sản phẩm " +
                "{ProductId}.",
                ProductId);

            ErrorMessage =
                "Không thể lưu biến động kho. " +
                exception.GetBaseException().Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryCreateRequest(
        out InventoryAdjustmentRequest? request,
        out string validationMessage)
    {
        request = null;
        validationMessage = string.Empty;

        if (!_isInitialized)
        {
            validationMessage =
                "Dữ liệu sản phẩm chưa được tải.";

            return false;
        }

        if (!TryParseQuantity(
                QuantityText,
                out var quantity))
        {
            validationMessage =
                "Số lượng phải là một số nguyên hợp lệ.";

            return false;
        }

        if (!TryCalculatePreview(
                quantity,
                out _,
                out _,
                out validationMessage))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            validationMessage =
                "Vui lòng nhập lý do điều chỉnh kho.";

            return false;
        }

        var hasReferenceType =
            !string.IsNullOrWhiteSpace(
                ReferenceType);

        var hasReferenceId =
            !string.IsNullOrWhiteSpace(
                ReferenceId);

        if (hasReferenceType != hasReferenceId)
        {
            validationMessage =
                "Loại chứng từ và mã chứng từ phải được nhập cùng nhau.";

            return false;
        }

        try
        {
            request =
                new InventoryAdjustmentRequest(
                    ProductId,
                    SelectedMovement.Value,
                    quantity,
                    Reason,
                    ReferenceType,
                    ReferenceId);

            return true;
        }
        catch (
            Exception exception
        ) when (
            exception is
                ArgumentException or
                ArgumentOutOfRangeException)
        {
            validationMessage =
                exception.Message;

            return false;
        }
    }

    private void RefreshPreview()
    {
        if (!TryParseQuantity(
                QuantityText,
                out var quantity))
        {
            PreviewAfterText = "—";
            PreviewDeltaText =
                "Số lượng chưa hợp lệ";

            PreviewStateText =
                "Kiểm tra lại số lượng";

            PreviewIsNegative = false;

            return;
        }

        if (!TryCalculatePreview(
                quantity,
                out var quantityAfter,
                out var quantityDelta,
                out var validationMessage))
        {
            PreviewAfterText = "—";
            PreviewDeltaText =
                validationMessage;

            PreviewStateText =
                "Không thể thực hiện";

            PreviewIsNegative = false;

            return;
        }

        PreviewAfterText =
            $"{quantityAfter.ToString(
                "N0",
                VietnameseCulture)} {UnitName}";

        PreviewDeltaText =
            quantityDelta switch
            {
                > 0 =>
                    $"+{quantityDelta.ToString(
                        "N0",
                        VietnameseCulture)} {UnitName}",

                < 0 =>
                    $"{quantityDelta.ToString(
                        "N0",
                        VietnameseCulture)} {UnitName}",

                _ =>
                    $"Không chênh lệch"
            };

        PreviewIsNegative =
            quantityAfter < 0;

        PreviewStateText =
            quantityAfter switch
            {
                < 0 =>
                    "Tồn kho sau thao tác sẽ âm",

                0 =>
                    "Sản phẩm sẽ hết hàng",

                _ when quantityAfter <=
                       MinimumStock =>
                    "Tồn kho sau thao tác ở mức cảnh báo",

                _ =>
                    "Tồn kho sau thao tác ổn định"
            };
    }

    private bool TryCalculatePreview(
        int quantity,
        out int quantityAfter,
        out int quantityDelta,
        out string validationMessage)
    {
        quantityAfter = CurrentStock;
        quantityDelta = 0;
        validationMessage = string.Empty;

        long calculatedAfter;
        long calculatedDelta;

        switch (SelectedMovement.Value)
        {
            case InventoryMovementType.StockIn:
                if (quantity <= 0)
                {
                    validationMessage =
                        "Số lượng nhập phải lớn hơn 0.";

                    return false;
                }

                calculatedDelta = quantity;
                calculatedAfter =
                    (long)CurrentStock +
                    calculatedDelta;

                break;

            case InventoryMovementType.StockOut:
                if (quantity <= 0)
                {
                    validationMessage =
                        "Số lượng xuất phải lớn hơn 0.";

                    return false;
                }

                calculatedDelta =
                    -(long)quantity;

                calculatedAfter =
                    (long)CurrentStock +
                    calculatedDelta;

                break;

            case InventoryMovementType.Adjustment:
                if (quantity == 0)
                {
                    validationMessage =
                        "Mức điều chỉnh không được bằng 0.";

                    return false;
                }

                calculatedDelta = quantity;

                calculatedAfter =
                    (long)CurrentStock +
                    calculatedDelta;

                break;

            case InventoryMovementType.Stocktake:
                calculatedAfter = quantity;

                calculatedDelta =
                    calculatedAfter -
                    CurrentStock;

                break;

            default:
                validationMessage =
                    "Loại biến động tồn kho không hợp lệ.";

                return false;
        }

        if (calculatedAfter >
                BusinessRules.Products
                    .MaximumStockQuantity ||
            calculatedAfter <
                -BusinessRules.Products
                    .MaximumStockQuantity)
        {
            validationMessage =
                "Tồn kho sau thao tác vượt quá giới hạn hệ thống.";

            return false;
        }

        if (!AllowNegativeStock &&
            calculatedAfter < 0)
        {
            validationMessage =
                "Sản phẩm không cho phép tồn kho âm.";

            return false;
        }

        if (calculatedDelta >
                BusinessRules.Inventory
                    .MaximumQuantityDelta ||
            calculatedDelta <
                -BusinessRules.Inventory
                    .MaximumQuantityDelta)
        {
            validationMessage =
                "Mức thay đổi tồn kho vượt quá giới hạn hệ thống.";

            return false;
        }

        quantityAfter =
            checked((int)calculatedAfter);

        quantityDelta =
            checked((int)calculatedDelta);

        return true;
    }

    private static bool TryParseQuantity(
        string? value,
        out int quantity)
    {
        quantity = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized =
            value.Trim();

        const NumberStyles styles =
            NumberStyles.Integer |
            NumberStyles.AllowThousands;

        if (int.TryParse(
                normalized,
                styles,
                VietnameseCulture,
                out quantity))
        {
            return true;
        }

        return int.TryParse(
            normalized,
            styles,
            CultureInfo.InvariantCulture,
            out quantity);
    }

    private bool CanSave()
    {
        return _isInitialized &&
               !IsBusy &&
               !string.IsNullOrWhiteSpace(
                   Reason);
    }

    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    private void ClearError()
    {
        if (HasError)
        {
            ErrorMessage = string.Empty;
        }
    }

    private void HandleCommandException(
        Exception exception)
    {
        _logger.LogError(
            exception,
            "Lệnh điều chỉnh tồn kho không thể hoàn thành.");

        ErrorMessage =
            "Thao tác không thể hoàn thành. " +
            exception.GetBaseException().Message;
    }
}