using POS.Application.DTOs.Payments;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentManualResolutionHistoryTests
{
    private static readonly PaymentIntentManualResolutionDto Resolution = new(
        1, 7, "VQ-HISTORY-01",
        PaymentIntentManualResolutionType.RefundedExternally,
        new DateTimeOffset(2026, 7, 30, 1, 2, 3, TimeSpan.Zero),
        9, "Đã đối chiếu và hoàn bên ngoài", "RF-001", null, null, 125_000);

    [Fact]
    public void Resolved_item_appears_in_manual_resolution_history()
    {
        var service = Read("src", "POS.Application", "Services", "PaymentIntentService.cs");
        Assert.Contains("GetResolutionHistoryAsync", service, StringComparison.Ordinal);
        Assert.Equal("VQ-HISTORY-01", Resolution.DisplayCode);
    }

    [Fact]
    public void Active_pending_excludes_resolved_item()
    {
        var repository = Read("src", "POS.Infrastructure", "Persistence",
            "Repositories", "PaymentIntentRepository.cs");
        Assert.Contains("!dbContext.PaymentIntentManualResolutions.Any(", repository,
            StringComparison.Ordinal);
    }

    [Fact]
    public void History_is_read_only()
    {
        var xaml = Read("src", "POS.Wpf", "Views",
            "PaymentIntentManualResolutionHistoryWindow.xaml");
        Assert.Contains("IsReadOnly=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CanUserAddRows=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CanUserDeleteRows=\"False\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("OnEdit", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OnDelete", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void History_shows_actor_reason_and_resolution_type()
    {
        var xaml = Read("src", "POS.Wpf", "Views",
            "PaymentIntentManualResolutionHistoryWindow.xaml");
        Assert.Contains("ResolvedByText", xaml, StringComparison.Ordinal);
        Assert.Contains("Reason", xaml, StringComparison.Ordinal);
        Assert.Contains("ResolutionTypeText", xaml, StringComparison.Ordinal);
        Assert.Equal(9, Resolution.ResolvedByUserId);
        Assert.NotEmpty(Resolution.Reason);
    }

    [Fact]
    public void History_does_not_expose_raw_checkout_json()
    {
        var xaml = Read("src", "POS.Wpf", "Views",
            "PaymentIntentManualResolutionHistoryWindow.xaml");
        var code = Read("src", "POS.Wpf", "Views",
            "PaymentIntentManualResolutionHistoryWindow.xaml.cs");
        Assert.DoesNotContain("CheckoutRequestJson", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckoutRequestJson", code, StringComparison.Ordinal);
        Assert.DoesNotContain("PayloadText", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void History_uses_local_time_once()
    {
        var code = Read("src", "POS.Wpf", "Views",
            "PaymentIntentManualResolutionHistoryWindow.xaml.cs");
        var mapper = code[code.IndexOf("public static HistoryRow From",
            StringComparison.Ordinal)..];
        Assert.Equal(1, Count(mapper, ".ToLocalTime()"));
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var index = 0; (index = text.IndexOf(
                 value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            count++;
        return count;
    }

    private static string Read(params string[] parts)
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. parts]));
    }
}
