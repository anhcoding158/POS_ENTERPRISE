using System.Windows;
using POS.Application.DTOs.HeldSales;
using POS.Domain.Enums;
using POS.Wpf.Views;

namespace POS.Wpf.Services;

public sealed class HeldSaleDialogService : IHeldSaleDialogService
{
    public HeldSaleHoldDialogResult? ShowHold(
        int lineCount,
        int totalQuantity,
        long subtotal,
        long discountAmount,
        long totalSnapshot,
        SalesDiscountType discountType,
        long requestedDiscountValue,
        Guid clientRequestId)
    {
        var window = new HeldSaleHoldWindow(
            lineCount, totalQuantity, subtotal, discountAmount, totalSnapshot,
            discountType, requestedDiscountValue, clientRequestId)
        {
            Owner = System.Windows.Application.Current?.Windows
                .OfType<Window>().FirstOrDefault(value => value.IsActive)
        };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public HeldSaleListDialogResult? ShowActiveList(
        IReadOnlyList<HeldSaleDto> heldSales)
    {
        var window = new HeldSalesWindow(heldSales)
        {
            Owner = System.Windows.Application.Current?.Windows
                .OfType<Window>().FirstOrDefault(value => value.IsActive)
        };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public HeldSaleResumeDialogResult? ShowResumeReview(
        HeldSaleResumeDto heldSale)
    {
        var window = new HeldSaleResumeWindow(heldSale)
        {
            Owner = System.Windows.Application.Current?.Windows
                .OfType<Window>().FirstOrDefault(value => value.IsActive)
        };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public bool ConfirmCancel() =>
        MessageBox.Show(
            "Đơn giữ này chưa được thanh toán. Hủy đơn giữ?",
            "Hủy đơn giữ",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
}

internal sealed class NullHeldSaleDialogService : IHeldSaleDialogService
{
    public HeldSaleHoldDialogResult? ShowHold(
        int lineCount, int totalQuantity, long subtotal, long discountAmount,
        long totalSnapshot, SalesDiscountType discountType,
        long requestedDiscountValue, Guid clientRequestId) => null;
    public HeldSaleListDialogResult? ShowActiveList(IReadOnlyList<HeldSaleDto> heldSales) => null;
    public HeldSaleResumeDialogResult? ShowResumeReview(HeldSaleResumeDto heldSale) => null;
    public bool ConfirmCancel() => false;
}
