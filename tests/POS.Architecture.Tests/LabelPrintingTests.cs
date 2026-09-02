using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Printing;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Printing;
using POS.Application.DTOs.Products;
using POS.Application.Printing;
using POS.Infrastructure.Printing;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.StoreSetup;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class LabelPrintingTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 1, 2, 30, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedCodes = ["A", "C"];

    [Fact]
    public void Snapshot_uses_one_date_and_deduplicates_product_ids_without_cost_price()
    {
        var products = new[]
        {
            new LabelProductSnapshot(1, "A", "Cà phê sữa đá", 35_000, "8938500012345", true, 2),
            new LabelProductSnapshot(1, "A", "Dữ liệu trùng", 99_000, "8938500012345", true, 7)
        };

        var job = LabelJobSnapshot.Create(FixedNow, products, LabelTemplate.Standard50x30);

        Assert.Single(job.Products);
        Assert.Equal("01/09/2026", job.PrintDateText);
        Assert.Equal(2, job.TotalLabels);
        Assert.Equal(35_000, job.Products[0].SalePrice);
        Assert.DoesNotContain("Cost", string.Join('|', job.Products.Select(x => x.ProductName)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Millimetre_conversion_roundtrips_with_explicit_tolerance()
    {
        foreach (var millimetres in new[] { 10d, 30d, 50d, 60.25d, 300d })
        {
            var dip = MillimetreConverter.ToDip(millimetres);
            Assert.Equal(millimetres, MillimetreConverter.ToMillimetres(dip), 8);
        }
        Assert.Equal(188.97637795, MillimetreConverter.ToDip(50), 6);
    }

    [Fact]
    public void Invalid_template_and_barcode_fail_closed_without_product_code_fallback()
    {
        var badTemplate = new LabelTemplate(LabelTemplateKind.Custom, "Sai", 0, 30);
        Assert.False(badTemplate.IsValid(out _));
        var outOfBoundsOffset = new LabelTemplate(LabelTemplateKind.Custom, "Lệch", 50, 30, 3, 0, 2);
        Assert.False(outOfBoundsOffset.IsValid(out _));

        var missingBarcode = new LabelProductSnapshot(1, "P-001", "Sản phẩm", 1_000, null, true);
        Assert.False(missingBarcode.HasValidBarcode);
        Assert.Contains("ProductCode", missingBarcode.BarcodeError, StringComparison.Ordinal);

        var unicodeBarcode = missingBarcode with { Barcode = "Mã-☕" };
        Assert.False(unicodeBarcode.HasValidBarcode);
    }

    [Fact]
    public void Non_sensitive_label_settings_roundtrip_without_using_the_manual_database()
    {
        var root = Path.Combine(Path.GetTempPath(), "POS-Enterprise-Label-Settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var databasePath = Path.Combine(root, "fixture.db");
            var paths = new StoreSettingsPathProvider(
                DatabaseRuntimeGuard.IsolatedTestMode,
                databasePath,
                root);
            var settings = new JsonLabelPrintSettingsStore(paths);
            settings.Save(new LabelPrintSettings
            {
                TemplateKind = LabelTemplateKind.Custom,
                WidthMm = 72.5,
                HeightMm = 38.25,
                OffsetXmm = 1.5,
                OffsetYmm = 0.75,
                InnerMarginMm = 2.25,
                PrinterName = "Fixture label printer"
            });

            var reloaded = new JsonLabelPrintSettingsStore(paths);
            Assert.Equal(LabelTemplateKind.Custom, reloaded.Current.TemplateKind);
            Assert.Equal(72.5, reloaded.Current.WidthMm);
            Assert.Equal(1.5, reloaded.Current.OffsetXmm);
            Assert.Equal("Fixture label printer", reloaded.Current.PrinterName);
            Assert.True(File.Exists(paths.LabelSettingsPath));
            Assert.False(File.Exists(databasePath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Paginator_has_exact_pages_and_renders_vector_bars_from_runtime_barcode()
    {
        RunOnSta(() =>
        {
            var job = LabelJobSnapshot.Create(
                FixedNow,
                [new LabelProductSnapshot(1, "P-001", "Tên tiếng Việt dài để thử wrap", 123_456, "8938500012345", true, 5)],
                LabelTemplate.Standard50x30);
            var paginator = LabelDocumentBuilder.Build(job);

            Assert.Equal(5, paginator.PageCount);
            Assert.Equal(MillimetreConverter.ToDip(50), paginator.PageSize.Width, 6);
            var page = paginator.GetPage(0);
            Assert.NotNull(page.Visual);
            var drawingVisual = Assert.IsType<global::System.Windows.Media.DrawingVisual>(page.Visual);
            Assert.NotEmpty(drawingVisual.Drawing.Children);
            var matrix = LabelBarcodeEncoder.Encode("8938500012345");
            Assert.True(matrix.Width > 0 && matrix.Height > 0);
            Assert.Contains(Enumerable.Range(0, matrix.Width), column => matrix[column, 0]);

            var testPaginator = LabelDocumentBuilder.Build(job, isTestPrint: true);
            Assert.Equal(1, testPaginator.PageCount);
        });
    }

    [Fact]
    public void ViewModel_sends_two_plus_three_labels_in_stable_order_and_test_sends_one()
    {
        var printer = new FakePrinterCatalog(new LabelPrinterInfo("Tem giả lập", true));
        var printing = new RecordingLabelPrintingService();
        var allRows = CreateRows();
        var viewModel = CreateViewModel(printing, printer, [allRows[0], allRows[2]]);
        var closeResult = (bool?)null;
        viewModel.RequestClose += result => closeResult = result;

        viewModel.Products[0].QuantityText = "2";
        viewModel.Products[1].QuantityText = "3";

        Assert.True(viewModel.IsPreviewValid);
        Assert.Equal(5, viewModel.TotalLabels);
        Assert.Equal("A", viewModel.PreviewProduct!.ProductCode);
        Assert.True(viewModel.TestPrintCommand.CanExecute(null));
        viewModel.TestPrintCommand.Execute(null);
        WaitUntil(() => printing.LastRequest is not null);
        WaitUntil(() => !viewModel.IsBusy);
        Assert.True(printing.LastRequest!.IsTestPrint);
        Assert.Equal(1, printing.LastRequest.EffectiveLabelCount);
        Assert.Null(closeResult);

        viewModel.PrintCommand.Execute(null);
        WaitUntil(() => closeResult == true);
        Assert.False(printing.LastRequest!.IsTestPrint);
        Assert.Equal(5, printing.LastRequest.EffectiveLabelCount);
        Assert.Equal(ExpectedCodes, printing.LastRequest.Job.Products.Select(x => x.ProductCode));
    }

    [Fact]
    public void Production_label_window_resolves_resources_and_preview_bounds_on_sta()
    {
        RunOnSta(() =>
        {
            if (global::System.Windows.Application.Current is null)
            {
                var application = new POS.Wpf.App();
                application.InitializeComponent();
            }
            var allRows = CreateRows();
            using var viewModel = CreateViewModel(
                new RecordingLabelPrintingService(),
                new FakePrinterCatalog(new LabelPrinterInfo("Tem giả lập", true)),
                [allRows[0], allRows[2]]);
            var window = new POS.Wpf.Views.LabelPrintWindow(viewModel);
            window.Show();
            window.Measure(new global::System.Windows.Size(1180, 760));
            window.Arrange(new global::System.Windows.Rect(0, 0, 1180, 760));
            window.UpdateLayout();
            window.Close();
        });
    }

    [Fact]
    public void Production_controls_have_active_bindings_and_clicks_reach_the_real_view_model()
    {
        RunOnSta(() =>
        {
            EnsureApplicationResources();
            using var bindingTrace = new StringWriter();
            var bindingListener = new TextWriterTraceListener(bindingTrace);
            var previousBindingLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
            PresentationTraceSources.DataBindingSource.Listeners.Add(bindingListener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            var bindingOutput = string.Empty;
            try
            {
                var catalog = new SequencedPrinterCatalog(
                    new[] { new LabelPrinterInfo("Fixture printer", true) },
                    new[] { new LabelPrinterInfo("Fixture printer", true), new LabelPrinterInfo("Second printer", true) },
                    new[] { new LabelPrinterInfo("Second printer", true) },
                    new[] { new LabelPrinterInfo("Second printer", true) });
                var printing = new RecordingLabelPrintingService();
                var scheduler = new ManualPreviewDebounceScheduler();
                using var viewModel = CreateViewModel(printing, catalog, new[] { CreateRows()[0] }, scheduler);
                var window = new POS.Wpf.Views.LabelPrintWindow(viewModel);
                window.Show();
                window.UpdateLayout();

                Assert.Same(viewModel, window.DataContext);
                Assert.Null(window.FindName("UpdatePreviewButton"));
                var refresh = Assert.IsType<Button>(window.FindName("RefreshPrintersButton"));
                var close = Assert.IsType<Button>(window.FindName("CloseButton"));
                var testPrint = Assert.IsType<Button>(window.FindName("TestPrintButton"));
                var print = Assert.IsType<Button>(window.FindName("PrintButton"));
                var templates = Assert.IsType<ComboBox>(window.FindName("TemplateComboBox"));
                var expander = Assert.IsType<Expander>(window.FindName("AlignmentExpander"));

                foreach (var button in new[] { refresh, close, print })
                {
                    Assert.NotNull(button.Command);
                    Assert.NotNull(button.GetBindingExpression(Button.CommandProperty));
                    Assert.Equal(BindingStatus.Active, button.GetBindingExpression(Button.CommandProperty)!.Status);
                }
                Assert.NotNull(testPrint.Command);
                Assert.Equal("50 × 30 mm", templates.Text);
                Assert.Equal("In 1 tem", print.Content);
                Assert.Equal("1 tem • ngày in 01/09/2026", viewModel.PreviewMessage);
                Assert.False(expander.IsExpanded);
                foreach (var textBlock in new[]
                         {
                             Assert.IsType<TextBlock>(window.FindName("PreviewStatusText")),
                             Assert.IsType<TextBlock>(window.FindName("StatusText"))
                         })
                {
                    Assert.NotNull(textBlock.GetBindingExpression(TextBlock.TextProperty));
                    Assert.Equal(BindingStatus.Active, textBlock.GetBindingExpression(TextBlock.TextProperty)!.Status);
                }
                Assert.True(refresh.IsHitTestVisible);
                Assert.True(close.IsHitTestVisible);
                Assert.True(print.IsHitTestVisible);
                Assert.True(refresh.IsEnabled);
                Assert.True(print.IsEnabled);

                InvokeButton(refresh);
                Assert.Equal(2, catalog.DiscoverCalls);
                Assert.Equal(2, viewModel.Printers.Count);
                Assert.Equal("Fixture printer", viewModel.SelectedPrinter!.Name);
                Assert.Contains("Đã làm mới", viewModel.StatusMessage, StringComparison.Ordinal);

                var previousPreview = viewModel.PreviewProduct;
                viewModel.WidthText = "51";
                Assert.False(viewModel.IsPreviewValid);
                Assert.Equal("In tem", print.Content);
                Assert.Equal(1, scheduler.PendingCount);
                scheduler.RunLatest();
                Assert.NotSame(previousPreview, viewModel.PreviewProduct);
                Assert.Equal("In 1 tem", print.Content);

                viewModel.SelectedTemplate = viewModel.TemplateOptions[1];
                Assert.Equal("60 × 40 mm", templates.Text);
                scheduler.RunLatest();
                viewModel.SelectedTemplate = viewModel.TemplateOptions[0];
                Assert.Equal("50 × 30 mm", templates.Text);
                scheduler.RunLatest();

                expander.IsExpanded = true;
                window.UpdateLayout();
                Assert.True(testPrint.IsEnabled);
                InvokeButton(testPrint);
                Assert.Equal(1, printing.CallCount);
                Assert.True(printing.LastRequest!.IsTestPrint);
                Assert.Contains("Đã gửi 1 tem kiểm tra", viewModel.StatusMessage, StringComparison.Ordinal);
                InvokeButton(refresh);
                Assert.Null(viewModel.SelectedPrinter);
                Assert.Contains("không còn khả dụng", viewModel.PrinterWarning, StringComparison.Ordinal);
                Assert.False(print.IsEnabled);
                InvokeButton(refresh);
                Assert.Null(viewModel.SelectedPrinter);
                Assert.Contains("không thay đổi", viewModel.StatusMessage, StringComparison.Ordinal);
                InvokeButton(close);
                Assert.False(window.IsVisible);
            }
            finally
            {
                bindingListener.Flush();
                bindingOutput = bindingTrace.ToString();
                PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingListener);
                PresentationTraceSources.DataBindingSource.Switch.Level = previousBindingLevel;
                bindingListener.Dispose();
            }

            Assert.DoesNotContain("path error", bindingOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Cannot find source", bindingOutput, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Production_quantity_changes_auto_rebuild_without_manual_preview_and_invalid_state_is_empty()
    {
        RunOnSta(() =>
        {
            EnsureApplicationResources();
            var scheduler = new ManualPreviewDebounceScheduler();
            using var viewModel = CreateViewModel(
                new RecordingLabelPrintingService(),
                new FakePrinterCatalog(new LabelPrinterInfo("Fixture printer", true)),
                new[] { CreateRows()[0] },
                scheduler);
            var window = new POS.Wpf.Views.LabelPrintWindow(viewModel);
            window.Show();
            window.UpdateLayout();
            var print = Assert.IsType<Button>(window.FindName("PrintButton"));
            var pagination = Assert.IsType<StackPanel>(window.FindName("PreviewPaginationPanel"));
            var empty = Assert.IsType<TextBlock>(window.FindName("PreviewEmptyStateText"));
            var summary = Assert.IsType<TextBlock>(window.FindName("ValidationSummaryText"));

            viewModel.Products[0].QuantityText = string.Empty;
            Assert.False(viewModel.IsPreviewValid);
            Assert.Equal("In tem", print.Content);
            scheduler.RunLatest();
            window.UpdateLayout();
            Assert.Equal(0, viewModel.PreviewPageCount);
            Assert.Equal(0, viewModel.PreviewPageNumber);
            Assert.Equal(Visibility.Collapsed, pagination.Visibility);
            Assert.Equal(Visibility.Visible, empty.Visibility);
            Assert.Equal("Nhập số lượng từ 1 đến 1.000.", viewModel.Products[0].ErrorText);
            Assert.Empty(viewModel.ValidationSummary);
            Assert.Equal(string.Empty, summary.Text);

            viewModel.Products[0].QuantityText = "2";
            scheduler.RunLatest();
            window.UpdateLayout();
            Assert.True(viewModel.IsPreviewValid);
            Assert.Equal(2, viewModel.TotalLabels);
            Assert.Equal("In 2 tem", print.Content);
            Assert.Equal(2, viewModel.PreviewPageCount);
            Assert.Equal(Visibility.Visible, pagination.Visibility);
            Assert.Equal(Visibility.Collapsed, empty.Visibility);
            Assert.Equal("2 tem • ngày in 01/09/2026", viewModel.PreviewMessage);
            window.Close();
        });
    }

    [Fact]
    public void Preview_debounce_discards_older_input_and_reports_one_multi_product_summary()
    {
        var scheduler = new ManualPreviewDebounceScheduler();
        using var viewModel = CreateViewModel(
            new RecordingLabelPrintingService(),
            new FakePrinterCatalog(new LabelPrinterInfo("Fixture printer", true)),
            CreateRows(),
            scheduler);

        viewModel.SelectedTemplate = viewModel.TemplateOptions[2];
        scheduler.RunLatest();
        viewModel.WidthText = "51";
        viewModel.WidthText = "52";
        Assert.Equal(3, scheduler.ScheduleCalls);
        Assert.Equal(1, scheduler.PendingCount);
        scheduler.RunLatest();
        Assert.Equal(52, viewModel.PreviewTemplate.WidthMm);
        Assert.True(viewModel.IsPreviewValid);

        viewModel.Products[0].QuantityText = string.Empty;
        viewModel.Products[1].QuantityText = string.Empty;
        scheduler.RunLatest();
        Assert.Equal("Còn 2 sản phẩm cần nhập dữ liệu hợp lệ.", viewModel.ValidationSummary);
        Assert.Contains("Nhập số lượng từ 1 đến 1.000.", viewModel.Products[0].ErrorText, StringComparison.Ordinal);
        Assert.Contains("Nhập số lượng từ 1 đến 1.000.", viewModel.Products[1].ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_print_button_updates_content_and_escape_closes_window()
    {
        RunOnSta(() =>
        {
            EnsureApplicationResources();
            var rows = CreateRows();
            var printing = new RecordingLabelPrintingService();
            using var viewModel = CreateViewModel(
                printing,
                new FakePrinterCatalog(new LabelPrinterInfo("Fixture printer", true)),
                new[] { rows[0] });
            var window = new POS.Wpf.Views.LabelPrintWindow(viewModel);
            window.Show();
            window.UpdateLayout();
            var print = Assert.IsType<Button>(window.FindName("PrintButton"));
            viewModel.Products[0].QuantityText = "3";
            Assert.Equal("In 3 tem", print.Content);
            InvokeButton(print);
            Assert.Equal(1, printing.CallCount);
            Assert.Equal(3, printing.LastRequest!.EffectiveLabelCount);
            Assert.False(printing.LastRequest.IsTestPrint);
            Assert.False(window.IsVisible);

            using var escapeViewModel = CreateViewModel(
                new RecordingLabelPrintingService(),
                new FakePrinterCatalog(new LabelPrinterInfo("Fixture printer", true)),
                new[] { CreateRows()[0] });
            var escapeWindow = new POS.Wpf.Views.LabelPrintWindow(escapeViewModel);
            escapeWindow.Show();
            escapeWindow.UpdateLayout();
            var keyArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(escapeWindow)!,
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            escapeWindow.RaiseEvent(keyArgs);
            Assert.False(escapeWindow.IsVisible);
        });
    }

    [Fact]
    public void Production_refresh_and_print_failures_are_visible_without_closing()
    {
        RunOnSta(() =>
        {
            EnsureApplicationResources();
            var catalog = new SequencedPrinterCatalog(
                new[] { new LabelPrinterInfo("Fixture printer", true) },
                new InvalidOperationException("catalog unavailable"));
            using var viewModel = CreateViewModel(
                new ThrowingLabelPrintingService(new InvalidOperationException("spool failed")),
                catalog,
                CreateRows());
            var window = new POS.Wpf.Views.LabelPrintWindow(viewModel);
            window.Show();
            window.UpdateLayout();
            var refresh = Assert.IsType<Button>(window.FindName("RefreshPrintersButton"));
            var testPrint = Assert.IsType<Button>(window.FindName("TestPrintButton"));

            InvokeButton(refresh);
            Assert.Contains("Không thể tải máy in", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.False(testPrint.IsEnabled);
            Assert.True(window.IsVisible);
            window.Close();
        });
    }

    [Fact]
    public void Production_test_print_exception_is_visible_and_window_stays_open()
    {
        RunOnSta(() =>
        {
            EnsureApplicationResources();
            using var viewModel = CreateViewModel(
                new ThrowingLabelPrintingService(new InvalidOperationException("spool failed")),
                new FakePrinterCatalog(new LabelPrinterInfo("Fixture printer", true)),
                new[] { CreateRows()[0] });
            var window = new POS.Wpf.Views.LabelPrintWindow(viewModel);
            window.Show();
            window.UpdateLayout();

            InvokeButton(Assert.IsType<Button>(window.FindName("TestPrintButton")));
            Assert.Contains("In thử thất bại: spool failed", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.True(window.IsVisible);
            window.Close();
        });
    }

    [Fact]
    public void Production_cancelled_test_print_is_reported_without_success_or_error()
    {
        RunOnSta(() =>
        {
            EnsureApplicationResources();
            using var viewModel = CreateViewModel(
                new CancelledLabelPrintingService(),
                new FakePrinterCatalog(new LabelPrinterInfo("Fixture printer", true)),
                new[] { CreateRows()[0] });
            var window = new POS.Wpf.Views.LabelPrintWindow(viewModel);
            window.Show();
            window.UpdateLayout();

            InvokeButton(Assert.IsType<Button>(window.FindName("TestPrintButton")));
            Assert.Equal("Đã hủy in thử.", viewModel.StatusMessage);
            Assert.True(window.IsVisible);
            window.Close();
        });
    }

    [Fact]
    public void No_printer_keeps_preview_available_but_locks_print_with_reason()
    {
        var viewModel = CreateViewModel(
            new RecordingLabelPrintingService(),
            new FakePrinterCatalog(),
            CreateRows());

        Assert.True(viewModel.IsPreviewValid);
        Assert.False(viewModel.PrintCommand.CanExecute(null));
        Assert.Contains("máy in", viewModel.CanPrintReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Double_click_does_not_dispatch_two_real_jobs()
    {
        var printing = new DelayedLabelPrintingService();
        using var viewModel = CreateViewModel(
            printing,
            new FakePrinterCatalog(new LabelPrinterInfo("Tem giả lập", true)),
            CreateRows());

        viewModel.PrintCommand.Execute(null);
        viewModel.PrintCommand.Execute(null);
        Assert.Equal(1, printing.CallCount);
        printing.Release();
        WaitUntil(() => !viewModel.IsBusy);
        Assert.Equal(1, printing.CallCount);
    }

    [Fact]
    public void Missing_barcode_is_reported_on_the_correct_product_and_blocks_print()
    {
        var rows = new[]
        {
            new ProductRowViewModel(Product(1, "A", "8938500012345")),
            new ProductRowViewModel(Product(3, "C", null))
        };
        var viewModel = CreateViewModel(
            new RecordingLabelPrintingService(),
            new FakePrinterCatalog(new LabelPrinterInfo("Tem giả lập", true)),
            rows);

        Assert.Empty(viewModel.Products[0].ErrorText);
        Assert.Contains("ProductCode", viewModel.Products[1].ErrorText, StringComparison.Ordinal);
        Assert.False(viewModel.IsPreviewValid);
        Assert.False(viewModel.PrintCommand.CanExecute(null));
    }

    [Fact]
    public async Task Printing_service_uses_fake_dispatcher_and_never_requires_a_physical_printer()
    {
        var dispatcher = new RecordingDispatcher();
        using var service = new WpfLabelPrintingService(
            new FakePrinterCatalog(new LabelPrinterInfo("Fixture printer", true)),
            dispatcher);
        var job = LabelJobSnapshot.Create(
            FixedNow,
            [new LabelProductSnapshot(1, "A", "Sản phẩm A", 20_000, "8938500012345", true, 4)],
            LabelTemplate.Standard60x40);

        var result = await service.PrintAsync(new LabelPrintRequest(job, "Fixture printer", false, 4));

        Assert.True(result.IsSuccess);
        Assert.NotNull(dispatcher.Request);
        Assert.Equal(4, dispatcher.Request!.EffectiveLabelCount);
        Assert.Equal(4, dispatcher.Request.RequestedLabelCount);
    }

    private static LabelPrintViewModel CreateViewModel(
        ILabelPrintingService printing,
        ILabelPrinterCatalog printer,
        IReadOnlyList<ProductRowViewModel> rows,
        ILabelPreviewDebounceScheduler? scheduler = null) =>
        new(
            rows,
            new FixedClock(FixedNow),
            printer,
            printing,
            new AllowPermissionService(),
            NullLogger<LabelPrintViewModel>.Instance,
            previewScheduler: scheduler ?? new ImmediatePreviewDebounceScheduler());

    private static ProductRowViewModel[] CreateRows() =>
    [
        new(Product(1, "A", "8938500012345", "Cà phê sữa đá")),
        new(Product(2, "B", "8938500099999", "Sản phẩm B")),
        new(Product(3, "C", "8938500077777", "Trà đào"))
    ];

    private static ProductListItemDto Product(int id, string code, string? barcode, string? name = null) =>
        new(id, 1, "Đồ uống", code, barcode, name ?? $"Sản phẩm {code}", "Ly", 10_000, 20_000, 10_000, 3, 1, true, false, false, false, true);

    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) Thread.Sleep(10);
        Assert.True(condition());
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (error is not null) throw error;
    }

    private static void EnsureApplicationResources()
    {
        if (global::System.Windows.Application.Current is null)
        {
            var application = new POS.Wpf.App();
            application.InitializeComponent();
        }
    }

    private static void InvokeButton(Button button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button) ?? new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
        button.Dispatcher.Invoke(DispatcherPriority.Input, new Action(() => { }));
    }

    private sealed class ImmediatePreviewDebounceScheduler : ILabelPreviewDebounceScheduler
    {
        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            callback();
            return NoopDisposable.Instance;
        }
    }

    private sealed class ManualPreviewDebounceScheduler : ILabelPreviewDebounceScheduler
    {
        private readonly List<PendingPreview> _pending = [];

        public int ScheduleCalls { get; private set; }
        public int PendingCount => _pending.Count(item => !item.Cancelled);

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            ScheduleCalls++;
            var pending = new PendingPreview(callback);
            _pending.Add(pending);
            return new Cancellation(() => pending.Cancelled = true);
        }

        public void RunLatest()
        {
            var pending = _pending.LastOrDefault(item => !item.Cancelled)
                ?? throw new InvalidOperationException("Không có preview debounce đang chờ.");
            pending.Cancelled = true;
            pending.Callback();
        }

        private sealed class PendingPreview(Action callback)
        {
            public Action Callback { get; } = callback;
            public bool Cancelled { get; set; }
        }

        private sealed class Cancellation(Action cancel) : IDisposable
        {
            public void Dispose() => cancel();
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();
        public void Dispose() { }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class AllowPermissionService : IPermissionService
    {
        public bool HasPermission(SystemCapability permission) => true;
        public Result Authorize(SystemCapability permission) => Result.Success();
    }

    private sealed class FakePrinterCatalog(params LabelPrinterInfo[] printers) : ILabelPrinterCatalog
    {
        public IReadOnlyList<LabelPrinterInfo> Discover() => printers;
    }

    private sealed class SequencedPrinterCatalog : ILabelPrinterCatalog
    {
        private readonly Queue<object> _results;
        public int DiscoverCalls { get; private set; }

        public SequencedPrinterCatalog(params object[] results) => _results = new(results);

        public IReadOnlyList<LabelPrinterInfo> Discover()
        {
            DiscoverCalls++;
            var result = _results.Count == 0 ? Array.Empty<LabelPrinterInfo>() : _results.Dequeue();
            if (result is Exception exception) throw exception;
            return (IReadOnlyList<LabelPrinterInfo>)result;
        }
    }

    private sealed class RecordingLabelPrintingService : ILabelPrintingService
    {
        public LabelPrintRequest? LastRequest { get; private set; }
        public int CallCount { get; private set; }
        public Task<Result> PrintAsync(LabelPrintRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class DelayedLabelPrintingService : ILabelPrintingService
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }
        public Task<Result> PrintAsync(LabelPrintRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return WaitAsync();
        }
        private async Task<Result> WaitAsync()
        {
            await _release.Task;
            return Result.Success();
        }
        public void Release() => _release.TrySetResult();
    }

    private sealed class ThrowingLabelPrintingService(Exception exception) : ILabelPrintingService
    {
        public Task<Result> PrintAsync(LabelPrintRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<Result>(exception);
    }

    private sealed class CancelledLabelPrintingService : ILabelPrintingService
    {
        public Task<Result> PrintAsync(LabelPrintRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure(new AppError(ErrorCodes.Printing.Cancelled, "Người dùng đã hủy.")));
    }

    private sealed class RecordingDispatcher : ILabelPrintDispatcher
    {
        public LabelPrintRequest? Request { get; private set; }
        public Task<Result> DispatchAsync(LabelPrintRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(Result.Success());
        }
    }
}
