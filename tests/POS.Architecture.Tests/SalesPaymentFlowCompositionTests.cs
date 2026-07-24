using Microsoft.Extensions.DependencyInjection;
using POS.Wpf;
using POS.Wpf.Services;
using System.Reflection;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử registration của payment flow trong
/// composition root WPF.
///
/// Test gọi private configuration method bằng reflection
/// để kiểm tra đúng registration production, thay vì tự dựng
/// một ServiceCollection khác với App.
/// </summary>
public sealed class
    SalesPaymentFlowCompositionTests
{
    [Fact]
    public void
        App_must_register_sales_payment_flow_as_singleton()
    {
        var services =
            new ServiceCollection();

        var configureDialogServicesMethod =
            typeof(App)
                .GetMethod(
                    name:
                        "ConfigureDialogServices",

                    bindingAttr:
                        BindingFlags.NonPublic |
                        BindingFlags.Static);

        Assert.NotNull(
            configureDialogServicesMethod);

        configureDialogServicesMethod.Invoke(
            obj:
                null,

            parameters:
            [
                services
            ]);

        var paymentFlowDescriptor =
            Assert.Single(
                services,
                descriptor =>
                    descriptor.ServiceType ==
                    typeof(
                        ISalesPaymentFlowService));

        Assert.Equal(
            ServiceLifetime.Singleton,
            paymentFlowDescriptor.Lifetime);

        Assert.Equal(
            typeof(
                SalesPaymentFlowService),
            paymentFlowDescriptor
                .ImplementationType);
    }

    [Fact]
    public void
        App_must_register_vietqr_dialog_before_payment_flow()
    {
        var services =
            new ServiceCollection();

        var configureDialogServicesMethod =
            typeof(App)
                .GetMethod(
                    name:
                        "ConfigureDialogServices",

                    bindingAttr:
                        BindingFlags.NonPublic |
                        BindingFlags.Static);

        Assert.NotNull(
            configureDialogServicesMethod);

        configureDialogServicesMethod.Invoke(
            obj:
                null,

            parameters:
            [
                services
            ]);

        var descriptors =
            services
                .ToArray();

        var dialogIndex =
            Array.FindIndex(
                descriptors,
                descriptor =>
                    descriptor.ServiceType ==
                    typeof(
                        IVietQrPaymentDialogService));

        var paymentFlowIndex =
            Array.FindIndex(
                descriptors,
                descriptor =>
                    descriptor.ServiceType ==
                    typeof(
                        ISalesPaymentFlowService));

        Assert.True(
            dialogIndex >= 0,
            "Không tìm thấy registration " +
            "IVietQrPaymentDialogService.");

        Assert.True(
            paymentFlowIndex >= 0,
            "Không tìm thấy registration " +
            "ISalesPaymentFlowService.");

        Assert.True(
            dialogIndex <
            paymentFlowIndex,
            "VietQR dialog nên được đăng ký trước " +
            "Sales payment flow để composition root " +
            "dễ kiểm tra.");
    }

    [Fact]
    public void
        App_must_not_register_duplicate_payment_flow_services()
    {
        var services =
            new ServiceCollection();

        var configureDialogServicesMethod =
            typeof(App)
                .GetMethod(
                    name:
                        "ConfigureDialogServices",

                    bindingAttr:
                        BindingFlags.NonPublic |
                        BindingFlags.Static);

        Assert.NotNull(
            configureDialogServicesMethod);

        configureDialogServicesMethod.Invoke(
            obj:
                null,

            parameters:
            [
                services
            ]);

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(
                    ISalesPaymentFlowService));

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(
                    IVietQrPaymentDialogService));
    }
}