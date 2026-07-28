using POS.Application.Common;
using POS.Application.DTOs.HeldSales;

namespace POS.Application.Abstractions.Services;

public interface IHeldSaleService
{
    Task<Result<HeldSaleDto>> CreateHeldSaleAsync(
        CreateHeldSaleRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<HeldSaleDto>>> GetActiveHeldSalesAsync(
        int limit = 100, CancellationToken cancellationToken = default);
    Task<Result<HeldSaleResumeDto>> GetHeldSaleForResumeAsync(
        int heldSaleId, CancellationToken cancellationToken = default);
    Task<Result> CancelHeldSaleAsync(
        int heldSaleId, CancellationToken cancellationToken = default);
}
