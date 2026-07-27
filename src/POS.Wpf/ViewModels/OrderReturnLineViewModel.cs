using POS.Application.DTOs.Orders;

namespace POS.Wpf.ViewModels;

public sealed class OrderReturnLineViewModel(ReturnableOrderLineDto line) :
    ViewModelBase
{
    private int _returnQuantity;
    private int _restockQuantity;

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
            var constrainedValue = Math.Clamp(value, 0, RemainingQuantity);
            if (SetProperty(ref _returnQuantity, constrainedValue))
            {
                if (_restockQuantity > constrainedValue)
                    RestockQuantity = constrainedValue;
                OnPropertyChanged(nameof(PreviewRefundAmount));
            }
        }
    }
    public int RestockQuantity
    {
        get => _restockQuantity;
        set => SetProperty(
            ref _restockQuantity,
            TrackInventory ? Math.Clamp(value, 0, ReturnQuantity) : 0);
    }
    public long PreviewRefundAmount =>
        ReturnQuantity <= 0 || RemainingQuantity <= 0
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
