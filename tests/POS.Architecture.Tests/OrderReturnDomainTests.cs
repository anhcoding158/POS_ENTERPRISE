using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Services;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class OrderReturnDomainTests
{
    [Fact]
    public void Single_line_partial_return_must_use_cumulative_integer_allocation()
    {
        Assert.Equal(3, OrderReturnRefundAllocator.CalculateCurrentRefund(10, 3, 0, 0, 1));
    }

    [Fact]
    public void Second_partial_return_must_not_duplicate_rounding_remainder()
    {
        var first = OrderReturnRefundAllocator.CalculateCurrentRefund(10, 3, 0, 0, 1);
        Assert.Equal(3, OrderReturnRefundAllocator.CalculateCurrentRefund(10, 3, 1, first, 1));
    }

    [Fact]
    public void Final_partial_return_must_receive_exact_remaining_amount()
    {
        Assert.Equal(4, OrderReturnRefundAllocator.CalculateCurrentRefund(10, 3, 2, 6, 1));
    }

    [Fact]
    public void Multi_line_order_discount_allocation_must_equal_order_total()
    {
        var allocation = OrderReturnRefundAllocator.AllocateOrderTotal(
            999, [new(3, 2, 500), new(1, 1, 250), new(2, 1, 250)]);

        Assert.Equal(999, allocation.Values.Sum());
    }

    [Fact]
    public void Allocation_must_be_stable_regardless_of_request_line_order()
    {
        OrderReturnAllocationLine[] first = [new(2, 1, 50), new(1, 1, 50)];
        OrderReturnAllocationLine[] second = [new(1, 1, 50), new(2, 1, 50)];

        Assert.Equal(
            OrderReturnRefundAllocator.AllocateOrderTotal(99, first),
            OrderReturnRefundAllocator.AllocateOrderTotal(99, second));
    }

    [Fact]
    public void Allocation_must_not_overflow_for_supported_long_amounts()
    {
        var allocation = OrderReturnRefundAllocator.AllocateOrderTotal(
            long.MaxValue - 1,
            [new(1, int.MaxValue, long.MaxValue - 2), new(2, 1, 1)]);

        Assert.Equal(long.MaxValue - 1, allocation.Values.Sum());
    }

    [Fact]
    public void Full_return_across_multiple_documents_must_equal_original_total()
    {
        var first = OrderReturnRefundAllocator.CalculateCurrentRefund(101, 4, 0, 0, 1);
        var second = OrderReturnRefundAllocator.CalculateCurrentRefund(101, 4, 1, first, 2);
        var final = OrderReturnRefundAllocator.CalculateCurrentRefund(101, 4, 3, first + second, 1);

        Assert.Equal(101, first + second + final);
    }

    [Fact]
    public void Zero_or_negative_refundable_line_must_be_rejected_or_handled_by_existing_invariant()
    {
        Assert.Throws<DomainException>(() =>
            OrderReturnRefundAllocator.AllocateOrderTotal(
                1, [new(1, 1, -1), new(2, 1, 2)]));
        Assert.Throws<DomainException>(() =>
            OrderReturnRefundAllocator.CalculateCurrentRefund(0, 1, 0, 0, 1));
    }

    [Fact]
    public void OrderReturnDomain_partial_and_final_refund_must_be_deterministic()
    {
        var first = OrderReturnRefundAllocator.CalculateCurrentRefund(10, 3, 0, 0, 1);
        var second = OrderReturnRefundAllocator.CalculateCurrentRefund(10, 3, 1, first, 1);
        var final = OrderReturnRefundAllocator.CalculateCurrentRefund(10, 3, 2, first + second, 1);

        Assert.Equal(3, first);
        Assert.Equal(3, second);
        Assert.Equal(4, final);
        Assert.Equal(10, first + second + final);
    }

    [Fact]
    public void OrderReturnDomain_order_discount_allocation_must_equal_order_total()
    {
        var result = OrderReturnRefundAllocator.AllocateOrderTotal(
            99,
            [new(2, 1, 50), new(1, 1, 50)]);

        Assert.Equal(99, result.Values.Sum());
        Assert.Equal(49, result[1]);
        Assert.Equal(50, result[2]);
    }

    [Fact]
    public void OrderReturnDomain_balance_must_reject_over_return()
    {
        var balance = new OrderReturnBalance(1);
        balance.Register(1, 50, 2, 100);

        Assert.Throws<DomainException>(() =>
            balance.Register(2, 50, 2, 100));
    }

    [Fact]
    public void OrderReturnDomain_fingerprint_must_be_sha256_hex()
    {
        Assert.Throws<DomainException>(() =>
            new OrderReturn(
                Guid.NewGuid(), "bad", 1, 1, DateTimeOffset.UtcNow,
                "Lỗi", PaymentMethod.Cash, null,
                [new OrderReturnItem(1, 1, "P1", "Product", "Cái", 1, 1, 1)]));
    }

    [Fact]
    public void OrderReturnDomain_document_must_use_snapshots_and_be_append_only()
    {
        var line = new OrderReturnItem(1, 2, "P1", "Tên lúc bán", "Cái", 1, 0, 10);
        var document = new OrderReturn(
            Guid.NewGuid(), new string('A', 64), 1, 1, DateTimeOffset.UtcNow,
            "Hỏng", PaymentMethod.Cash, null, [line]);

        Assert.Equal("Tên lúc bán", Assert.Single(document.Items).ProductName);
        Assert.Equal(10, document.TotalRefundAmount);
        Assert.DoesNotContain(
            typeof(OrderReturn).GetMethods(),
            method => method.Name is "Update" or "Delete" or "Replace");
    }
}
