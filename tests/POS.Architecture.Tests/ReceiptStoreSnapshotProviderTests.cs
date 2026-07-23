using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Printing;
using POS.Infrastructure.Printing;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ReceiptStoreSnapshotProviderTests
{
    [Fact]
    public void Provider_must_create_configured_unicode_snapshot()
    {
        var options =
            Options.Create(
                new ReceiptStoreOptions
                {
                    Name =
                        "  Cửa hàng Ánh Dương  ",

                    Address =
                        "  123 Đường Trần Phú, Hà Nội  ",

                    Phone =
                        "  0999 888 777  ",

                    TaxCode =
                        "  0101234567  ",

                    FooterMessage =
                        "  Cảm ơn quý khách và hẹn gặp lại!  "
                });

        IReceiptStoreSnapshotProvider provider =
            new ReceiptStoreSnapshotProvider(
                options);

        var snapshot =
            provider.GetCurrentSnapshot();

        Assert.True(
            snapshot.IsConfigured);

        Assert.Equal(
            "Cửa hàng Ánh Dương",
            snapshot.Name);

        Assert.Equal(
            "123 Đường Trần Phú, Hà Nội",
            snapshot.Address);

        Assert.Equal(
            "0999 888 777",
            snapshot.Phone);

        Assert.Equal(
            "0101234567",
            snapshot.TaxCode);

        Assert.Equal(
            "Cảm ơn quý khách và hẹn gặp lại!",
            snapshot.FooterMessage);
    }

    [Fact]
    public void Provider_must_return_stable_immutable_snapshot()
    {
        var mutableOptions =
            new ReceiptStoreOptions
            {
                Name =
                    "Cửa hàng ban đầu",

                Address =
                    "Địa chỉ ban đầu",

                Phone =
                    "0901 234 567"
            };

        var provider =
            new ReceiptStoreSnapshotProvider(
                Options.Create(
                    mutableOptions));

        var first =
            provider.GetCurrentSnapshot();

        mutableOptions.Name =
            "Tên đã bị thay đổi";

        mutableOptions.Address =
            "Địa chỉ đã bị thay đổi";

        var second =
            provider.GetCurrentSnapshot();

        Assert.Same(
            first,
            second);

        Assert.Equal(
            "Cửa hàng ban đầu",
            second.Name);

        Assert.Equal(
            "Địa chỉ ban đầu",
            second.Address);
    }

    [Fact]
    public void Provider_must_fail_fast_when_store_name_is_blank()
    {
        var options =
            Options.Create(
                new ReceiptStoreOptions
                {
                    Name =
                        "   "
                });

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    new ReceiptStoreSnapshotProvider(
                        options));

        Assert.Equal(
            "Cấu hình Store không hợp lệ.",
            exception.Message);

        var innerException =
            Assert.IsType<ArgumentException>(
                exception.InnerException);

        Assert.Equal(
            "name",
            innerException.ParamName);
    }

    [Fact]
    public void Typed_options_must_not_expose_wifi_password()
    {
        var property =
            typeof(ReceiptStoreOptions)
                .GetProperty(
                    "WifiPassword");

        Assert.Null(
            property);
    }

    [Fact]
    public void Provider_contract_must_not_expose_configuration_types()
    {
        var interfaceAssembly =
            typeof(IReceiptStoreSnapshotProvider)
                .Assembly;

        var references =
            interfaceAssembly
                .GetReferencedAssemblies()
                .Select(
                    assembly =>
                        assembly.Name)
                .Where(
                    name =>
                        name is not null)
                .ToArray();

        Assert.DoesNotContain(
            "Microsoft.Extensions.Options",
            references);

        Assert.DoesNotContain(
            "Microsoft.Extensions.Configuration",
            references);
    }
}