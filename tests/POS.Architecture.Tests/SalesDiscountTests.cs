using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Checkout;
using POS.Application.Services;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Services;
using POS.Infrastructure.Persistence;
using POS.Wpf.Services;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SalesDiscountDomainTests
{
    [Theory]
    [InlineData(1000, "10%")]
    [InlineData(1050, "10,5%")]
    [InlineData(1234, "12,34%")]
    [InlineData(1, "0,01%")]
    [InlineData(10000, "100%")]
    public void Percentage_basis_points_have_human_readable_presentation(
        long basisPoints,
        string expected)
    {
        Assert.Equal(
            expected,
            SalesDiscountPresentationFormatter.FormatPercentage(basisPoints));
    }

    [Theory]
    [InlineData(10_000, "10.000 đ")]
    [InlineData(20_100, "20.100 đ")]
    [InlineData(1_000_000, "1.000.000 đ")]
    public void Discount_money_uses_Vietnamese_group_separator(
        long amount,
        string expected)
    {
        Assert.Equal(
            expected,
            SalesDiscountPresentationFormatter.FormatMoney(amount));
    }

    [Fact]
    public void Fixed_amount_is_integer_and_bounded()
    {
        Assert.Equal(25_000, SalesDiscountCalculator.Resolve(
            100_000, SalesDiscountType.FixedAmount, 25_000, " Khách thân thiết "));
        Assert.Throws<DomainException>(() => SalesDiscountCalculator.Resolve(
            100_000, SalesDiscountType.FixedAmount, 100_001, "Lý do"));
    }

    [Fact]
    public void Percentage_uses_basis_points_and_floor()
    {
        Assert.Equal(33, SalesDiscountCalculator.Resolve(
            101, SalesDiscountType.Percentage, 3_333, "Ưu đãi"));
    }

    [Fact]
    public void Invalid_values_reason_and_zero_total_are_rejected()
    {
        Assert.Throws<DomainException>(() => SalesDiscountCalculator.Resolve(
            100, SalesDiscountType.Percentage, 0, "Lý do"));
        Assert.Throws<DomainException>(() => SalesDiscountCalculator.Resolve(
            100, SalesDiscountType.Percentage, 10_001, "Lý do"));
        Assert.Throws<DomainException>(() => SalesDiscountCalculator.Resolve(
            100, SalesDiscountType.FixedAmount, 10, " "));
        Assert.Throws<DomainException>(() => SalesDiscountCalculator.Resolve(
            100, SalesDiscountType.FixedAmount, 100, "Miễn phí"));
    }
}

public sealed class SalesDiscountApplicationTests
{
    [Fact]
    public void Canonical_fingerprint_contains_normalized_discount_request()
    {
        var canonicalizer = new CheckoutRequestCanonicalizer();
        var first = Request(SalesDiscountType.Percentage, 1_500, " Khách   VIP ");
        var normalized = Request(SalesDiscountType.Percentage, 1_500, "Khách VIP");
        var changedValue = Request(SalesDiscountType.Percentage, 1_501, "Khách VIP");
        var changedType = Request(SalesDiscountType.FixedAmount, 1_500, "Khách VIP");

        Assert.Equal(canonicalizer.Canonicalize(first).Fingerprint,
            canonicalizer.Canonicalize(normalized).Fingerprint);
        Assert.NotEqual(canonicalizer.Canonicalize(first).Fingerprint,
            canonicalizer.Canonicalize(changedValue).Fingerprint);
        Assert.NotEqual(canonicalizer.Canonicalize(first).Fingerprint,
            canonicalizer.Canonicalize(changedType).Fingerprint);
    }

    private static CheckoutRequest Request(
        SalesDiscountType type, long value, string reason) =>
        new([new CheckoutLineRequest(1, 1)], PaymentMethod.Cash, 100_000,
            clientRequestId: Guid.NewGuid(),
            salesDiscount: new SalesDiscountRequest(type, value, reason));
}

