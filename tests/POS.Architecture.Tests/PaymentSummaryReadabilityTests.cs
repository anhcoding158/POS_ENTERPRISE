using System.Globalization;
using System.Xml.Linq;
using POS.Domain.Enums;
using POS.Domain.Services;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentSummaryReadabilityTests
{
    [Fact]
    public void Payment_summary_must_span_sufficient_width()
    {
        var card = Element("PaymentSummaryCard");
        Assert.Equal("0", GridColumn(card));
        Assert.Empty(Attribute(card, "Grid.ColumnSpan"));
        Assert.Same(Element("PaymentAreaGrid"), card.Parent);
    }

    [Fact]
    public void Payment_summary_must_not_share_a_tight_dynamic_column_with_cash_input()
    {
        var area = Element("PaymentAreaGrid");
        Assert.Null(area.Element(Presentation + "Grid.ColumnDefinitions"));
        Assert.Equal("0", GridRow(Element("PaymentSummaryCard")));
        Assert.Equal("1", GridRow(Element("CashPaymentSection")));
    }

    [Fact]
    public void Payment_summary_rows_must_use_auto_height()
    {
        var rows = Element("PaymentSummaryGrid")
            .Element(Presentation + "Grid.RowDefinitions")!
            .Elements(Presentation + "RowDefinition");
        Assert.All(rows, row => Assert.Equal("Auto", Attribute(row, "Height")));
    }

    [Fact]
    public void Payment_summary_subtotal_label_must_be_readable() =>
        AssertReadableLabel(Element("PaymentSubtotalLabel"));

    [Fact]
    public void Payment_summary_discount_label_must_be_readable() =>
        AssertReadableLabel(Element("PaymentDiscountLabel"));

    [Fact]
    public void Payment_summary_subtotal_amount_must_be_right_aligned() =>
        AssertRightAligned(Element("PaymentSubtotalAmount"));

    [Fact]
    public void Payment_summary_discount_amount_must_be_right_aligned() =>
        AssertRightAligned(Element("PaymentDiscountAmount"));

    [Fact]
    public void Payment_summary_labels_and_amounts_must_use_separate_grid_columns()
    {
        Assert.Equal("0", GridColumn(Element("PaymentSubtotalLabel")));
        Assert.Equal("2", GridColumn(Element("PaymentSubtotalAmount")));
        Assert.Equal("0", GridColumn(Element("PaymentDiscountLabel")));
        Assert.Equal("2", GridColumn(Element("PaymentDiscountAmount")));
        Assert.Equal(3, Element("PaymentSummaryGrid")
            .Element(Presentation + "Grid.ColumnDefinitions")!
            .Elements(Presentation + "ColumnDefinition").Count());
    }

    [Theory]
    [InlineData("PaymentSubtotalLabel", "PaymentSubtotalAmount")]
    [InlineData("PaymentDiscountLabel", "PaymentDiscountAmount")]
    [InlineData("PaymentTotalLabel", "PaymentTotalAmount")]
    public void Summary_labels_must_not_be_covered_by_long_amount(
        string labelName,
        string amountName)
    {
        var label = Element(labelName);
        var amount = Element(amountName);
        Assert.Equal("0", GridColumn(label));
        Assert.Equal("2", GridColumn(amount));
        Assert.Equal("NoWrap", Attribute(amount, "TextWrapping"));
        AssertRightAligned(amount);
    }

    [Fact]
    public void Total_label_must_not_be_clipped()
    {
        var label = Element("PaymentTotalLabel");
        Assert.Equal("TỔNG THANH TOÁN", Attribute(label, "Text"));
        Assert.DoesNotContain(
            label.AncestorsAndSelf(),
            element => Attribute(element, "ClipToBounds") == "True");
    }

    [Fact]
    public void Payment_summary_must_not_display_redundant_cash_label()
    {
        var summary = Element("PaymentSummaryCard");
        Assert.DoesNotContain(
            summary.Descendants(Presentation + "TextBlock"),
            element => Attribute(element, "Text") == "Tiền mặt");
    }

    [Fact]
    public void Summary_and_cash_sections_must_be_independent_layout_regions()
    {
        var area = Element("PaymentAreaGrid");
        Assert.Same(area, Element("PaymentSummaryCard").Parent);
        Assert.Same(area, Element("CashPaymentSection").Parent);
        Assert.NotEqual(
            GridRow(Element("PaymentSummaryCard")),
            GridRow(Element("CashPaymentSection")));
    }

    [Theory]
    [InlineData("CashReceivedHelper")]
    [InlineData("ChangeAmount")]
    public void Cash_dynamic_content_must_not_resize_or_cover_summary(string elementName)
    {
        var dynamicElement = Element(elementName);
        Assert.DoesNotContain(
            dynamicElement.AncestorsAndSelf(),
            element => Attribute(element, "Name") == "PaymentSummaryCard");
        Assert.Equal("1", GridRow(Element("CashPaymentSection")));
    }

    [Fact]
    public void Payment_summary_supports_999_999_999_VND_layout()
    {
        var amount = Element("PaymentTotalAmount");
        Assert.Equal("Auto", Element("PaymentSummaryGrid")
            .Element(Presentation + "Grid.ColumnDefinitions")!
            .Elements(Presentation + "ColumnDefinition")
            .ElementAt(2).Attribute("Width")!.Value);
        Assert.Equal("NoWrap", Attribute(amount, "TextWrapping"));
        AssertRightAligned(amount);
    }

    [Fact]
    public void Discount_buttons_must_not_overlap_total()
    {
        Assert.Equal("3", GridRow(Element("PaymentTotalAmount")));
        Assert.Equal("4", GridRow(Element("AddDiscountButton").Parent!));
        Assert.Equal("4", GridRow(Element("EditDiscountButton").Parent!.Parent!));
    }

    [Fact]
    public void Payment_summary_subtotal_and_discount_must_not_use_micro_font()
    {
        AssertFontAtLeast(Element("PaymentSubtotalLabel"), 12);
        AssertFontAtLeast(Element("PaymentDiscountLabel"), 12);
        AssertFontAtLeast(Element("PaymentSubtotalAmount"), 13);
        AssertFontAtLeast(Element("PaymentDiscountAmount"), 13);
    }

    [Fact]
    public void Payment_summary_total_must_remain_most_prominent()
    {
        var total = Document.Descendants(Presentation + "TextBlock")
            .Single(element => Attribute(element, "Text")?.Contains(
                "EstimatedTotalText", StringComparison.Ordinal) == true);
        var supportingSizes = new[]
        {
            FontSize(Element("PaymentSubtotalLabel")),
            FontSize(Element("PaymentDiscountLabel")),
            FontSize(Element("PaymentSubtotalAmount")),
            FontSize(Element("PaymentDiscountAmount"))
        };
        Assert.True(FontSize(total) > supportingSizes.Max());
        Assert.Equal("Bold", Attribute(total, "FontWeight"));
    }

    [Fact]
    public void Discount_button_text_must_not_include_F7()
    {
        Assert.Equal("Giảm giá", Attribute(Element("AddDiscountButton"), "Content"));
        Assert.DoesNotContain("F7", Attribute(Element("AddDiscountButton"), "Content"));
    }

    [Fact]
    public void Edit_discount_button_text_must_not_include_F7()
    {
        Assert.Equal("Sửa", Attribute(Element("EditDiscountButton"), "Content"));
        Assert.DoesNotContain("F7", Attribute(Element("EditDiscountButton"), "Content"));
    }

    [Fact]
    public void F7_shortcut_must_still_open_discount_dialog()
    {
        var source = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml.cs");
        var branch = source[
            source.IndexOf("Input.Key.F7", StringComparison.Ordinal)..
            source.IndexOf("Input.Key.F6", StringComparison.Ordinal)];
        Assert.Contains("ShowDiscountDialog();", branch, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true;", branch, StringComparison.Ordinal);
    }

    [Fact]
    public void Cash_received_label_must_be_readable() =>
        AssertReadableLabel(Element("CashReceivedLabel"));

    [Fact]
    public void Change_label_must_be_readable() =>
        AssertReadableLabel(Element("ChangeLabel"));

    [Fact]
    public void Change_amount_must_be_visually_clear()
    {
        var amount = Element("ChangeAmount");
        AssertFontAtLeast(amount, 13);
        AssertRightAligned(amount);
        Assert.Equal("Bold", Attribute(amount, "FontWeight"));
    }

    [Fact]
    public void Cash_received_helper_must_not_replace_primary_label()
    {
        var label = Element("CashReceivedLabel");
        var helper = Element("CashReceivedHelper");
        Assert.Equal("Tiền khách đưa", Attribute(label, "Text"));
        Assert.Contains("CashPreviewText", Attribute(helper, "Text"));
        Assert.NotSame(label, helper);
        Assert.True(FontSize(label) > FontSize(helper));
    }

    [Fact]
    public void Cash_input_and_F4_button_must_not_overlap()
    {
        var grid = Element("CashInputGrid");
        var input = Element("CashReceivedTextBox");
        var button = Element("ExactCashButton");
        Assert.Same(grid, input.Parent);
        Assert.Same(grid, button.Parent);
        Assert.Equal("0", GridColumn(input));
        Assert.Equal("2", GridColumn(button));
        Assert.Equal("8", grid.Element(Presentation + "Grid.ColumnDefinitions")!
            .Elements(Presentation + "ColumnDefinition").ElementAt(1).Attribute("Width")!.Value);
    }

    [Fact]
    public void Quick_cash_buttons_must_remain_visible()
    {
        var quickCash = Element("QuickCashButtons");
        Assert.Equal("4", Attribute(quickCash, "Columns"));
        Assert.Equal(4, quickCash.Elements(Presentation + "Button").Count());
        Assert.Empty(Attribute(quickCash, "Visibility"));
    }

    [Fact]
    public void Checkout_button_must_remain_visible()
    {
        var checkout = Element("CheckoutButton");
        Assert.Contains("CheckoutCommand", Attribute(checkout, "Command"));
        Assert.Empty(Attribute(checkout, "Visibility"));
    }

    [Fact]
    public void Payment_area_must_fit_1366x768_at_125_percent()
    {
        var window = Document.Root!;
        Assert.True(Parse(Attribute(window, "MinWidth")) <= 1366);
        Assert.True(Parse(Attribute(window, "MinHeight")) <= 768);
        Assert.DoesNotContain(
            Element("PaymentSummaryGrid").AncestorsAndSelf(),
            element => element.Name == Presentation + "ScrollViewer");
        Assert.DoesNotContain(
            Element("CashInputGrid").AncestorsAndSelf(),
            element => element.Name == Presentation + "ScrollViewer");
    }

    [Fact]
    public void Payment_area_must_add_no_new_hex_colors()
    {
        foreach (var element in new[]
                 {
                     Element("PaymentSummaryGrid"),
                     Element("CashPaymentHeaderGrid"),
                     Element("CashInputGrid")
                 })
        {
            Assert.DoesNotContain(
                element.DescendantsAndSelf().Attributes(),
                attribute => System.Text.RegularExpressions.Regex.IsMatch(
                    attribute.Value, "#[0-9A-Fa-f]{6,8}"));
        }
    }

    [Fact]
    public void Read_only_bindings_must_remain_one_way()
    {
        Assert.Contains("Mode=OneWay", Attribute(Element("PaymentSubtotalAmount"), "Text"));
        Assert.Contains("Mode=OneWay", Attribute(Element("PaymentDiscountAmount"), "Text"));
        Assert.Contains("Mode=OneWay", Attribute(
            Document.Descendants(Presentation + "TextBlock").Single(element =>
                Attribute(element, "Text")?.Contains("EstimatedTotalText", StringComparison.Ordinal) == true),
            "Text"));
    }

    [Fact]
    public void Editable_cash_binding_must_remain_two_way()
    {
        var binding = Attribute(Element("CashReceivedTextBox"), "Text");
        Assert.Contains("Mode=TwoWay", binding);
        Assert.Contains("UpdateSourceTrigger=PropertyChanged", binding);
    }

    [Fact]
    public void Cash_change_math_must_remain_unchanged()
    {
        var source = Read("src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");
        var property = Slice(source, "public string ChangePreviewText", "public bool HasEnoughCash");
        Assert.Contains("(decimal)cash -", property, StringComparison.Ordinal);
        Assert.Contains("EstimatedTotal;", property, StringComparison.Ordinal);
    }

    [Fact]
    public void Discount_math_must_remain_unchanged()
    {
        Assert.Equal(15_000, SalesDiscountCalculator.Resolve(
            150_000, SalesDiscountType.Percentage, 1_000, "Ưu đãi"));
        var source = Read("src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");
        Assert.Contains(
            "public decimal EstimatedTotal => EstimatedSubtotal - ResolvedDiscountAmount;",
            source,
            StringComparison.Ordinal);
    }

    private static void AssertReadableLabel(XElement element)
    {
        AssertFontAtLeast(element, 12);
        Assert.Contains(
            Attribute(element, "FontWeight"),
            ReadableFontWeights);
        Assert.False(string.IsNullOrWhiteSpace(Attribute(element, "Foreground")));
    }

    private static void AssertRightAligned(XElement element)
    {
        Assert.Equal("Right", Attribute(element, "HorizontalAlignment"));
        Assert.Equal("Right", Attribute(element, "TextAlignment"));
    }

    private static void AssertFontAtLeast(XElement element, double minimum) =>
        Assert.True(FontSize(element) >= minimum);

    private static double FontSize(XElement element) =>
        Parse(Attribute(element, "FontSize"));

    private static double Parse(string? value) =>
        double.Parse(value!, CultureInfo.InvariantCulture);

    private static string GridColumn(XElement element)
    {
        var column = Attribute(element, "Grid.Column");
        return column.Length == 0 ? "0" : column;
    }

    private static string GridRow(XElement element)
    {
        var row = Attribute(element, "Grid.Row");
        return row.Length == 0 ? "0" : row;
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        return source[startIndex..endIndex];
    }

    private static XElement Element(string name) =>
        Document.Descendants().Single(element =>
            Attribute(element, "Name") == name);

    private static string Attribute(XElement element, string name) =>
        (string?)element.Attribute(name switch
        {
            "Name" => Xaml + "Name",
            _ when name.Contains('.', StringComparison.Ordinal) => name,
            _ => name
        }) ?? string.Empty;

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. parts]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] ReadableFontWeights =
        ["Medium", "SemiBold", "Bold"];
    private static readonly XDocument Document = XDocument.Load(Path.Combine(
        RepositoryRoot, "src", "POS.Wpf", "Views", "SalesWindow.xaml"));
}
