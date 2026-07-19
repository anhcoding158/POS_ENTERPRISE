using POS.Application.Common;
using POS.Application.DTOs.Printing;

namespace POS.Application.Abstractions.Printing;

/// <summary>
/// In hóa đơn bán hàng.
///
/// Application không phụ thuộc PrintDialog,
/// FlowDocument hoặc API máy in của WPF.
/// </summary>
public interface IReceiptService
{
    Task<Result> PrintAsync(
        ReceiptRequest request,
        CancellationToken cancellationToken = default);
}