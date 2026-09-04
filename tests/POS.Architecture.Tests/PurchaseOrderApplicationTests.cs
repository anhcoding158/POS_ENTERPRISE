using POS.Application.Abstractions.Services;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PurchaseOrderApplicationTests
{
    [Fact]
    public void R62A_contract_exposes_purchase_order_operations_without_delete_or_receiving_api()
    {
        var methodNames = typeof(IPurchaseOrderService)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        Assert.Contains(nameof(IPurchaseOrderService.SearchAsync), methodNames);
        Assert.Contains(nameof(IPurchaseOrderService.GetByIdAsync), methodNames);
        Assert.Contains(nameof(IPurchaseOrderService.CreateDraftAsync), methodNames);
        Assert.Contains(nameof(IPurchaseOrderService.UpdateDraftAsync), methodNames);
        Assert.Contains(nameof(IPurchaseOrderService.MarkOrderedAsync), methodNames);
        Assert.Contains(nameof(IPurchaseOrderService.AmendOrderedAsync), methodNames);
        Assert.Contains(nameof(IPurchaseOrderService.CancelAsync), methodNames);
        Assert.DoesNotContain(methodNames, name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("Receipt", StringComparison.OrdinalIgnoreCase));
    }
}
