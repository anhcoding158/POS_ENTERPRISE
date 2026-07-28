using POS.Application.DTOs.HeldSales;
using POS.Application.Services;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSaleCanonicalTests
{
    private readonly HeldSaleRequestCanonicalizer _canonicalizer = new();

    [Fact]
    public void Reordered_lines_produce_same_fingerprint()
    {
        var id = Guid.NewGuid();
        var first = Request(id, [new(2, 1), new(1, 3)]);
        var second = Request(id, [new(1, 3), new(2, 1)]);
        Assert.Equal(_canonicalizer.Canonicalize(first).Fingerprint,
            _canonicalizer.Canonicalize(second).Fingerprint);
    }

    [Fact]
    public void Different_quantity_changes_fingerprint()
    {
        var id = Guid.NewGuid();
        Assert.NotEqual(
            _canonicalizer.Canonicalize(Request(id, [new(1, 1)])).Fingerprint,
            _canonicalizer.Canonicalize(Request(id, [new(1, 2)])).Fingerprint);
    }

    [Fact]
    public void Whitespace_normalization_is_deterministic()
    {
        var id = Guid.NewGuid();
        var first = Request(id, [new(1, 1, "  ít   đá ")], "  Khách   A ", " ghi  chú ");
        var second = Request(id, [new(1, 1, "ít đá")], "Khách A", "ghi chú");
        Assert.Equal(_canonicalizer.Canonicalize(first),
            _canonicalizer.Canonicalize(second));
    }

    [Fact]
    public void Fingerprint_contains_no_cost_or_secrets()
    {
        var canonical = _canonicalizer.Canonicalize(
            Request(Guid.NewGuid(), [new(1, 1)]));
        Assert.Matches("^[0-9A-F]{64}$", canonical.Fingerprint);
        Assert.DoesNotContain("Cost", canonical.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", canonical.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", canonical.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClientRequestId", canonical.Json, StringComparison.OrdinalIgnoreCase);
    }

    private static CreateHeldSaleRequest Request(
        Guid id,
        IReadOnlyList<CreateHeldSaleLineRequest> lines,
        string? label = "Đơn giữ",
        string? notes = null) =>
        new(id, label, notes, lines);
}
