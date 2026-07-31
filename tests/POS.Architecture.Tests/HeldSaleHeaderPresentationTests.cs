using System.Xml.Linq;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSaleHeaderPresentationTests
{
    private static readonly string SalesWindowPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "POS.Wpf", "Views", "SalesWindow.xaml"));

    [Fact]
    public void Held_sale_count_must_not_render_as_unlabelled_zero_button()
    {
        var button = HeldSaleButton();
        Assert.NotEqual("0", (string?)button.Attribute("Content"));
    }

    [Fact]
    public void Held_sale_action_must_include_semantic_label()
    {
        var button = HeldSaleButton();
        Assert.Contains(
            "Đơn đang giữ",
            (string?)button.Attribute("Content"),
            StringComparison.Ordinal);
        Assert.Contains(
            "Đơn đang giữ",
            button.Attributes().Single(value =>
                value.Name.LocalName == "AutomationProperties.Name").Value,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Held_sale_count_updates_without_replacing_label()
    {
        var content = (string?)HeldSaleButton().Attribute("Content");
        Assert.Contains("ActiveHeldSaleButtonText", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Cart_header_still_fits_1366x768_at_125_percent()
    {
        var document = XDocument.Load(SalesWindowPath);
        var window = document.Root!;
        Assert.True(double.Parse((string)window.Attribute("MinWidth")!,
            System.Globalization.CultureInfo.InvariantCulture) <= 1366);
        Assert.True(double.Parse((string)window.Attribute("MinHeight")!,
            System.Globalization.CultureInfo.InvariantCulture) <= 768);
    }

    private static XElement HeldSaleButton()
    {
        var document = XDocument.Load(SalesWindowPath);
        return document.Descendants().Single(element =>
            element.Name.LocalName == "Button" &&
            ((string?)element.Attribute("Command"))?.Contains(
                "OpenHeldSalesCommand",
                StringComparison.Ordinal) == true);
    }
}
