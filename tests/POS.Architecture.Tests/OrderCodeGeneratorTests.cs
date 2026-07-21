using POS.Domain.Constants;
using POS.Infrastructure.Orders;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class OrderCodeGeneratorTests
{
    [Fact]
    public void Generated_code_must_have_stable_format()
    {
        var generator =
            new OrderCodeGenerator();

        var code =
            generator.Generate(
                new DateTimeOffset(
                    2026,
                    7,
                    21,
                    16,
                    30,
                    15,
                    125,
                    TimeSpan.Zero));

        Assert.Matches(
            "^HD-20260721-163015125-[0-9A-F]{8}$",
            code);

        Assert.True(
            code.Length <=
            BusinessRules.Orders.CodeMaxLength);
    }

    [Fact]
    public void Generated_codes_must_be_highly_unique()
    {
        var generator =
            new OrderCodeGenerator();

        var utcNow =
            new DateTimeOffset(
                2026,
                7,
                21,
                16,
                30,
                0,
                TimeSpan.Zero);

        var codes =
            Enumerable.Range(
                    0,
                    500)
                .Select(
                    _ =>
                        generator.Generate(
                            utcNow))
                .ToArray();

        Assert.Equal(
            codes.Length,
            codes.Distinct(
                StringComparer.Ordinal).Count());
    }
}