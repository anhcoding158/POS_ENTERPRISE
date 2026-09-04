using POS.Domain.Common;
using POS.Domain.Entities;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SupplierDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Supplier_normalizes_code_and_optional_whitespace()
    {
        var supplier = new Supplier("  ncc-001  ", "  Công ty A  ", "  ", "  ", "  ", "  ", "  ", "  ", Now);
        Assert.Equal("ncc-001", supplier.Code);
        Assert.Equal("NCC-001", supplier.NormalizedCode);
        Assert.Null(supplier.TaxCode);
        Assert.Null(supplier.ContactName);
        Assert.True(supplier.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("NCC\n01")]
    [InlineData("NCC/01")]
    public void Invalid_code_is_rejected(string code) =>
        Assert.Throws<DomainException>(() =>
            new Supplier(code, "Nhà cung cấp", null, null, null, null, null, null, Now));

    [Fact]
    public void Required_and_optional_control_characters_are_rejected()
    {
        Assert.Throws<DomainException>(() => new Supplier("NCC01", "Tên\nnhà cung cấp", null, null, null, null, null, null, Now));
        Assert.Throws<DomainException>(() => new Supplier("NCC01", "Tên", null, "Người\tliên hệ", null, null, null, null, Now));
    }

    [Fact]
    public void State_changes_are_idempotent_and_no_hard_delete_contract_exists()
    {
        var supplier = new Supplier("NCC01", "Nhà cung cấp", null, null, null, null, null, null, Now);
        supplier.Deactivate(Now.AddMinutes(1));
        var changedAt = supplier.UpdatedAtUtc;
        supplier.Deactivate(Now.AddMinutes(2));
        Assert.False(supplier.IsActive);
        Assert.Equal(changedAt, supplier.UpdatedAtUtc);
        Assert.DoesNotContain(typeof(Supplier).GetMethods(), method => method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }
}
