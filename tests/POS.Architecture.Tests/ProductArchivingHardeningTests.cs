using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductArchivingHardeningTests
{
    [Fact]
    public void Sales_search_must_explicitly_exclude_archived_products()
    {
        var source = Read("src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");

        Assert.Contains("isActive:\n                        true", Normalize(source));
        Assert.Contains("isArchived:\n                        false", Normalize(source));
    }

    [Theory]
    [InlineData("Checkout_must_reject_archived_product", "CheckoutService.cs",
        "if (product.IsArchived)")]
    [InlineData("Checkout_must_reject_archived_product_even_when_is_active_is_inconsistent",
        "CheckoutService.cs", "ErrorCodes.Products.Archived")]
    [InlineData("Stale_cart_checkout_must_fail_after_product_is_archived",
        "CheckoutService.cs", "không thể thanh toán")]
    [InlineData("Concurrent_archive_and_checkout_must_not_create_successful_order_with_archived_product",
        "CheckoutService.cs", "PersistenceConflictKind.Concurrency")]
    [InlineData("Update_must_reject_archived_product_without_changing_fields",
        "ProductService.cs", "Hãy khôi phục trước khi chỉnh sửa")]
    [InlineData("Restore_must_allow_update_again_but_keep_product_inactive",
        "ProductService.cs", "product.Restore(")]
    [InlineData("Inventory_adjustment_must_reject_archived_product",
        "InventoryService.cs", "Không thể điều chỉnh kho cho sản phẩm đã lưu trữ")]
    [InlineData("Inventory_history_must_still_be_readable_for_archived_product",
        "InventoryService.cs", "public async Task<\n        Result<PagedResult<InventoryMovementDto>>>")]
    [InlineData("Set_active_must_preserve_archive_metadata_on_failure",
        "ProductService.cs", "product.Activate(")]
    public void Application_archive_contracts(
        string contract,
        string fileName,
        string expected)
    {
        _ = contract;
        var source = Read("src", "POS.Application", "Services", fileName);

        Assert.Contains(expected, Normalize(source));
    }

    private static string Read(params string[] path)
    {
        return File.ReadAllText(
            Path.Combine([RepositoryLocator.Root, .. path]));
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);
}