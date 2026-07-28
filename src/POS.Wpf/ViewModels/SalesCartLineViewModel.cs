using System.Globalization;
using POS.Domain.Constants;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// Một dòng trong giỏ hàng của màn hình bán hàng.
///
/// Giá tại đây chỉ là số tiền tạm tính để nhân viên xem.
/// CheckoutService sẽ đọc lại giá chính thức từ database.
/// </summary>
public sealed class SalesCartLineViewModel :
    ViewModelBase
{
    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private readonly Action<
        SalesCartLineViewModel>
        _changed;

    private readonly Action<
        SalesCartLineViewModel>
        _remove;

    private readonly Func<bool> _canMutate;
    private readonly Action _mutationBlocked;

    private int _quantity = 1;

    public SalesCartLineViewModel(
        SalesProductCardViewModel product,
        Action<SalesCartLineViewModel> changed,
        Action<SalesCartLineViewModel> remove,
        Func<bool> canMutate,
        Action mutationBlocked)
    {
        ArgumentNullException.ThrowIfNull(
            product);

        _changed =
            changed ??
            throw new ArgumentNullException(
                nameof(changed));

        _remove =
            remove ??
            throw new ArgumentNullException(
                nameof(remove));

        _canMutate = canMutate ??
            throw new ArgumentNullException(nameof(canMutate));
        _mutationBlocked = mutationBlocked ??
            throw new ArgumentNullException(nameof(mutationBlocked));

        ProductId = product.ProductId;
        ProductCode = product.Code;
        ProductName = product.Name;
        UnitName = product.UnitName;

        UnitSalePrice =
            product.SalePrice;

        StockQuantity =
            product.StockQuantity;

        TrackInventory =
            product.TrackInventory;

        AllowNegativeStock =
            product.AllowNegativeStock;

        IncreaseCommand =
            new AsyncRelayCommand(
                IncreaseAsync,
                () =>
                    CanIncrease && _canMutate());

        DecreaseCommand =
            new AsyncRelayCommand(
                DecreaseAsync,
                () =>
                    CanDecrease && _canMutate());

        RemoveCommand =
            new AsyncRelayCommand(
                RemoveAsync,
                _canMutate);
    }

    public SalesCartLineViewModel(
        int productId,
        string productCode,
        string productName,
        string unitName,
        long unitSalePrice,
        int stockQuantity,
        bool trackInventory,
        bool allowNegativeStock,
        int quantity,
        Action<SalesCartLineViewModel> changed,
        Action<SalesCartLineViewModel> remove,
        Func<bool> canMutate,
        Action mutationBlocked)
    {
        if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
        ProductId = productId;
        ProductCode = productCode ?? throw new ArgumentNullException(nameof(productCode));
        ProductName = productName ?? throw new ArgumentNullException(nameof(productName));
        UnitName = unitName ?? throw new ArgumentNullException(nameof(unitName));
        UnitSalePrice = unitSalePrice;
        StockQuantity = stockQuantity;
        TrackInventory = trackInventory;
        AllowNegativeStock = allowNegativeStock;
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        _remove = remove ?? throw new ArgumentNullException(nameof(remove));
        _canMutate = canMutate ?? throw new ArgumentNullException(nameof(canMutate));
        _mutationBlocked = mutationBlocked ?? throw new ArgumentNullException(nameof(mutationBlocked));
        if (quantity <= 0 || quantity > MaximumQuantity)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        _quantity = quantity;
        IncreaseCommand = new AsyncRelayCommand(IncreaseAsync, () => CanIncrease && _canMutate());
        DecreaseCommand = new AsyncRelayCommand(DecreaseAsync, () => CanDecrease && _canMutate());
        RemoveCommand = new AsyncRelayCommand(RemoveAsync, _canMutate);
    }

    public int ProductId { get; }

    public string ProductCode { get; }

    public string ProductName { get; }

    public string UnitName { get; }

    public long UnitSalePrice { get; }

    public int StockQuantity { get; }

    public bool TrackInventory { get; }

    public bool AllowNegativeStock { get; }

    public AsyncRelayCommand
        IncreaseCommand
    {
        get;
    }

    public AsyncRelayCommand
        DecreaseCommand
    {
        get;
    }

    public AsyncRelayCommand
        RemoveCommand
    {
        get;
    }

    public int Quantity
    {
        get => _quantity;

        private set
        {
            if (!SetProperty(
                    ref _quantity,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(LineTotal));

            OnPropertyChanged(
                nameof(LineTotalText));

            OnPropertyChanged(
                nameof(QuantityText));

            OnPropertyChanged(
                nameof(CanIncrease));

            OnPropertyChanged(
                nameof(CanDecrease));

            IncreaseCommand
                .NotifyCanExecuteChanged();

            DecreaseCommand
                .NotifyCanExecuteChanged();

            _changed(this);
        }
    }

    public int MaximumQuantity
    {
        get
        {
            if (!TrackInventory ||
                AllowNegativeStock)
            {
                return
                    BusinessRules.Orders
                        .MaximumLineQuantity;
            }

            return Math.Min(
                Math.Max(
                    StockQuantity,
                    0),

                BusinessRules.Orders
                    .MaximumLineQuantity);
        }
    }

    public bool CanIncrease =>
        Quantity < MaximumQuantity;

    public bool CanDecrease =>
        Quantity > 1;

    public decimal LineTotal =>
        (decimal)UnitSalePrice *
        Quantity;

    public string QuantityText =>
        Quantity.ToString(
            "N0",
            VietnameseCulture);

    public string UnitPriceText =>
        $"{UnitSalePrice.ToString(
            "N0",
            VietnameseCulture)} ₫";

    public string LineTotalText =>
        $"{LineTotal.ToString(
            "N0",
            VietnameseCulture)} ₫";

    public string QuantityLimitText =>
        TrackInventory &&
        !AllowNegativeStock
            ? $"Tối đa {MaximumQuantity:N0}"
            : "Theo giới hạn đơn";

    public bool TryIncrease()
    {
        if (!_canMutate())
        {
            _mutationBlocked();
            return false;
        }

        if (!CanIncrease)
        {
            return false;
        }

        Quantity++;

        return true;
    }

    private Task IncreaseAsync()
    {
        TryIncrease();

        return Task.CompletedTask;
    }

    private Task DecreaseAsync()
    {
        if (!_canMutate())
        {
            _mutationBlocked();
            return Task.CompletedTask;
        }

        if (CanDecrease)
        {
            Quantity--;
        }

        return Task.CompletedTask;
    }

    private Task RemoveAsync()
    {
        if (!_canMutate())
        {
            _mutationBlocked();
            return Task.CompletedTask;
        }

        _remove(this);

        return Task.CompletedTask;
    }
}
