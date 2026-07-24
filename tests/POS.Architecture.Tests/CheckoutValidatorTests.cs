using POS.Application.Common;
using POS.Application.DTOs.Checkout;
using POS.Application.Validation;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class CheckoutValidatorTests
{
    [Fact]
    public void Valid_cash_checkout_must_pass()
    {
        var request =
            new CheckoutRequest(
                lines:
                [
                    new CheckoutLineRequest(
                        productId:
                            1,

                        quantity:
                            2)
                ],

                paymentMethod:
                    PaymentMethod.Cash,

                cashReceived:
                    100_000);

        var result =
            CheckoutValidator.Validate(
                request);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());
    }

    [Fact]
    public void
        Valid_vietqr_checkout_with_zero_cash_must_pass()
    {
        var request =
            new CheckoutRequest(
                lines:
                [
                    new CheckoutLineRequest(
                        productId:
                            1,

                        quantity:
                            2)
                ],

                paymentMethod:
                    PaymentMethod.VietQr,

                cashReceived:
                    0);

        var result =
            CheckoutValidator.Validate(
                request);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());
    }

    [Fact]
    public void Empty_cart_must_fail()
    {
        var request =
            new CheckoutRequest(
                lines:
                    [],

                paymentMethod:
                    PaymentMethod.Cash,

                cashReceived:
                    0);

        var result =
            CheckoutValidator.Validate(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Checkout.EmptyCart,
            result.Error.Code);
    }

    [Fact]
    public void
        Vietqr_checkout_with_cash_received_must_fail()
    {
        var request =
            new CheckoutRequest(
                lines:
                [
                    new CheckoutLineRequest(
                        productId:
                            1,

                        quantity:
                            1)
                ],

                paymentMethod:
                    PaymentMethod.VietQr,

                cashReceived:
                    100_000);

        var result =
            CheckoutValidator.Validate(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Validation,
            result.Error.Code);

        Assert.Contains(
            "VietQR",
            result.Error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(
        PaymentMethod.BankTransfer)]
    [InlineData(
        PaymentMethod.Card)]
    public void
        Unsupported_payment_method_must_fail(
            PaymentMethod paymentMethod)
    {
        var request =
            new CheckoutRequest(
                lines:
                [
                    new CheckoutLineRequest(
                        productId:
                            1,

                        quantity:
                            1)
                ],

                paymentMethod:
                    paymentMethod,

                cashReceived:
                    0);

        var result =
            CheckoutValidator.Validate(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Checkout
                .PaymentMethodNotSupported,
            result.Error.Code);
    }

    [Fact]
    public void
        Negative_cash_received_must_fail_for_cash_payment()
    {
        var request =
            new CheckoutRequest(
                lines:
                [
                    new CheckoutLineRequest(
                        productId:
                            1,

                        quantity:
                            1)
                ],

                paymentMethod:
                    PaymentMethod.Cash,

                cashReceived:
                    -1);

        var result =
            CheckoutValidator.Validate(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Validation,
            result.Error.Code);
    }

    [Fact]
    public void
        Client_line_discount_must_be_rejected()
    {
        var request =
            new CheckoutRequest(
                lines:
                [
                    new CheckoutLineRequest(
                        productId:
                            1,

                        quantity:
                            1,

                        lineDiscountAmount:
                            5_000)
                ],

                paymentMethod:
                    PaymentMethod.Cash,

                cashReceived:
                    100_000);

        var result =
            CheckoutValidator.Validate(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Checkout
                .LineDiscountNotSupported,
            result.Error.Code);
    }

    [Fact]
    public void
        Client_modifier_must_be_rejected()
    {
        var request =
            new CheckoutRequest(
                lines:
                [
                    new CheckoutLineRequest(
                        productId:
                            1,

                        quantity:
                            1,

                        modifiers:
                        [
                            new CheckoutModifierRequest(
                                ModifierId:
                                    10,

                                Quantity:
                                    1)
                        ])
                ],

                paymentMethod:
                    PaymentMethod.Cash,

                cashReceived:
                    100_000);

        var result =
            CheckoutValidator.Validate(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Checkout
                .ModifiersNotSupported,
            result.Error.Code);
    }

    [Fact]
    public void
        Unsupported_customer_reference_must_fail()
    {
        var request =
            new CheckoutRequest(
                lines:
                [
                    new CheckoutLineRequest(
                        productId:
                            1,

                        quantity:
                            1)
                ],

                paymentMethod:
                    PaymentMethod.Cash,

                cashReceived:
                    100_000,

                customerId:
                    15);

        var result =
            CheckoutValidator.Validate(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Checkout
                .CustomerNotSupported,
            result.Error.Code);
    }
}