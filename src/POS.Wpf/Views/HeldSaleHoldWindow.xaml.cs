using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using POS.Wpf.Services;

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
    {
        InitializeComponent();
        _clientRequestId = clientRequestId;
        _label = $"Đơn giữ {DateTime.Now:HH:mm}";
        Summary = $"{lineCount:N0} loại sản phẩm • {totalQuantity:N0} sản phẩm • " +
            $"{totalSnapshot.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫";
        DataContext = this;
        Loaded += (_, _) => LabelTextBox.Focus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public HeldSaleHoldDialogResult? Result { get; private set; }
    public string Summary { get; }

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
