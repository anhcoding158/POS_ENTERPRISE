using POS.Application.DTOs.HeldSales;

namespace POS.Wpf.Services;

public sealed record HeldSaleHoldDialogResult(
    Guid ClientRequestId,
    string Label,
    string? Notes);

public enum HeldSaleListAction
{
    Resume = 1,
    Cancel = 2
}

public sealed record HeldSaleListDialogResult(
    HeldSaleListAction Action,
    int HeldSaleId);

public sealed record HeldSaleResumeLineSelection(
    int ProductId,
    bool Include,
    int Quantity,
    bool CurrentPriceAccepted);

public sealed record HeldSaleResumeDialogResult(
    IReadOnlyList<HeldSaleResumeLineSelection> Lines);

public interface IHeldSaleDialogService
{
    HeldSaleHoldDialogResult? ShowHold(
        int lineCount,
        int totalQuantity,
        long totalSnapshot,
        Guid clientRequestId);

    HeldSaleListDialogResult? ShowActiveList(
        IReadOnlyList<HeldSaleDto> heldSales);

    HeldSaleResumeDialogResult? ShowResumeReview(
        HeldSaleResumeDto heldSale);

    bool ConfirmCancel();
}
