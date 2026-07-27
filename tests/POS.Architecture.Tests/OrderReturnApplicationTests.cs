using POS.Application.Authorization;
using POS.Application.DTOs.Orders;
using POS.Application.Services;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class OrderReturnApplicationTests
{
    [Fact]
    public void Same_request_with_different_line_order_must_have_same_fingerprint()
    {
        var request = Request();
        var reordered = request with { Lines = request.Lines.Reverse().ToArray() };
        Assert.Equal(
            OrderReturnService.ComputeFingerprint(request),
            OrderReturnService.ComputeFingerprint(reordered));
    }

    [Fact]
    public void Whitespace_normalization_must_produce_same_fingerprint()
    {
        var request = Request();
        var spaced = request with { Reason = "  hàng   lỗi ", RefundReference = " REF   1 " };
        Assert.Equal(
            OrderReturnService.ComputeFingerprint(request),
            OrderReturnService.ComputeFingerprint(spaced));
    }

    [Theory]
    [InlineData(8, 1, 0, PaymentMethod.Cash)]
    [InlineData(7, 2, 0, PaymentMethod.Cash)]
    [InlineData(7, 1, 1, PaymentMethod.Cash)]
    [InlineData(7, 1, 0, PaymentMethod.VietQr)]
    public void Changed_canonical_payload_must_produce_different_fingerprint(
        int orderId,
        int quantity,
        int restockQuantity,
        PaymentMethod method)
    {
        var request = Request();
        var changed = request with
        {
            OrderId = orderId,
            RefundMethod = method,
            Lines = [new(1, quantity, restockQuantity), new(2, 1, 0)]
        };
        Assert.NotEqual(
            OrderReturnService.ComputeFingerprint(request),
            OrderReturnService.ComputeFingerprint(changed));
    }

    [Fact]
    public void OrderReturnApplication_fingerprint_must_be_canonical_and_ignore_line_order()
    {
        var id = Guid.NewGuid();
        var first = new OrderReturnRequest(
            id, 7, "  hàng   lỗi ", PaymentMethod.Cash, " REF  1 ",
            [new(2, 1, 0), new(1, 2, 1)]);
        var second = new OrderReturnRequest(
            id, 7, "hàng lỗi", PaymentMethod.Cash, "REF 1",
            [new(1, 2, 1), new(2, 1, 0)]);

        var firstHash = OrderReturnService.ComputeFingerprint(first);
        var secondHash = OrderReturnService.ComputeFingerprint(second);

        Assert.Equal(firstHash, secondHash);
        Assert.Equal(64, firstHash.Length);
        Assert.All(firstHash, character => Assert.True(Uri.IsHexDigit(character)));
    }

    [Fact]
    public void OrderReturnApplication_fingerprint_must_detect_changed_payload()
    {
        var request = new OrderReturnRequest(
            Guid.NewGuid(), 7, "Lỗi", PaymentMethod.Cash, null,
            [new(1, 1, 0)]);
        var changed = request with
        {
            Lines = [new OrderReturnLineRequest(1, 1, 1)]
        };

        Assert.NotEqual(
            OrderReturnService.ComputeFingerprint(request),
            OrderReturnService.ComputeFingerprint(changed));
    }

    [Fact]
    public void OrderReturnApplication_permission_policy_must_match_checkpoint()
    {
        Assert.True(RolePermissionPolicy.HasPermission(Role.Administrator, SystemPermission.ProcessReturns));
        Assert.True(RolePermissionPolicy.HasPermission(Role.Manager, SystemPermission.ProcessReturns));
        Assert.False(RolePermissionPolicy.HasPermission(Role.Cashier, SystemPermission.ProcessReturns));
        Assert.False(RolePermissionPolicy.HasPermission(Role.InventoryStaff, SystemPermission.ProcessReturns));
    }

    [Fact]
    public void OrderReturnApplication_dto_must_not_accept_server_computed_fields()
    {
        var names = typeof(OrderReturnRequest).GetProperties().Select(property => property.Name);
        Assert.DoesNotContain("RefundAmount", names);
        Assert.DoesNotContain("TotalRefundAmount", names);
        Assert.DoesNotContain("ProcessedByUserId", names);
        Assert.DoesNotContain("RequestFingerprint", names);
    }

    private static OrderReturnRequest Request() =>
        new(
            Guid.NewGuid(),
            7,
            "hàng lỗi",
            PaymentMethod.Cash,
            "REF 1",
            [new(1, 1, 0), new(2, 1, 0)]);
}
