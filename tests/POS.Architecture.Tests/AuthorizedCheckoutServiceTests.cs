using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Checkout;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AuthorizedCheckoutServiceTests
{
    private static readonly DateTimeOffset
        AuthenticatedAtUtc =
            new(
                2026,
                7,
                21,
                10,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task Anonymous_user_must_not_checkout()
    {
        var inner =
            new RecordingCheckoutService();

        var currentUser =
            new CurrentUserService();

        var service =
            new AuthorizedCheckoutService(
                inner,
                new PermissionService(
                    currentUser));

        var result =
            await service.CheckoutAsync(
                CreateRequest());

        Assert.Equal(
            ErrorCodes.General.Unauthorized,
            result.Error.Code);

        Assert.Equal(
            0,
            inner.CallCount);
    }

    [Fact]
    public async Task Inventory_staff_must_not_checkout()
    {
        var inner =
            new RecordingCheckoutService();

        var currentUser =
            CreateCurrentUser(
                Role.InventoryStaff);

        var service =
            new AuthorizedCheckoutService(
                inner,
                new PermissionService(
                    currentUser));

        var result =
            await service.CheckoutAsync(
                CreateRequest());

        Assert.Equal(
            ErrorCodes.General.Forbidden,
            result.Error.Code);

        Assert.Equal(
            0,
            inner.CallCount);
    }

    [Fact]
    public async Task Cashier_must_reach_checkout_core()
    {
        var inner =
            new RecordingCheckoutService();

        var currentUser =
            CreateCurrentUser(
                Role.Cashier);

        var service =
            new AuthorizedCheckoutService(
                inner,
                new PermissionService(
                    currentUser));

        var result =
            await service.CheckoutAsync(
                CreateRequest());

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            1,
            inner.CallCount);
    }

    private static CurrentUserService CreateCurrentUser(
        Role role)
    {
        var currentUser =
            new CurrentUserService();

        currentUser.SetCurrentUser(
            new AuthenticatedUserDto(
                id: 10,
                username: "checkout.test",
                fullName: "Thu ngân kiểm thử",
                role: role,
                authenticatedAtUtc:
                    AuthenticatedAtUtc));

        return currentUser;
    }

    private static CheckoutRequest CreateRequest()
    {
        return new CheckoutRequest(
            lines:
            [
                new CheckoutLineRequest(
                    1,
                    1)
            ],
            paymentMethod:
                PaymentMethod.Cash,
            cashReceived:
                100_000);
    }

    private sealed class RecordingCheckoutService :
        ICheckoutService
    {
        public int CallCount { get; private set; }

        public Task<Result<CheckoutResultDto>> CheckoutAsync(
            CheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            var result =
                new CheckoutResultDto(
                    OrderId: 1,
                    OrderCode: "HD-TEST",
                    CashierUserId: 10,
                    CashierName: "Thu ngân kiểm thử",
                    CustomerId: null,
                    CustomerName: null,
                    RestaurantTableId: null,
                    RestaurantTableName: null,
                    DiscountCode: null,
                    Status: OrderStatus.Completed,
                    PaymentMethod: PaymentMethod.Cash,
                    Subtotal: 50_000,
                    DiscountAmount: 0,
                    TotalAmount: 50_000,
                    CashReceived: 100_000,
                    ChangeAmount: 50_000,
                    CreatedAtUtc: AuthenticatedAtUtc,
                    PaidAtUtc: AuthenticatedAtUtc,
                    Lines: []);

            return Task.FromResult(
                Result.Success(
                    result));
        }
    }
}