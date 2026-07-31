using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using POS.Wpf.Services;
using POS.Domain.Enums;

namespace POS.Wpf.Views;

public partial class HeldSaleHoldWindow : Window, INotifyPropertyChanged
{
    private string _label;
    private string _notes = string.Empty;
    private readonly Guid _clientRequestId;

    public HeldSaleHoldWindow(
        int lineCount,
        int totalQuantity,
        long totalSnapshot,
        Guid clientRequestId)
        : this(
            lineCount,
            totalQuantity,
            totalSnapshot,
            0,
            totalSnapshot,
            SalesDiscountType.None,
            0,
            clientRequestId)
    {
    }

    public HeldSaleHoldWindow(
        int lineCount,
        int totalQuantity,
        long subtotal,
        long discountAmount,
        long totalSnapshot,
        SalesDiscountType discountType,
        long requestedDiscountValue,
        Guid clientRequestId)
    {
        InitializeComponent();
        _clientRequestId = clientRequestId;
        _label = $"Đơn giữ {DateTime.Now:HH:mm}";
        ItemSummary = $"{lineCount:N0} loại sản phẩm · {totalQuantity:N0} sản phẩm";
        HasDiscount = discountType != SalesDiscountType.None && discountAmount > 0;
        SubtotalText = SalesDiscountPresentationFormatter.FormatMoney(subtotal);
        DiscountLabel = HasDiscount
            ? $"Giảm giá {SalesDiscountPresentationFormatter.FormatRequestedValue(discountType, requestedDiscountValue)}"
            : "Giảm giá";
        DiscountText = $"-{SalesDiscountPresentationFormatter.FormatMoney(discountAmount)}";
        TotalText = SalesDiscountPresentationFormatter.FormatMoney(totalSnapshot);
        DataContext = this;
        Loaded += (_, _) => LabelTextBox.Focus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public HeldSaleHoldDialogResult? Result { get; private set; }
    public string ItemSummary { get; }
    public bool HasDiscount { get; }
    public string SubtotalText { get; }
    public string DiscountLabel { get; }
    public string DiscountText { get; }
    public string TotalText { get; }

    public string Label
    {
        get => _label;
        set
        {
            if (_label == value) return;
            _label = value;
            PropertyChanged?.Invoke(this, new(nameof(Label)));
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            if (_notes == value) return;
            _notes = value;
            PropertyChanged?.Invoke(this, new(nameof(Notes)));
        }
    }

    private void OnHold(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            LabelTextBox.Focus();
            return;
        }
        Result = new(_clientRequestId, Label.Trim(),
            string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());
        DialogResult = true;
    }
}