public sealed class SalesDiscountPersistenceTests
{
    [Fact]
    public void Snapshot_has_unique_order_fk_and_integer_money()
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        using var db = new PosDbContext(options);
        var entity = db.Model.FindEntityType(typeof(OrderDiscountSnapshot))!;
        Assert.True(entity.GetIndexes().Single(index =>
            index.Properties.Single().Name == nameof(OrderDiscountSnapshot.OrderId)).IsUnique);
        Assert.Equal("INTEGER", entity.FindProperty(
            nameof(OrderDiscountSnapshot.RequestedValue))!.GetColumnType());
        Assert.Equal("INTEGER", entity.FindProperty(
            nameof(OrderDiscountSnapshot.ResolvedAmount))!.GetColumnType());
        Assert.DoesNotContain(entity.GetProperties(),
            property => property.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class SalesDiscountMigrationTests
{
    [Fact]
    public void Migration_and_model_snapshot_include_controlled_discount_storage()
    {
        var migration = typeof(POS.Infrastructure.Persistence.Migrations.AddControlledSalesDiscounts);
        Assert.NotNull(migration);
        Assert.NotNull(typeof(PosDbContext).GetProperty(nameof(PosDbContext.OrderDiscountSnapshots)));
    }
}

public sealed class SalesDiscountInputTests
{
    [Theory]
    [InlineData(1, "1")]
    [InlineData(10, "10")]
    [InlineData(100, "100")]
    [InlineData(1_000, "1.000")]
    [InlineData(10_000, "10.000")]
    [InlineData(1_000_000, "1.000.000")]
    public void Fixed_discount_input_uses_Vietnamese_grouping(
        long value,
        string display)
    {
        Assert.Equal(display, SalesDiscountInputFormatter.FormatVnd(value));
        Assert.True(SalesDiscountInputFormatter.TryParseVndInput(display, out var parsed));
        Assert.Equal(value, parsed);
    }

    [Theory]
    [InlineData("10000")]
    [InlineData("10.000")]
    [InlineData("10 000")]
    [InlineData("10,000")]
    [InlineData(" 10.000 đ ")]
    [InlineData("10.000 ₫")]
    public void Fixed_discount_accepts_supported_plain_and_grouped_paste(string input)
    {
        Assert.True(SalesDiscountInputFormatter.TryParseVndInput(input, out var parsed));
        Assert.Equal(10_000, parsed);
    }

    [Theory]
    [InlineData("-10000")]
    [InlineData("khách 10000")]
    [InlineData("9223372036854775808")]
    [InlineData("")]
    public void Fixed_discount_rejects_negative_letters_overflow_and_empty(string input)
    {
        Assert.False(SalesDiscountInputFormatter.TryParseVndInput(input, out _));
    }

    [Fact]
    public void Fixed_discount_caret_supports_middle_backspace_and_delete()
    {
        const string afterBackspace = "1.345";
        const string afterDelete = "12.45";
        Assert.Equal(
            2,
            SalesDiscountInputFormatter.FindCaretIndex(
                afterBackspace,
                SalesDiscountInputFormatter.CountDigitsToRight(afterBackspace, 2)));
        Assert.Equal(
            3,
            SalesDiscountInputFormatter.FindCaretIndex(
                afterDelete,
                SalesDiscountInputFormatter.CountDigitsToRight(afterDelete, 3)));
    }

    [Theory]
    [InlineData("10", 1_000)]
    [InlineData("10%", 1_000)]
    [InlineData("10,5", 1_050)]
    [InlineData("10.5", 1_050)]
    [InlineData("10,05", 1_005)]
    public void Percentage_maps_decimal_text_to_basis_points(
        string input,
        long expected)
    {
        Assert.True(SalesDiscountInputFormatter.TryParsePercentage(input, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("100,01")]
    [InlineData("101")]
    [InlineData("10,001")]
    [InlineData("-10")]
    [InlineData("ten")]
    public void Percentage_rejects_out_of_range_or_ambiguous_values(string input)
    {
        Assert.False(SalesDiscountInputFormatter.TryParsePercentage(input, out _));
    }

    [Fact]
    public void Discount_input_implementation_does_not_use_floating_point()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "POS.Wpf",
                "Services",
                "SalesDiscountInputFormatter.cs"));
        Assert.DoesNotContain("double", source, StringComparison.Ordinal);
        Assert.DoesNotContain("float", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Discount_dialog_constructs_on_STA()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var application = new POS.Wpf.App();
                    application.ShutdownMode =
                        System.Windows.ShutdownMode.OnExplicitShutdown;
                    application.InitializeComponent();
                }
                var window = new SalesDiscountWindow(
                    200_000,
                    SalesDiscountType.FixedAmount,
                    10_000,
                    "Ưu đãi khách quen");
                window.Close();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ??
            throw new InvalidOperationException("Repository root not found.");
    }
}
