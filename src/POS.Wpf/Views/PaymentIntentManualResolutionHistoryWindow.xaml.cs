using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using POS.Application.DTOs.Payments;
using POS.Domain.Enums;

namespace POS.Wpf.Views;

public partial class PaymentIntentManualResolutionHistoryWindow : Window
{
    private readonly IReadOnlyList<PaymentIntentManualResolutionDto> _source;
    private readonly ObservableCollection<HistoryRow> _rows = [];

    public PaymentIntentManualResolutionHistoryWindow(
        IReadOnlyList<PaymentIntentManualResolutionDto> history)
    {
        InitializeComponent();
        _source = history ?? [];
        HistoryGrid.ItemsSource = _rows;
        ApplyFilter();
    }

    private void OnFilterChanged(object sender, EventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (HistoryGrid is null || ResolutionTypeInput is null ||
            ReferenceSearchInput is null)
            return;

        PaymentIntentManualResolutionType? type = null;
        var tag = (ResolutionTypeInput.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            type = (PaymentIntentManualResolutionType)value;

        var from = FromDateInput.SelectedDate;
        var toExclusive = ToDateInput.SelectedDate?.AddDays(1);
        var search = ReferenceSearchInput.Text.Trim();
        var filtered = _source.Where(item =>
        {
            var local = item.ResolvedAtUtc.ToLocalTime();
            return (!from.HasValue || local.Date >= from.Value.Date) &&
                   (!toExclusive.HasValue || local.Date < toExclusive.Value.Date) &&
                   (!type.HasValue || item.ResolutionType == type.Value) &&
                   (search.Length == 0 ||
                    item.DisplayCode.Contains(search, StringComparison.OrdinalIgnoreCase));
        }).Select(HistoryRow.From);

        _rows.Clear();
        foreach (var row in filtered)
            _rows.Add(row);
    }

    private sealed record HistoryRow(
        string DisplayCode,
        string ResolutionTypeText,
        string ResolvedAtLocalText,
        string ResolvedByText,
        string Reason,
        string? ExternalReference,
        string? LinkedOrderText,
        string AmountText)
    {
        public static HistoryRow From(PaymentIntentManualResolutionDto item)
        {
            var local = item.ResolvedAtUtc.ToLocalTime();
            return new(
                item.DisplayCode,
                item.ResolutionType switch
                {
                    PaymentIntentManualResolutionType.LinkExistingOrder => "Liên kết hóa đơn",
                    PaymentIntentManualResolutionType.NoRealMoneyTestTransaction => "Giao dịch thử nghiệm",
                    PaymentIntentManualResolutionType.RefundedExternally => "Hoàn tiền bên ngoài",
                    _ => item.ResolutionType.ToString()
                },
                local.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN")),
                $"User #{item.ResolvedByUserId}",
                item.Reason,
                item.ExternalReference,
                item.LinkedOrderCode,
                item.Amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")));
        }
    }
}
