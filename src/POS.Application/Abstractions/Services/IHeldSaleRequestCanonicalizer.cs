using POS.Application.DTOs.HeldSales;

namespace POS.Application.Abstractions.Services;

public sealed record CanonicalHeldSaleRequest(string Json, string Fingerprint);

public interface IHeldSaleRequestCanonicalizer
{
    CanonicalHeldSaleRequest Canonicalize(CreateHeldSaleRequest request);
}
