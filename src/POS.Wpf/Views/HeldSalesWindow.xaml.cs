using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using POS.Application.DTOs.HeldSales;
using POS.Wpf.Services;

namespace POS.Wpf.Views;

public partial class HeldSalesWindow : Window, INotifyPropertyChanged
{
    private readonly IReadOnlyList<HeldSaleRow> _allItems;
    private string _searchText = string.Empty;
    private HeldSaleRow? _selectedItem;

    public HeldSalesWindow(IReadOnlyList<HeldSaleDto> heldSales)
    {
        InitializeComponent();
        _allItems = heldSales.Select(value => new HeldSaleRow(value)).ToArray();
        FilteredItems = new(_allItems);
        SelectedItem = FilteredItems.FirstOrDefault();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<HeldSaleRow> FilteredItems { get; }
    public HeldSaleListDialogResult? Result { get; private set; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value ?? string.Empty;
            ApplyFilter();
            OnPropertyChanged();
        }
    }

    public HeldSaleRow? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem == value) return;
            _selectedItem = value;
            OnPropertyChanged();
        }
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        var selectedId = SelectedItem?.Id;
        FilteredItems.Clear();
        foreach (var item in _allItems.Where(item =>
                     search.Length == 0 ||
                     item.DisplayCode.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                     item.Label.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                     item.NotesPreview.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            FilteredItems.Add(item);
        SelectedItem = FilteredItems.FirstOrDefault(item => item.Id == selectedId)
            ?? FilteredItems.FirstOrDefault();
    }

    private void OnResume(object sender, RoutedEventArgs e) =>
        Complete(HeldSaleListAction.Resume);

    private void OnCancelHeld(object sender, RoutedEventArgs e) =>
        Complete(HeldSaleListAction.Cancel);

    private void Complete(HeldSaleListAction action)
    {
        if (SelectedItem is null) return;
        Result = new(action, SelectedItem.Id);
        DialogResult = true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new(name));

    public sealed class HeldSaleRow
    {
        public HeldSaleRow(HeldSaleDto value)
        {
            Id = value.Id;
            DisplayCode = value.DisplayCode;
            Label = value.Label;
            CreatedBy = value.CreatedBy;
            LineCount = value.Lines.Count;
            TotalQuantity = value.TotalQuantity;
            UpdatedAtText = value.UpdatedAtUtc.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
            TotalText = $"{value.TotalSnapshot:N0} ₫";
            NotesPreview = value.Notes ?? string.Empty;
        }

        public int Id { get; }
        public string DisplayCode { get; }
        public string Label { get; }
        public string CreatedBy { get; }
        public int LineCount { get; }
        public int TotalQuantity { get; }
        public string UpdatedAtText { get; }
        public string TotalText { get; }
        public string NotesPreview { get; }
    }
}
