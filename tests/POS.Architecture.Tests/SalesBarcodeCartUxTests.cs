using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Payments;
using POS.Application.DTOs.Products;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SalesBarcodeCartUxTests
{
    [Fact]
    public async Task Confirmed_recovery_retry_button_has_command()
    {
        var exception = await RunOnStaAsync(() =>
        {
            var context = CreateContext();
            context.ViewModel.PendingPaymentIntents.Add(ConfirmedIntent());
            context.ViewModel.SelectedPaymentIntentRecovery =
                context.ViewModel.PendingPaymentIntents[0];
            Assert.Equal(
                "Button",
                NamedElement("ConfirmedRecoveryRetryButton").Name.LocalName);
            var button = new System.Windows.Controls.Button
            {
                DataContext = context.ViewModel,
                Command = context.ViewModel.RetryPaymentIntentRecoveryCommand,
                CommandParameter = context.ViewModel.SelectedPaymentIntentRecovery.Id
            };

            Assert.NotNull(button.Command);
            Assert.Equal(73, button.CommandParameter);
            Assert.True(button.IsEnabled);
            Assert.True(button.IsHitTestVisible);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Confirmed_recovery_retry_command_receives_payment_intent_id()
    {
        var context = CreateContext();
        context.ViewModel.PendingPaymentIntents.Add(ConfirmedIntent());
        context.ViewModel.SelectedPaymentIntentRecovery =
            context.ViewModel.PendingPaymentIntents[0];

        Assert.True(context.ViewModel.RetryPaymentIntentRecoveryCommand.CanExecute(73));
        Assert.False(context.ViewModel.RetryPaymentIntentRecoveryCommand.CanExecute(null));
        Assert.False(context.ViewModel.RetryPaymentIntentRecoveryCommand.CanExecute(74));
    }

    [Fact]
    public void Confirmed_recovery_retry_command_cannot_execute_while_busy()
    {
        var context = CreateContext();
        context.ViewModel.PendingPaymentIntents.Add(ConfirmedIntent());
        context.ViewModel.SelectedPaymentIntentRecovery =
            context.ViewModel.PendingPaymentIntents[0];
        SetField(context.ViewModel, "_isProcessingRecovery", true);

        Assert.False(context.ViewModel.RetryPaymentIntentRecoveryCommand.CanExecute(73));
    }

    [Fact]
    public async Task Confirmed_recovery_has_close_for_later_action()
    {
        var exception = await RunOnStaAsync(() =>
        {
            var context = CreateContext();
            Assert.Equal(
                "Button",
                NamedElement("ConfirmedRecoveryCloseForLaterButton").Name.LocalName);
            var close = new System.Windows.Controls.Button
            {
                DataContext = context.ViewModel,
                Content = "ĐÓNG ĐỂ XỬ LÝ SAU",
                IsEnabled = context.ViewModel.IsRecoveryOperationIdle
            };

            Assert.Equal("ĐÓNG ĐỂ XỬ LÝ SAU", close.Content);
            Assert.True(close.IsEnabled);
            Assert.True(close.IsHitTestVisible);
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task Exact_barcode_adds_product_once()
    {
        var context = CreateContext(Product("8938500000001"));
        Assert.True(await context.ViewModel.ProcessScanOrSearchAsync("8938500000001"));
        Assert.Single(context.ViewModel.CartLines);
        Assert.Equal(1, context.ViewModel.CartItemCount);
    }

    [Fact]
    public async Task Repeated_barcode_increments_existing_line()
    {
        var context = CreateContext(Product("8938500000001"));
        await context.ViewModel.ProcessScanOrSearchAsync("8938500000001");
        await context.ViewModel.ProcessScanOrSearchAsync("8938500000001");
        Assert.Single(context.ViewModel.CartLines);
        Assert.Equal(2, context.ViewModel.CartLines[0].Quantity);
    }

    [Fact]
    public async Task Unknown_barcode_keeps_cart_unchanged()
    {
        var context = CreateContext(Product("KNOWN"));
        await context.ViewModel.ProcessScanOrSearchAsync("KNOWN");
        Assert.False(await context.ViewModel.ProcessScanOrSearchAsync("UNKNOWN"));
        Assert.Equal(1, context.ViewModel.CartItemCount);
        Assert.Contains("Không tìm thấy mã sản phẩm", context.ViewModel.StatusMessage);
    }

    [Fact]
    public async Task Archived_product_barcode_is_rejected()
    {
        var context = CreateContext(Product("ARCHIVED", isArchived: true));
        Assert.False(await context.ViewModel.ProcessScanOrSearchAsync("ARCHIVED"));
        Assert.Empty(context.ViewModel.CartLines);
        Assert.Equal("Sản phẩm đã ngừng bán.", context.ViewModel.StatusMessage);
    }

    [Fact]
    public async Task Out_of_stock_product_is_rejected_by_existing_policy()
    {
        var context = CreateContext(Product("EMPTY", stock: 0));
        Assert.False(await context.ViewModel.ProcessScanOrSearchAsync("EMPTY"));
        Assert.Empty(context.ViewModel.CartLines);
    }

    [Fact]
    public void Barcode_lookup_does_not_return_cost_price()
    {
        Assert.Null(typeof(SalesCatalogProductDto).GetProperty("CostPrice"));
        Assert.Null(typeof(SalesCatalogProductDto).GetProperty("ProfitPerUnit"));
    }

    [Fact]
    public void Barcode_lookup_is_database_side()
    {
        var source = Read("src", "POS.Infrastructure", "Persistence", "Repositories", "ProductRepository.cs");
        Assert.Contains(".AsNoTracking()", source, StringComparison.Ordinal);
        Assert.Contains("product.Barcode ==", source, StringComparison.Ordinal);
        Assert.Contains("SingleOrDefaultAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rapid_sequential_scans_preserve_order()
    {
        var first = Product("A", id: 1);
        var second = Product("B", id: 2);
        var context = CreateContext(first, second);
        await Task.WhenAll(
            context.ViewModel.ProcessScanOrSearchAsync("A"),
            context.ViewModel.ProcessScanOrSearchAsync("B"),
            context.ViewModel.ProcessScanOrSearchAsync("A"));
        Assert.Equal([1, 2], context.ViewModel.CartLines.Select(x => x.ProductId));
        Assert.Equal(2, context.ViewModel.CartLines[0].Quantity);
    }

    [Fact]
    public async Task Scan_during_checkout_does_not_mutate_cart()
    {
        var context = CreateContext(Product("A"));
        SetField(context.ViewModel, "_isCheckingOut", true);
        Assert.False(await context.ViewModel.ProcessScanOrSearchAsync("A"));
        Assert.Empty(context.ViewModel.CartLines);
    }

    [Fact]
    public async Task Scan_during_prepared_payload_lock_does_not_mutate_cart()
    {
        var context = CreateContext(Product("A"));
        context.ViewModel.CheckoutRecoveries.Add(null!);
        Assert.False(await context.ViewModel.ProcessScanOrSearchAsync("A"));
        Assert.Empty(context.ViewModel.CartLines);
    }

    [Fact]
    public void Increment_updates_quantity_and_totals()
    {
        var context = CreateContext(Product("A", stock: 5));
        var line = CreateLine(context, Product("A", stock: 5));
        Assert.True(line.TryIncrease());
        Assert.Equal(2, line.Quantity);
        Assert.Equal(20_000, line.LineTotal);
    }

    [Fact]
    public void Increment_respects_stock()
    {
        var context = CreateContext(Product("A", stock: 1));
        var line = CreateLine(context, Product("A", stock: 1));
        Assert.False(line.TryIncrease());
        Assert.Equal(1, line.Quantity);
    }

    [Fact]
    public void Decrement_never_goes_below_one()
    {
        var context = CreateContext(Product("A"));
        var line = CreateLine(context, Product("A"));
        Assert.False(line.CanDecrease);
        Assert.Equal(1, line.Quantity);
    }

    [Fact]
    public async Task Clear_cart_requires_confirmation_default_no()
    {
        var source = Read("src", "POS.Wpf", "Services", "ICheckoutRecoveryConfirmationService.cs");
        Assert.Contains("MessageBoxResult.No", source, StringComparison.Ordinal);

        var context = CreateContext(Product("A"));
        await context.ViewModel.ProcessScanOrSearchAsync("A");
        context.ViewModel.ClearCartCommand.Execute(null);
        await Task.Yield();
        Assert.Single(context.ViewModel.CartLines);
    }

    [Fact]
    public async Task Confirm_clear_empties_cart_once()
    {
        var context = CreateContext(true, Product("A"));
        await context.ViewModel.ProcessScanOrSearchAsync("A");
        context.ViewModel.ClearCartCommand.Execute(null);
        await Task.Yield();
        Assert.Empty(context.ViewModel.CartLines);
        Assert.Equal(1, context.Confirmation.ClearCalls);
    }

    [Fact]
    public async Task Search_text_behavior_remains_compatible()
    {
        var context = CreateContext(Product("BARCODE"));
        context.ViewModel.SearchTerm = "cà phê";
        context.ViewModel.SearchCommand.Execute(null);
        await Task.Yield();
        Assert.Equal("cà phê", context.ViewModel.SearchTerm);
    }

    [Fact]
    public async Task Remove_line_updates_totals()
    {
        var context = CreateContext(Product("A"));
        await context.ViewModel.ProcessScanOrSearchAsync("A");
        context.ViewModel.SelectedCartLine = context.ViewModel.CartLines[0];
        context.ViewModel.RemoveSelectedCartLine();
        Assert.Empty(context.ViewModel.CartLines);
        Assert.Equal(0, context.ViewModel.EstimatedTotal);
    }

    [Fact]
    public async Task Cancel_clear_keeps_cart()
    {
        var context = CreateContext(Product("A"));
        await context.ViewModel.ProcessScanOrSearchAsync("A");
        context.ViewModel.ClearCartCommand.Execute(null);
        await Task.Yield();
        Assert.Single(context.ViewModel.CartLines);
    }

    [Fact]
    public async Task Clear_cart_is_blocked_during_checkout()
    {
        var context = CreateContext(true, Product("A"));
        await context.ViewModel.ProcessScanOrSearchAsync("A");
        SetField(context.ViewModel, "_isCheckingOut", true);
        Assert.False(context.ViewModel.ClearCartCommand.CanExecute(null));
        Assert.Single(context.ViewModel.CartLines);
    }

    [Fact]
    public async Task Clear_cart_is_blocked_during_prepared_lock()
    {
        var context = CreateContext(true, Product("A"));
        await context.ViewModel.ProcessScanOrSearchAsync("A");
        context.ViewModel.CheckoutRecoveries.Add(null!);
        Assert.False(context.ViewModel.ClearCartCommand.CanExecute(null));
        Assert.Single(context.ViewModel.CartLines);
    }

    [Fact]
    public void Empty_cart_disables_checkout()
    {
        var context = CreateContext();
        Assert.False(context.ViewModel.CheckoutCommand.CanExecute(null));
    }

    [Fact]
    public void Shortcut_commands_respect_busy_state()
    {
        var context = CreateContext();
        SetField(context.ViewModel, "_isCheckingOut", true);
        Assert.False(context.ViewModel.SearchCommand.CanExecute(null));
        Assert.False(context.ViewModel.ClearCartCommand.CanExecute(null));
    }

    [Fact]
    public async Task Sales_window_constructs_on_STA_without_binding_exception()
    {
        var completion = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var application = new POS.Wpf.App();
                    application.InitializeComponent();
                }
                var window = new SalesWindow(CreateContext().ViewModel);
                window.Close();
                completion.SetResult(null);
            }
            catch (Exception exception)
            {
                completion.SetResult(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.Null(await completion.Task);
        thread.Join();
    }

    [Theory]
    [InlineData("F2_focuses_scan_input", "Key.F2", "ProductScanBox")]
    [InlineData("Unknown_barcode_keeps_scan_focus_ready", "ProcessScanOrSearchAsync", "FocusAndSelectAll(ProductScanBox)")]
    [InlineData("Successful_add_restores_scan_focus", "ProcessScanOrSearchAsync", "FocusAndSelectAll(ProductScanBox)")]
    [InlineData("Unknown_code_does_not_move_focus_to_cash", "ProcessScanOrSearchAsync", "ProductScanBox.Text")]
    [InlineData("Escape_requests_guarded_window_close", "Input.Key.Escape", "Close();")]
    [InlineData("Enter_scan_does_not_trigger_checkout", "HandleEnterKeyAsync", "ProcessScanOrSearchAsync")]
    [InlineData("Delete_removes_selected_line", "Input.Key.Delete", "RemoveSelectedCartLine")]
    [InlineData("Existing_F4_F6_F8_shortcuts_are_preserved", "Input.Key.F4", "Input.Key.F8")]
    public void Keyboard_and_focus_contracts(string _, string first, string second)
    {
        var source = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml.cs");
        Assert.Contains(first, source, StringComparison.Ordinal);
        Assert.Contains(second, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Displayed_shortcuts_match_actual_keyboard_behavior()
    {
        var xaml = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml");
        var codeBehind = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml.cs");

        Assert.Contains(
            "F2 Quét mã · Ctrl+F Tìm sản phẩm · F4 Đủ tiền · F6 Tiền khách đưa · F8 Thanh toán",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Esc Đóng quầy · Delete Xóa dòng",
            xaml,
            StringComparison.Ordinal);
        Assert.DoesNotContain("F6 Thanh toán", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("F8 Hoàn tất", xaml, StringComparison.Ordinal);
        Assert.Contains("Input.Key.F6", codeBehind, StringComparison.Ordinal);
        Assert.Contains("CashReceivedTextBox", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Input.Key.F8", codeBehind, StringComparison.Ordinal);
        Assert.Contains("CheckoutCommand", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_scanner_helper_explains_keyboard_entry()
    {
        var xaml = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml");

        Assert.Contains(
            "Barcode hoặc mã sản phẩm",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Không có máy quét? Nhập Barcode/ProductCode rồi nhấn Enter.",
            xaml,
            StringComparison.Ordinal);
        Assert.DoesNotContain("camera", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sales_search_and_scan_must_be_visually_separate_groups()
    {
        var document = XDocument.Load(
            PathOf("src", "POS.Wpf", "Views", "SalesWindow.xaml"));
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        Assert.Contains(
            document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "TÌM SẢN PHẨM");
        Assert.Contains(
            document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "QUÉT MÃ");
    }

    [Fact]
    public void Sales_header_must_use_stretched_grid_not_centered_toolbar()
    {
        var header = NamedElement("SalesCatalogHeaderGrid");
        Assert.Equal("Grid", header.Name.LocalName);
        Assert.Equal("Stretch", Attribute(header, "HorizontalAlignment"));
        Assert.NotEqual("Center", Attribute(NamedElement("SalesSearchGroup"), "HorizontalAlignment"));
        Assert.DoesNotContain(
            header.AncestorsAndSelf(),
            element => Attribute(element, "HorizontalAlignment") == "Center");
    }

    [Fact]
    public void Sales_title_must_remain_left_aligned()
    {
        var title = NamedElement("SalesTitleBlock");
        Assert.Equal("0", Attribute(title, "Grid.Column") ?? "0");
        Assert.Equal("Left", Attribute(title, "HorizontalAlignment"));
        Assert.Equal("Center", Attribute(title, "VerticalAlignment"));
    }

    [Fact]
    public void Sales_search_and_scan_toolbar_must_align_to_right_side()
    {
        var header = NamedElement("SalesCatalogHeaderGrid");
        var columns = GridColumnWidths(header);
        Assert.Equal(["Auto", "20", "*", "14", "230"], columns);
        Assert.Equal("2", Attribute(NamedElement("SalesSearchGroup"), "Grid.Column"));
        Assert.Equal("4", Attribute(NamedElement("SalesScanGroup"), "Grid.Column"));
    }

    [Fact]
    public void Sales_header_must_not_have_large_dead_space_between_title_and_tools()
    {
        var columns = GridColumnWidths(NamedElement("SalesCatalogHeaderGrid"));
        Assert.Equal("20", columns[1]);
        Assert.Equal("*", columns[2]);
        Assert.DoesNotContain(columns, width => width.EndsWith('*') && width != "*");
    }

    [Fact]
    public void Sales_search_group_must_expand_more_than_scan_group()
    {
        var search = NamedElement("SalesSearchGroup");
        var scan = NamedElement("SalesScanGroup");
        Assert.Equal("Stretch", Attribute(search, "HorizontalAlignment"));
        Assert.Equal("280", Attribute(search, "MinWidth"));
        Assert.Equal("230", Attribute(scan, "MinWidth"));
        Assert.Equal("300", Attribute(scan, "MaxWidth"));
    }

    [Fact]
    public void Sales_search_button_must_align_with_search_textbox()
    {
        var search = NamedElement("SalesSearchGroup");
        var searchBox = NamedElement("ProductSearchBox");
        var button = NamedElement("ProductSearchButton");
        Assert.Equal("44", Attribute(searchBox, "Height") ?? StyleSetter("SalesSearchTextBoxStyle", "Height"));
        Assert.Equal("44", Attribute(button, "Height"));
        Assert.Same(
            searchBox.Ancestors().First(element => element.Name.LocalName == "Grid" && Attribute(element, "Grid.Row") == "2"),
            button.Parent);
    }

    [Fact]
    public void Sales_scan_icon_must_be_embedded_inside_scan_input()
    {
        var scanInput = NamedElement("SalesScanInput");
        var scanBox = NamedElement("ProductScanBox");
        var icon = NamedElement("SalesScannerIcon");
        Assert.Same(scanInput, scanBox.Parent);
        Assert.Same(scanInput, icon.Parent);
        Assert.False(string.IsNullOrWhiteSpace((string?)icon.Attribute("Data")));
        Assert.Equal("{StaticResource GoldBrush}", Attribute(icon, "Fill"));
        Assert.Equal("43,0,13,0", Attribute(scanBox, "Padding"));
    }

    [Fact]
    public void Sales_scan_icon_must_not_be_a_button_or_focusable_control()
    {
        var icon = NamedElement("SalesScannerIcon");
        Assert.Equal("Path", icon.Name.LocalName);
        Assert.Equal("False", Attribute(icon, "Focusable"));
        Assert.Equal("False", Attribute(icon, "IsHitTestVisible"));
        Assert.Null(Attribute(icon, "Command"));
        Assert.DoesNotContain(icon.Ancestors(), element => element.Name.LocalName == "Button");
    }

    [Fact]
    public void Sales_scan_input_must_not_have_double_border()
    {
        var scanInput = NamedElement("SalesScanInput");
        Assert.Equal("Grid", scanInput.Name.LocalName);
        Assert.Empty(scanInput.Elements(Presentation + "Border"));
        Assert.Single(scanInput.Elements(Presentation + "TextBox"));
    }

    [Fact]
    public void Sales_search_and_scan_labels_must_share_vertical_alignment()
    {
        var labels = new List<string> { "TÌM SẢN PHẨM", "QUÉT MÃ" }
            .Select(text => Document.Descendants(Presentation + "TextBlock")
                .Single(element => Attribute(element, "Text") == text))
            .ToArray();
        Assert.All(labels, label => Assert.Equal("Bottom", Attribute(label, "VerticalAlignment")));
        Assert.All(labels, label => Assert.Equal("10.5", Attribute(label, "FontSize")));
        Assert.Equal(
            labels[0].Ancestors(Presentation + "Grid").First().Elements(Presentation + "RowDefinition").Count(),
            labels[1].Ancestors(Presentation + "Grid").First().Elements(Presentation + "RowDefinition").Count());
    }

    [Fact]
    public void Sales_search_textbox_scan_textbox_and_button_must_share_height()
    {
        Assert.Equal("44", StyleSetter("SalesSearchTextBoxStyle", "Height"));
        Assert.Equal("44", Attribute(NamedElement("ProductSearchButton"), "Height"));
        Assert.Equal(
            Attribute(NamedElement("ProductSearchBox"), "Style"),
            Attribute(NamedElement("ProductScanBox"), "Style"));
    }

    [Fact]
    public void Sales_scan_placeholder_must_have_sufficient_width()
    {
        var scan = NamedElement("SalesScanGroup");
        Assert.True(double.Parse(
            Attribute(scan, "MinWidth")!,
            CultureInfo.InvariantCulture) >= 230);
        Assert.Equal(
            "Barcode hoặc mã sản phẩm",
            Document.Descendants(Presentation + "TextBlock")
                .Single(element => Attribute(element, "Text") == "Barcode hoặc mã sản phẩm")
                .Attribute("Text")?.Value);
    }

    [Fact]
    public void Sales_toolbar_must_not_use_negative_margins_or_canvas_positioning()
    {
        var header = NamedElement("SalesCatalogHeaderGrid");
        Assert.Empty(header.Descendants(Presentation + "Canvas"));
        Assert.DoesNotContain(
            header.DescendantsAndSelf().Attributes("Margin"),
            margin => margin.Value.Split(',').Any(part =>
                double.TryParse(part, out var value) && value < 0));
    }

    [Fact]
    public void Sales_scan_helper_must_not_create_full_width_extra_row()
    {
        var helper = Document.Descendants()
            .Single(element =>
                ((string?)element.Attribute("ToolTip"))?.Contains(
                    "Không có máy quét?",
                    StringComparison.Ordinal) == true);

        Assert.Equal("Grid", helper.Name.LocalName);
        Assert.DoesNotContain(
            Document.Descendants(Presentation + "TextBlock"),
            element =>
                ((string?)element.Attribute("Text"))?.Contains(
                    "Không có máy quét?",
                    StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Search_and_scan_enter_handlers_must_not_be_cross_wired()
    {
        var codeBehind = Read(
            "src", "POS.Wpf", "Views", "SalesWindow.xaml.cs");
        var handlerStart = codeBehind.IndexOf(
            "private async Task HandleEnterKeyAsync",
            StringComparison.Ordinal);
        var searchBranch = codeBehind[
            codeBehind.IndexOf(
                "if (ProductSearchBox",
                handlerStart,
                StringComparison.Ordinal)..
            codeBehind.IndexOf(
                "if (ProductScanBox",
                handlerStart,
                StringComparison.Ordinal)];
        var scanBranchStart = codeBehind.IndexOf(
            "if (ProductScanBox",
            handlerStart,
            StringComparison.Ordinal);
        var scanBranch = codeBehind[
            scanBranchStart..
            codeBehind.IndexOf(
                "// Enter tại ô tiền",
                scanBranchStart,
                StringComparison.Ordinal)];

        Assert.Contains("SearchCommand", searchBranch, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ProcessScanOrSearchAsync",
            searchBranch,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProcessScanOrSearchAsync",
            scanBranch,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SearchCommand", scanBranch, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_only_sales_bindings_are_not_two_way()
    {
        var document = XDocument.Load(PathOf("src", "POS.Wpf", "Views", "SalesWindow.xaml"));
        var textBindings = document.Descendants()
            .Attributes("Text")
            .Where(x => x.Value.Contains("EstimatedTotalText", StringComparison.Ordinal));
        Assert.All(textBindings, x => Assert.DoesNotContain("Mode=TwoWay", x.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Editable_sales_bindings_keep_two_way()
    {
        var source = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml");
        Assert.Contains("UpdateSourceTrigger=PropertyChanged", source, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedCartLine,", source, StringComparison.Ordinal);
    }

    [Fact]
    public void No_cost_price_in_sales_ui()
    {
        Assert.DoesNotContain("CostPrice", Read("src", "POS.Wpf", "Views", "SalesWindow.xaml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Sales_toolbar_must_not_add_new_hex_colors()
    {
        var diff = RunGitDiff();
        Assert.DoesNotContain(
            diff.Split('\n').Where(x =>
                x.Length > 0 &&
                x[0] == '+' &&
                !x.StartsWith("+++", StringComparison.Ordinal)),
            x => System.Text.RegularExpressions.Regex.IsMatch(x, "#[0-9A-Fa-f]{6,8}"));
    }

    [Fact]
    public void Sales_toolbar_must_fit_1366x768()
    {
        var window = Document.Root!;
        Assert.True(double.Parse(
            Attribute(window, "MinWidth")!,
            CultureInfo.InvariantCulture) <= 1366);
        Assert.True(double.Parse(
            Attribute(window, "MinHeight")!,
            CultureInfo.InvariantCulture) <= 768);
        var fixedHeaderWidth = GridColumnWidths(NamedElement("SalesCatalogHeaderGrid"))
            .Where(width => double.TryParse(
                width,
                CultureInfo.InvariantCulture,
                out _))
            .Sum(width => double.Parse(
                width,
                CultureInfo.InvariantCulture));
        Assert.True(fixedHeaderWidth <= 264);
        Assert.DoesNotContain(
            Document.Descendants(Presentation + "ScrollViewer"),
            element => NamedElement("SalesCatalogHeaderGrid")
                .AncestorsAndSelf()
                .Contains(element));
    }

    [Fact]
    public void F2_must_still_focus_scan()
    {
        var source = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml.cs");
        var f2Branch = source[
            source.IndexOf("Input.Key.F2", StringComparison.Ordinal)..
            source.IndexOf("Input.Key.F4", StringComparison.Ordinal)];
        Assert.Contains("ProductScanBox", f2Branch, StringComparison.Ordinal);
        Assert.Contains("FocusAndSelectAll", f2Branch, StringComparison.Ordinal);
    }

    [Fact]
    public void Ctrl_F_must_still_focus_search()
    {
        var source = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml.cs");
        Assert.Contains("Input.ModifierKeys.Control", source, StringComparison.Ordinal);
        Assert.Contains("Input.Key.F)", source, StringComparison.Ordinal);
        Assert.Contains("FocusAndSelectAll(ProductSearchBox)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_enter_must_still_filter_only()
    {
        var searchBranch = EnterHandlerBranch("ProductSearchBox", "ProductScanBox");
        Assert.Contains("SearchCommand", searchBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessScanOrSearchAsync", searchBranch, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_enter_must_still_add_exact_product()
    {
        var scanBranch = EnterHandlerBranch("ProductScanBox", "// Enter tại ô tiền");
        Assert.Contains("ProcessScanOrSearchAsync", scanBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchCommand", scanBranch, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_single_result_must_not_auto_add()
    {
        var context = CreateContext(Product("ONLY"));
        context.ViewModel.SearchTerm = "Sản phẩm 1";
        context.ViewModel.SearchCommand.Execute(null);
        await Task.Yield();
        Assert.Empty(context.ViewModel.CartLines);
    }

    [Fact]
    public async Task Scan_must_not_change_search_or_cash()
    {
        var context = CreateContext(Product("SCAN"));
        context.ViewModel.SearchTerm = "catalog filter";
        context.ViewModel.CashReceivedText = "50000";
        Assert.True(await context.ViewModel.ProcessScanOrSearchAsync("SCAN"));
        Assert.Equal("catalog filter", context.ViewModel.SearchTerm);
        Assert.Equal("50000", context.ViewModel.CashReceivedText);
    }

    [Fact]
    public void Checkout_and_recovery_guards_must_remain()
    {
        var source = Read("src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");
        Assert.Contains("IsCheckingOut", source, StringComparison.Ordinal);
        Assert.Contains("HasCheckoutRecovery", source, StringComparison.Ordinal);
        Assert.Contains("HasPendingVietQrAuthorization", source, StringComparison.Ordinal);
    }

    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly XDocument Document =
        XDocument.Load(PathOf("src", "POS.Wpf", "Views", "SalesWindow.xaml"));

    private static XElement NamedElement(string name) =>
        Document.Descendants().Single(element =>
            Attribute(element, "Name") == name);

    private static string? Attribute(XElement element, string name)
    {
        var attributeName = name switch
        {
            "Name" => Xaml + "Name",
            _ when name.Contains('.', StringComparison.Ordinal) => name,
            _ => name
        };
        return (string?)element.Attribute(attributeName);
    }

    private static string[] GridColumnWidths(XElement grid) =>
        grid.Element(Presentation + "Grid.ColumnDefinitions")!
            .Elements(Presentation + "ColumnDefinition")
            .Select(column => Attribute(column, "Width")!)
            .ToArray();

    private static string StyleSetter(string key, string property) =>
        Document.Descendants(Presentation + "Style")
            .Single(style => (string?)style.Attribute(Xaml + "Key") == key)
            .Elements(Presentation + "Setter")
            .Single(setter => Attribute(setter, "Property") == property)
            .Attribute("Value")!.Value;

    private static string EnterHandlerBranch(string startMarker, string endMarker)
    {
        var source = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml.cs");
        var handlerStart = source.IndexOf(
            "private async Task HandleEnterKeyAsync",
            StringComparison.Ordinal);
        var branchStart = source.IndexOf(
            $"if ({startMarker}",
            handlerStart,
            StringComparison.Ordinal);
        var branchEnd = source.IndexOf(
            endMarker.StartsWith("//", StringComparison.Ordinal)
                ? endMarker
                : $"if ({endMarker}",
            branchStart + 1,
            StringComparison.Ordinal);
        return source[branchStart..branchEnd];
    }

    private static TestContext CreateContext(params SalesCatalogProductDto[] products) =>
        CreateContext(false, products);

    private static TestContext CreateContext(bool confirmClear, params SalesCatalogProductDto[] products)
    {
        var service = new ProductServiceFake(products);
        var services = new ServiceCollection()
            .AddSingleton<IProductService>(service)
            .BuildServiceProvider();
        var currentUser = new CurrentUserService();
        currentUser.SetCurrentUser(new AuthenticatedUserDto(
            1, "cashier", "Thu ngân", Role.Cashier, DateTimeOffset.UtcNow));
        var confirmation = new ConfirmationFake(confirmClear);
        var viewModel = new SalesViewModel(
            services.GetRequiredService<IServiceScopeFactory>(),
            currentUser,
            new ReceiptFake(),
            new PaymentFake(),
            NullLogger<SalesViewModel>.Instance,
            confirmation);
        return new(viewModel, confirmation);
    }

    private static PaymentIntentRecoveryItemViewModel ConfirmedIntent() =>
        new(new PaymentIntentPendingDto(
            73,
            "VQ-0073",
            PaymentIntentStatus.Confirmed,
            145_000,
            "VND",
            "PAY VQ-0073",
            "persisted-payload",
            "970415",
            "123456789",
            "POS TEST",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow,
            null,
            null,
            false,
            false,
            false,
            false,
            true,
            false,
            "KHÔNG YÊU CẦU KHÁCH CHUYỂN THÊM."));

    private static Task<Exception?> RunOnStaAsync(Action action)
    {
        var completion = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                completion.SetResult(null);
            }
            catch (Exception exception)
            {
                completion.SetResult(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static SalesCartLineViewModel CreateLine(TestContext context, SalesCatalogProductDto product) =>
        new(
            new SalesProductCardViewModel(product, _ => Task.CompletedTask),
            _ => { },
            _ => { },
            () => true,
            () => { });

    private static SalesCatalogProductDto Product(
        string barcode, int id = 1, int stock = 10, bool isArchived = false) =>
        new(id, 1, "Đồ uống", $"P{id}", barcode, $"Sản phẩm {id}", "chai",
            10_000, stock, 1, true, false, stock <= 1, stock <= 0, true, isArchived);

    private static void SetField(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static string PathOf(params string[] parts) =>
        Path.Combine([FindRoot(), .. parts]);

    private static string Read(params string[] parts) =>
        File.ReadAllText(PathOf(parts));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private static string RunGitDiff()
    {
        var start = new System.Diagnostics.ProcessStartInfo("git", "diff --unified=0")
        {
            WorkingDirectory = FindRoot(),
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private sealed record TestContext(SalesViewModel ViewModel, ConfirmationFake Confirmation);

    private sealed class ProductServiceFake(IEnumerable<SalesCatalogProductDto> products) : IProductService
    {
        private readonly SalesCatalogProductDto[] _products = products.ToArray();

        public Task<Result<SalesCatalogProductDto>> FindSalesExactAsync(
            string scanOrCode, CancellationToken cancellationToken = default)
        {
            var product = _products.FirstOrDefault(x =>
                string.Equals(x.Barcode, scanOrCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Code, scanOrCode, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(product is null
                ? Result.Failure<SalesCatalogProductDto>(
                    new AppError(ErrorCodes.Products.NotFound, "Không tìm thấy sản phẩm."))
                : Result.Success(product));
        }

        public Task<Result<PagedResult<ProductListItemDto>>> SearchAsync(
            ProductSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new PagedResult<ProductListItemDto>(
                [], request.PageNumber, request.PageSize, 0)));

        public Task<Result<ProductDetailsDto>> GetByIdAsync(int productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<ProductDetailsDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<ProductDetailsDto>> UpdateAsync(UpdateProductRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> SetActiveStateAsync(int productId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ArchiveAsync(int productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> RestoreAsync(int productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ReceiptFake : IReceiptPreviewService
    {
        public Task ShowAsync(POS.Application.DTOs.Printing.ReceiptRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PaymentFake : ISalesPaymentFlowService
    {
        public bool IsVietQrEnabled => false;
        public Task<Result<SalesPaymentAuthorizationOutcome>> AuthorizeAsync(
            SalesPaymentAuthorizationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ConfirmationFake(bool confirmClear) : ICheckoutRecoveryConfirmationService
    {
        public int ClearCalls { get; private set; }
        public bool ConfirmAbandon() => false;
        public bool ConfirmClearCart()
        {
            ClearCalls++;
            return confirmClear;
        }
    }
}
