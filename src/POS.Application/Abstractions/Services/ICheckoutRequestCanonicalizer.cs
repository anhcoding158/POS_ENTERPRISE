using POS.Application.DTOs.Checkout;

namespace POS.Application.Abstractions.Services;

public sealed record CanonicalCheckoutRequest(string Json, string Fingerprint);

public interface ICheckoutRequestCanonicalizer
{
    CanonicalCheckoutRequest Canonicalize(CheckoutRequest request);
    CheckoutRequest Deserialize(string canonicalJson, Guid clientRequestId);
}
