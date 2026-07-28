using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using POS.Application.DTOs.HeldSales;
using POS.Wpf.Services;

namespace POS.Wpf.Views;

public partial class HeldSaleResumeWindow : Window, INotifyPropertyChanged
{
    public HeldSaleResumeWindow(HeldSaleResumeDto heldSale)
    {
        InitializeComponent();
        Header = $"{heldSale.DisplayCode} • {heldSale.Label}";
        Lines = new(heldSale.Lines.Select(line => new ResumeRow(line, NotifyValidation)));
        DataContext = this;
        NotifyValidation();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Header { get; }
    public ObservableCollection<ResumeRow> Lines { get; }
    public HeldSaleResumeDialogResult? Result { get; private set; }

    public bool CanSubmit =>
        Lines.Any(line => line.Include) &&
        Lines.Where(line => line.Include).All(line => line.IsValid);

    public string ValidationMessage =>
        CanSubmit
            ? "Các dòng đã chọn sẽ dùng giá bán hiện tại."
            : "Hãy loại dòng không khả dụng, chỉnh số lượng hợp lệ và xác nhận mọi thay đổi giá.";

    private void NotifyValidation()
    {
        PropertyChanged?.Invoke(this, new(nameof(CanSubmit)));
        PropertyChanged?.Invoke(this, new(nameof(ValidationMessage)));
    }

    private void OnSubmit(object sender, RoutedEventArgs e)
    {
        if (!CanSubmit) return;
        Result = new(Lines.Select(line =>
            new HeldSaleResumeLineSelection(
                line.ProductId,
                line.Include,
                line.Quantity,
                line.CurrentPriceAccepted)).ToArray());
        DialogResult = true;
    }

    public sealed class ResumeRow : INotifyPropertyChanged
    {
        private readonly Action _changed;
        private bool _include;
        private int _quantity;
        private bool _currentPriceAccepted;

        public ResumeRow(HeldSaleResumeLineDto value, Action changed)
        {
            _changed = changed;
            ProductId = value.ProductId;
            ProductCode = value.CurrentProductCode ?? value.ProductCodeSnapshot;
            ProductName = value.CurrentProductName ?? value.ProductNameSnapshot;
            HeldQuantity = value.RequestedQuantity;
            _quantity = value.RequestedQuantity;
            SnapshotPriceText = $"{value.UnitPriceSnapshot:N0} ₫";
            CurrentPriceText = value.CurrentUnitPrice.HasValue
                ? $"{value.CurrentUnitPrice.Value:N0} ₫"
                : "—";
            CurrentStockText = value.CurrentStock?.ToString("N0") ?? "—";
            Warning = value.Warning ?? "Không thay đổi";
            IsUnavailable = value.Status == HeldSaleResumeLineStatus.Unavailable;
            PriceChanged = value.CurrentUnitPrice.HasValue &&
                value.CurrentUnitPrice.Value != value.UnitPriceSnapshot;
            TrackInventory = value.TrackInventory;
            AllowNegativeStock = value.AllowNegativeStock;
            MaximumQuantity = value.TrackInventory && !value.AllowNegativeStock
                ? Math.Max(value.CurrentStock ?? 0, 0)
                : POS.Domain.Constants.BusinessRules.Orders.MaximumLineQuantity;
            _include = !IsUnavailable &&
                value.Status != HeldSaleResumeLineStatus.InsufficientStock;
            _currentPriceAccepted = !PriceChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public int ProductId { get; }
        public string ProductCode { get; }
        public string ProductName { get; }
        public int HeldQuantity { get; }
        public string SnapshotPriceText { get; }
        public string CurrentPriceText { get; }
        public string CurrentStockText { get; }
        public string Warning { get; }
        public bool IsUnavailable { get; }
        public bool PriceChanged { get; }
        public bool TrackInventory { get; }
        public bool AllowNegativeStock { get; }
        public int MaximumQuantity { get; }

        public bool Include
        {
            get => _include;
            set
            {
                var normalized = !IsUnavailable && value;
                if (_include == normalized) return;
                _include = normalized;
                Notify();
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value) return;
                _quantity = value;
                Notify();
            }
        }

        public bool CurrentPriceAccepted
        {
            get => _currentPriceAccepted;
            set
            {
                var normalized = !PriceChanged || value;
                if (_currentPriceAccepted == normalized) return;
                _currentPriceAccepted = normalized;
                Notify();
            }
        }

        public bool IsValid =>
            !IsUnavailable &&
            Quantity > 0 &&
            Quantity <= MaximumQuantity &&
            (!PriceChanged || CurrentPriceAccepted);

        private void Notify([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new(propertyName));
            PropertyChanged?.Invoke(this, new(nameof(IsValid)));
            _changed();
        }
    }
}
