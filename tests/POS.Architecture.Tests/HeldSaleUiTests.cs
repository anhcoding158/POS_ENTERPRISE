using System.Text;
using POS.Application.DTOs.HeldSales;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class HeldSaleUiTests
{
    [Fact]
    public void List_search_filters_code_label_and_note()
    {
        RunOnSta(() =>
        {
            var window = new HeldSalesWindow(
            [
                CreateSummary(1, "HDG-001", "Khách áo xanh", "Bàn 05"),
                CreateSummary(2, "HDG-002", "Khách quen", "Giao cuối ngày")
            ]);
            window.SearchText = "áo xanh";
            Assert.Single(window.FilteredItems);
            Assert.Equal(1, window.FilteredItems[0].Id);

            window.SearchText = "HDG-002";
            Assert.Single(window.FilteredItems);
            Assert.Equal(2, window.FilteredItems[0].Id);

            window.SearchText = "Bàn 05";
            Assert.Single(window.FilteredItems);
            Assert.Equal(1, window.FilteredItems[0].Id);
            window.Close();
            return true;
        });
    }

    [Fact]
    public void Price_change_requires_acceptance()
    {
        RunOnSta(() =>
        {
            var window = new HeldSaleResumeWindow(CreateResume(
                HeldSaleResumeLineStatus.PriceChanged,
                snapshotPrice: 10_000,
                currentPrice: 12_000,
                stock: 5));
            var row = Assert.Single(window.Lines);

            Assert.True(row.Include);
            Assert.False(row.CurrentPriceAccepted);
            Assert.False(window.CanSubmit);

            row.CurrentPriceAccepted = true;

            Assert.True(row.IsValid);
            Assert.True(window.CanSubmit);
            window.Close();
            return true;
        });
    }

    [Fact]
    public void Insufficient_stock_requires_manual_quantity()
    {
        RunOnSta(() =>
        {
            var window = new HeldSaleResumeWindow(CreateResume(
                HeldSaleResumeLineStatus.InsufficientStock,
                snapshotPrice: 10_000,
                currentPrice: 10_000,
                stock: 2,
                quantity: 4));
            var row = Assert.Single(window.Lines);

            Assert.False(row.Include);
            row.Include = true;
            Assert.False(row.IsValid);

            row.Quantity = 2;

            Assert.True(row.IsValid);
            Assert.True(window.CanSubmit);
            window.Close();
            return true;
        });
    }

    [Fact]
    public void Unavailable_line_cannot_enter_cart()
    {
        RunOnSta(() =>
        {
            var window = new HeldSaleResumeWindow(CreateResume(
                HeldSaleResumeLineStatus.Unavailable,
                snapshotPrice: 10_000,
                currentPrice: null,
                stock: null));
            var row = Assert.Single(window.Lines);

            row.Include = true;

            Assert.False(row.Include);
            Assert.False(row.IsValid);
            Assert.False(window.CanSubmit);
            window.Close();
            return true;
        });
    }

    [Fact]
    public void Held_sale_windows_construct_on_STA()
    {
        RunOnSta(() =>
        {
            var hold = new HeldSaleHoldWindow(2, 3, 30_000, Guid.NewGuid());
            var list = new HeldSalesWindow([CreateSummary(1, "HDG-001", "Khách", null)]);
            var resume = new HeldSaleResumeWindow(CreateResume(
                HeldSaleResumeLineStatus.Unchanged,
                10_000,
                10_000,
                5));

            hold.Close();
            list.Close();
            resume.Close();
            return true;
        });
    }

    [Fact]
    public void Held_sale_xaml_preserves_binding_and_layout_contracts()
    {
        var hold = Read("src", "POS.Wpf", "Views", "HeldSaleHoldWindow.xaml");
        var list = Read("src", "POS.Wpf", "Views", "HeldSalesWindow.xaml");
        var resume = Read("src", "POS.Wpf", "Views", "HeldSaleResumeWindow.xaml");
        var sales = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml");

        Assert.Contains("Giữ đơn chưa tạo hóa đơn", hold, StringComparison.Ordinal);
        Assert.Contains("Đơn đang giữ", sales, StringComparison.Ordinal);
        Assert.Contains("Giữ đơn", sales, StringComparison.Ordinal);
        Assert.Contains("Mở lại", list, StringComparison.Ordinal);
        Assert.Contains("Hủy đơn giữ", list, StringComparison.Ordinal);
        Assert.Contains("Dùng giá hiện tại", resume, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", hold, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", list, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", resume, StringComparison.Ordinal);
    }

    private static HeldSaleDto CreateSummary(int id, string code, string label, string? notes) =>
        new(
            id,
            Guid.NewGuid(),
            code,
            label,
            notes,
            1,
            "Thu ngân",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            20_000,
            2,
            []);

    private static HeldSaleResumeDto CreateResume(
        HeldSaleResumeLineStatus status,
        long snapshotPrice,
        long? currentPrice,
        int? stock,
        int quantity = 2) =>
        new(
            1,
            "HDG-001",
            "Khách",
            "Ghi chú",
            [
                new HeldSaleResumeLineDto(
                    1,
                    "SP001",
                    "Sản phẩm",
                    quantity,
                    snapshotPrice,
                    currentPrice.HasValue ? "SP001" : null,
                    currentPrice.HasValue ? "Sản phẩm hiện tại" : null,
                    currentPrice.HasValue ? "Cái" : null,
                    currentPrice,
                    stock,
                    true,
                    false,
                    status,
                    status == HeldSaleResumeLineStatus.Unavailable
                        ? "Sản phẩm không còn bán."
                        : null,
                    null)
            ]);

    private static T RunOnSta<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<(T? Result, Exception? Error)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var application = new POS.Wpf.App();
                    application.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                    application.InitializeComponent();
                }

                completion.SetResult((action(), null));
            }
            catch (Exception exception)
            {
                completion.SetResult((default, exception));
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        var outcome = completion.Task.GetAwaiter().GetResult();
        thread.Join();
        if (outcome.Error is not null)
            throw new Xunit.Sdk.XunitException(
                $"STA construction failed: {outcome.Error}");
        return outcome.Result!;
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine([FindRepositoryRoot(), .. segments]),
            Encoding.UTF8);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
