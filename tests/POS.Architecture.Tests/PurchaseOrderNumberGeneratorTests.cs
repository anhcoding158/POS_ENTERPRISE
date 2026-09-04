using System.Text.RegularExpressions;
using POS.Infrastructure.Purchasing;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PurchaseOrderNumberGeneratorTests
{
    [Fact]
    public void Generator_uses_utc_timestamp_and_uppercase_hex_suffix()
    {
        var generator = new PurchaseOrderNumberGenerator();
        var value = generator.Generate(new DateTimeOffset(2026, 9, 4, 10, 11, 12, 345, TimeSpan.FromHours(7)));

        Assert.Matches(
            new Regex(@"^PO-20260904-031112345-[0-9A-F]{8}$", RegexOptions.CultureInvariant),
            value);
    }

    [Fact]
    public void Generator_rejects_default_time()
    {
        Assert.Throws<ArgumentException>(() => new PurchaseOrderNumberGenerator().Generate(default));
    }
}
