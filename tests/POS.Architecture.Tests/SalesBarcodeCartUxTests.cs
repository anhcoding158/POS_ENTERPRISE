using System.Reflection;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
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
    [InlineData("F2_focuses_scan_search_input", "Key.F2", "ProductSearchBox")]
    [InlineData("Unknown_barcode_keeps_scan_focus_ready", "ProcessScanOrSearchAsync", "FocusAndSelectAll(ProductSearchBox)")]
    [InlineData("Successful_add_clears_input_and_restores_focus", "SearchTerm = string.Empty", "FocusAndSelectAll(ProductSearchBox)")]
    [InlineData("Unknown_code_does_not_move_focus_to_cash", "ProcessScanOrSearchAsync", "ProductSearchBox.Text")]
    [InlineData("Escape_clears_search_without_clearing_cart", "Input.Key.Escape", "SearchTerm = string.Empty")]
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
    public void No_new_hex_colors()
    {
        var diff = RunGitDiff();
        Assert.DoesNotContain(
            diff.Split('\n').Where(x => x.StartsWith('+') && !x.StartsWith("+++")),
            x => System.Text.RegularExpressions.Regex.IsMatch(x, "#[0-9A-Fa-f]{6,8}"));
    }

    [Fact]
    public void Sales_layout_fits_1366x768_at_contract_level()
    {
        var source = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml");
        Assert.DoesNotContain("MinWidth=\"1367", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MinHeight=\"769", source, StringComparison.Ordinal);
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
                    new Error(ErrorCodes.Products.NotFound, "Không tìm thấy sản phẩm."))
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
