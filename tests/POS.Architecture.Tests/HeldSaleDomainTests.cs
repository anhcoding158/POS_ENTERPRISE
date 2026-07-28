using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSaleDomainTests
{
    [Fact]
    public void HeldSaleDomain_active_sale_has_immutable_price_snapshot_and_total()
    {
        var sale = Create();

        Assert.Equal(HeldSaleStatus.Active, sale.Status);
        Assert.Equal(100_000, sale.TotalSnapshot);
        Assert.Equal(2, Assert.Single(sale.Lines).Quantity);
        Assert.Null(sale.CompletedOrderId);
    }

    [Fact]
    public void HeldSaleDomain_complete_links_order_once_and_blocks_cancel()
    {
        var sale = Create();
        sale.Complete(42, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(HeldSaleStatus.Completed, sale.Status);
        Assert.Equal(42, sale.CompletedOrderId);
        Assert.NotNull(sale.CompletedAtUtc);
        Assert.Throws<DomainException>(() =>
            sale.Cancel(DateTimeOffset.UtcNow.AddMinutes(2)));
    }

    [Fact]
    public void HeldSaleDomain_cancel_blocks_completion()
    {
        var sale = Create();
        sale.Cancel(DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(HeldSaleStatus.Cancelled, sale.Status);
        Assert.NotNull(sale.CancelledAtUtc);
        Assert.Throws<DomainException>(() =>
            sale.Complete(42, DateTimeOffset.UtcNow.AddMinutes(2)));
    }

    [Fact]
    public void HeldSaleDomain_rejects_empty_request_id_and_empty_lines()
    {
        Assert.Throws<DomainException>(() => New(Guid.Empty, Lines()));
        Assert.Throws<DomainException>(() => New(Guid.NewGuid(), []));
    }

    [Theory]
    [InlineData(0, 50_000)]
    [InlineData(-1, 50_000)]
    [InlineData(1, -1)]
    public void HeldSaleDomain_rejects_invalid_line_values(int quantity, long price)
    {
        Assert.Throws<DomainException>(() =>
            New(Guid.NewGuid(),
                [(1, "P1", null, "Cà phê", quantity, price, 0, null)]));
    }

    private static HeldSale Create() => New(Guid.NewGuid(), Lines());

    private static HeldSale New(
        Guid requestId,
        IEnumerable<(int ProductId, string Code, string? Barcode, string Name,
            int Quantity, long UnitPrice, int SortOrder, string? Notes)> lines) =>
        new(requestId, new string('A', 64), "G260728-001", "Khách áo xanh",
            "Ghi chú", 1, DateTimeOffset.UtcNow, lines);

    private static (int ProductId, string Code, string? Barcode, string Name,
        int Quantity, long UnitPrice, int SortOrder, string? Notes)[] Lines() =>
        [(1, "P1", "8930001", "Cà phê", 2, 50_000, 0, null)];
}
