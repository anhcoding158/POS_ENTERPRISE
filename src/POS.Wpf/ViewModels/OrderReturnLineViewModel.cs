using POS.Application.DTOs.Orders;
using System.Globalization;

namespace POS.Wpf.ViewModels;

public sealed class OrderReturnLineViewModel(ReturnableOrderLineDto line) :
    ViewModelBase
{
    private int _returnQuantity;
    private int _restockQuantity;
    private string _returnQuantityText = string.Empty;
    private string _restockQuantityText = string.Empty;

    public int OrderItemId => line.OrderItemId;
    public string ProductCode => line.ProductCode;
    public string ProductName => line.ProductName;
    public string UnitName => line.UnitName;
    public int SoldQuantity => line.SoldQuantity;
    public int ReturnedQuantity => line.ReturnedQuantity;
    public int RemainingQuantity => line.RemainingQuantity;
    public bool TrackInventory => line.TrackInventory;
    public bool IsArchived => line.IsArchived;
    public long RemainingRefundableAmount => line.RemainingRefundableAmount;
    public int ReturnQuantity
    {
        get => _returnQuantity;
        set
        {
            if (SetProperty(ref _returnQuantity, value))
            {
                var text = value > 0
                    ? value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
                SetProperty(ref _returnQuantityText, text, nameof(ReturnQuantityText));
                OnPropertyChanged(nameof(PreviewRefundAmount));
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }
    public string ReturnQuantityText
    {
        get => _returnQuantityText;
        set
        {
            value ??= string.Empty;
            if (!SetProperty(ref _returnQuantityText, value)) return;
            _returnQuantity = string.IsNullOrWhiteSpace(value)
                ? 0
                : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : -1;
            OnPropertyChanged(nameof(ReturnQuantity));
            OnPropertyChanged(nameof(PreviewRefundAmount));
            OnPropertyChanged(nameof(IsValid));
        }
    }
    public int RestockQuantity
    {
        get => _restockQuantity;
        set
        {
            var next = TrackInventory ? value : 0;
            if (!SetProperty(ref _restockQuantity, next)) return;
            var text = next > 0
                ? next.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            SetProperty(ref _restockQuantityText, text, nameof(RestockQuantityText));
            OnPropertyChanged(nameof(IsValid));
        }
    }
    public string RestockQuantityText
    {
        get => _restockQuantityText;
        set
        {
            value ??= string.Empty;
            if (!SetProperty(ref _restockQuantityText, value)) return;
            _restockQuantity = string.IsNullOrWhiteSpace(value)
                ? 0
                : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : -1;
            OnPropertyChanged(nameof(RestockQuantity));
            OnPropertyChanged(nameof(IsValid));
        }
    }
    public long PreviewRefundAmount =>
        ReturnQuantity <= 0 || ReturnQuantity > RemainingQuantity || RemainingQuantity <= 0
            ? 0
            : (long)((System.Numerics.BigInteger)RemainingRefundableAmount *
                ReturnQuantity / RemainingQuantity);
    public bool IsValid =>
        ReturnQuantity is >= 0 &&
        ReturnQuantity <= RemainingQuantity &&
        RestockQuantity is >= 0 &&
        RestockQuantity <= ReturnQuantity &&
        (TrackInventory || RestockQuantity == 0);
}
