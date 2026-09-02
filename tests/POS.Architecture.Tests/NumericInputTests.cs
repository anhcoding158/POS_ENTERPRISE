using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Data;
using POS.Application.Common;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Categories;
using POS.Application.DTOs.Products;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Wpf.Behaviors;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class NumericInputFormatterTests
{
    [Theory]
    [InlineData("35000", 35000)]
    [InlineData("35.000", 35000)]
    [InlineData("35,000", 35000)]
    [InlineData("35 000", 35000)]
    [InlineData("35\u00A0000", 35000)]
    [InlineData("35\u202F000", 35000)]
    [InlineData("0", 0)]
    [InlineData("0003", 3)]
    public void Strict_integer_input_accepts_supported_grouping(
        string input,
        long expected)
    {
        Assert.True(
            NumericInputFormatter.TryParse(
                input,
                NumericInputMode.MoneyVnd,
                out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(35000, "35.000")]
    [InlineData(0, "0")]
    public void Vnd_and_integer_values_use_vietnamese_grouping(
        long value,
        string expected)
    {
        Assert.Equal(
            expected,
            NumericInputFormatter.Format(
                value,
                NumericInputMode.MoneyVnd));
        Assert.Equal(
            expected,
            NumericInputFormatter.Format(
                value,
                NumericInputMode.NonNegativeInteger));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12,34")]
    [InlineData("12.34")]
    [InlineData("1..000")]
    [InlineData("1,,000")]
    [InlineData("1.000,000")]
    [InlineData("1,000.000")]
    [InlineData(".1000")]
    [InlineData("1000.")]
    [InlineData("-1")]
    [InlineData("1a")]
    [InlineData("35.00")]
    [InlineData("9223372036854775808")]
    public void Strict_integer_input_rejects_ambiguous_or_invalid_text(
        string? input)
    {
        Assert.False(
            NumericInputFormatter.TryParse(
                input,
                NumericInputMode.NonNegativeInteger,
                out _));
    }

    [Theory]
    [InlineData("-1500", -1500)]
    [InlineData("-1.500", -1500)]
    public void Signed_integer_input_preserves_the_existing_opening_stock_contract(
        string input,
        long expected)
    {
        Assert.True(
            NumericInputFormatter.TryParse(
                input,
                NumericInputMode.SignedInteger,
                out var actual));
        Assert.Equal(expected, actual);
        Assert.Equal(
            input.Replace("1500", "1.500", StringComparison.Ordinal),
            NumericInputFormatter.Format(actual, NumericInputMode.SignedInteger));
    }
}

public sealed class NumericInputEditEngineTests
{
    [Theory]
    [InlineData("1.500", 5, 0, 0, "0", "15.000", 6)]
    [InlineData("1.500", 2, 0, 0, "9", "19.500", 2)]
    [InlineData("1.500", 5, 0, 0, "", "1.500", 5)]
    public void Insert_uses_digit_position_not_display_character_position(
        string display,
        int caret,
        int selectionStart,
        int selectionLength,
        string inserted,
        string expectedDisplay,
        int expectedCaret)
    {
        var result = NumericInputEditEngine.Insert(
            display,
            caret,
            selectionStart,
            selectionLength,
            inserted,
            NumericInputMode.MoneyVnd);

        Assert.True(result.IsValid);
        Assert.Equal(expectedDisplay, result.DisplayText);
        Assert.Equal(expectedCaret, result.CaretIndex);
    }

    [Theory]
    [InlineData("15.000", 6, "1.500")]
    [InlineData("1.500", 5, "150")]
    [InlineData("150", 3, "15")]
    public void Backspace_removes_the_previous_digit_across_grouping(
        string display,
        int caret,
        string expected)
    {
        var result = NumericInputEditEngine.Backspace(
            display, caret, 0, 0, NumericInputMode.MoneyVnd);
        Assert.Equal(expected, result.DisplayText);
    }

    [Fact]
    public void Delete_at_separator_removes_the_next_digit()
    {
        var result = NumericInputEditEngine.Delete(
            "1.500", 2, 0, 0, NumericInputMode.MoneyVnd);
        Assert.Equal("100", result.DisplayText);
        Assert.Equal(1, result.CaretIndex);
    }

    [Fact]
    public void Selection_replace_and_leading_zero_are_normalized()
    {
        var result = NumericInputEditEngine.Insert(
            "0", 1, 0, 1, "03", NumericInputMode.MoneyVnd);
        Assert.Equal("3", result.DisplayText);
        Assert.Equal(1, result.CaretIndex);
    }
}

public sealed class NumericInputBehaviorTests
{
    [Fact]
    public void Routed_digit_input_can_continue_after_grouping_separator()
    {
        RunOnSta(() =>
        {
            var textBox = new TextBox();
            NumericInputBehavior.SetMode(
                textBox,
                NumericInputMode.MoneyVnd);

            foreach (var character in "15000")
            {
                RaiseTextInput(textBox, character.ToString());
            }

            Assert.Equal("15.000", textBox.Text);
        });
    }

    [Fact]
    public void Routed_backspace_removes_digits_from_grouped_value()
    {
        RunOnSta(() =>
        {
            var textBox = new TextBox();
            NumericInputBehavior.SetMode(
                textBox,
                NumericInputMode.MoneyVnd);
            var window = new Window
            {
                Content = textBox,
                Width = 240,
                Height = 120
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                foreach (var character in "15000")
                {
                    RaiseTextInput(textBox, character.ToString());
                }

                Assert.Equal("15.000", textBox.Text);
                RaiseKeyInput(textBox, Key.Back);

                Assert.Equal("1.500", textBox.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Routed_backspace_sequence_removes_every_digit_without_rollback()
    {
        RunOnSta(() =>
        {
            var textBox = CreateAttachedTextBox(NumericInputMode.MoneyVnd);
            using var window = new TestWindow(textBox);
            window.Show();
            window.UpdateLayout();

            foreach (var character in "15000")
            {
                RaiseTextInput(textBox, character.ToString());
            }

            foreach (var expected in new[] { "1.500", "150", "15", "1", string.Empty })
            {
                RaiseKeyInput(textBox, Key.Back);
                Assert.Equal(expected, textBox.Text);
            }
        });
    }

    [Fact]
    public void Routed_delete_at_group_separator_removes_the_next_digit()
    {
        RunOnSta(() =>
        {
            var textBox = CreateAttachedTextBox(NumericInputMode.MoneyVnd);
            using var window = new TestWindow(textBox);
            window.Show();
            window.UpdateLayout();
            textBox.Text = "1500";
            textBox.CaretIndex = 2;

            RaiseKeyInput(textBox, Key.Delete);

            Assert.Equal("100", textBox.Text);
        });
    }

    [Fact]
    public void Routed_insert_replaces_selection_and_preserves_digit_caret()
    {
        RunOnSta(() =>
        {
            var textBox = CreateAttachedTextBox(NumericInputMode.MoneyVnd);
            using var window = new TestWindow(textBox);
            window.Show();
            window.UpdateLayout();
            textBox.Text = "15000";
            textBox.Select(0, textBox.Text.Length);

            RaiseTextInput(textBox, "35000");

            Assert.Equal("35.000", textBox.Text);
            Assert.Equal(textBox.Text.Length, textBox.CaretIndex);
        });
    }

    [Fact]
    public void Paste_applies_canonical_value_and_invalid_paste_keeps_old_value()
    {
        RunOnSta(() =>
        {
            var textBox = CreateAttachedTextBox(NumericInputMode.MoneyVnd);
            using var window = new TestWindow(textBox);
            window.Show();
            window.UpdateLayout();

            RaisePaste(textBox, "35 000");
            Assert.Equal("35.000", textBox.Text);

            RaisePaste(textBox, "12,34");
            Assert.Equal("35.000", textBox.Text);
        });
    }

    [Fact]
    public void Signed_input_keeps_negative_opening_stock_when_enabled()
    {
        RunOnSta(() =>
        {
            var textBox = CreateAttachedTextBox(NumericInputMode.SignedInteger);
            NumericInputBehavior.SetAllowNegative(textBox, true);
            using var window = new TestWindow(textBox);
            window.Show();
            window.UpdateLayout();

            RaiseTextInput(textBox, "-");
            foreach (var character in "1500")
            {
                RaiseTextInput(textBox, character.ToString());
            }

            Assert.Equal("-1.500", textBox.Text);
        });
    }

    [Fact]
    public void Binding_receives_one_canonical_value_per_logical_keyboard_edit()
    {
        RunOnSta(() =>
        {
            var source = new TrackingTextSource();
            var textBox = CreateAttachedTextBox(NumericInputMode.MoneyVnd);
            textBox.DataContext = source;
            BindingOperations.SetBinding(
                textBox,
                TextBox.TextProperty,
                new Binding(nameof(TrackingTextSource.Value))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            using var window = new TestWindow(textBox);
            window.Show();
            window.UpdateLayout();

            foreach (var character in "15000")
            {
                RaiseTextInput(textBox, character.ToString());
            }

            Assert.Equal("15.000", source.Value);
            Assert.Equal("15.000", textBox.Text);
            Assert.Equal(5, source.SetterCount);
        });
    }

    private sealed class TrackingTextSource
    {
        private string _value = string.Empty;
        public int SetterCount { get; private set; }
        public string Value
        {
            get => _value;
            set
            {
                SetterCount++;
                _value = value;
            }
        }
    }

    [Fact]
    public void Empty_text_is_draft_and_does_not_create_a_zero_source_value()
    {
        RunOnSta(() =>
        {
            var textBox = new TextBox();
            NumericInputBehavior.SetMode(
                textBox,
                NumericInputMode.NonNegativeInteger);
            NumericInputBehavior.SetPlaceholderText(textBox, "0");

            Assert.Equal(string.Empty, textBox.Text);
            Assert.Equal("0", NumericInputBehavior.GetPlaceholderText(textBox));
            Assert.False(
                NumericInputFormatter.TryParse(
                    textBox.Text,
                    NumericInputMode.NonNegativeInteger,
                    out _));
        });
    }

    [Fact]
    public void Text_change_is_grouped_without_changing_the_numeric_value()
    {
        RunOnSta(() =>
        {
            var textBox = new TextBox();
            NumericInputBehavior.SetMode(
                textBox,
                NumericInputMode.MoneyVnd);

            textBox.Text = "35000";

            Assert.Equal("35.000", textBox.Text);
            Assert.True(
                NumericInputFormatter.TryParse(
                    textBox.Text,
                    NumericInputMode.MoneyVnd,
                    out var value));
            Assert.Equal(35000, value);
        });
    }

    [Fact]
    public void Invalid_text_is_rejected_without_a_reentrancy_loop()
    {
        RunOnSta(() =>
        {
            var textBox = new TextBox();
            NumericInputBehavior.SetMode(
                textBox,
                NumericInputMode.MoneyVnd);
            textBox.Text = "12000";

            textBox.Text = "12,34";

            Assert.Equal("12.000", textBox.Text);
        });
    }

    [Fact]
    public void Pasting_accepts_grouped_numbers_and_cancels_invalid_text()
    {
        RunOnSta(() =>
        {
            var textBox = new TextBox();
            NumericInputBehavior.SetMode(
                textBox,
                NumericInputMode.MoneyVnd);

            var validData = new DataObject();
            validData.SetData(DataFormats.UnicodeText, "35 000");
            var validEvent = new DataObjectPastingEventArgs(
                validData,
                false,
                DataFormats.UnicodeText)
            {
                RoutedEvent = DataObject.PastingEvent
            };
            textBox.RaiseEvent(validEvent);

            var invalidData = new DataObject();
            invalidData.SetData(DataFormats.UnicodeText, "12,34");
            var invalidEvent = new DataObjectPastingEventArgs(
                invalidData,
                false,
                DataFormats.UnicodeText)
            {
                RoutedEvent = DataObject.PastingEvent
            };
            textBox.RaiseEvent(invalidEvent);

            Assert.True(validEvent.CommandCancelled);
            Assert.True(invalidEvent.CommandCancelled);
            Assert.Equal("35.000", textBox.Text);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    private static TextBox CreateAttachedTextBox(NumericInputMode mode)
    {
        var textBox = new TextBox();
        NumericInputBehavior.SetMode(textBox, mode);
        return textBox;
    }

    private static void RaisePaste(TextBox textBox, string value)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, value);
        var eventArgs = new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText)
        {
            RoutedEvent = DataObject.PastingEvent,
            Source = textBox
        };
        textBox.RaiseEvent(eventArgs);
    }

    private sealed class TestWindow : IDisposable
    {
        private readonly Window _window;

        public TestWindow(TextBox textBox)
        {
            _window = new Window
            {
                Content = textBox,
                Width = 240,
                Height = 120
            };
        }

        public void Show() => _window.Show();
        public void UpdateLayout() => _window.UpdateLayout();
        public void Dispose() => _window.Close();
    }

    private static void RaiseTextInput(TextBox textBox, string text)
    {
        var composition = new TextComposition(
            InputManager.Current,
            textBox,
            text);
        var eventArgs = new TextCompositionEventArgs(
            InputManager.Current.PrimaryKeyboardDevice,
            composition)
        {
            RoutedEvent = TextCompositionManager.PreviewTextInputEvent,
            Source = textBox
        };

        textBox.RaiseEvent(eventArgs);

        if (eventArgs.Handled)
        {
            return;
        }

        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;
        textBox.Text = textBox.Text
            .Remove(selectionStart, selectionLength)
            .Insert(selectionStart, text);
        textBox.CaretIndex = selectionStart + text.Length;
    }

    private static void RaiseKeyInput(TextBox textBox, Key key)
    {
        var eventArgs = new KeyEventArgs(
            InputManager.Current.PrimaryKeyboardDevice,
            PresentationSource.FromVisual(textBox),
            Environment.TickCount,
            key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
            Source = textBox
        };

        textBox.RaiseEvent(eventArgs);

        if (eventArgs.Handled || key != Key.Back)
        {
            return;
        }

        if (textBox.SelectionLength > 0)
        {
            textBox.Text = textBox.Text.Remove(
                textBox.SelectionStart,
                textBox.SelectionLength);
            textBox.CaretIndex = textBox.SelectionStart;
            return;
        }

        if (textBox.CaretIndex == 0)
        {
            return;
        }

        var caretIndex = textBox.CaretIndex;
        textBox.Text = textBox.Text.Remove(caretIndex - 1, 1);
        textBox.CaretIndex = caretIndex - 1;
    }
}

public sealed class NumericInputProductionControlTests
{
    [Fact]
    public void Product_editor_materializes_four_draft_numeric_fields()
    {
        RunOnSta(() =>
        {
            EnsureApplication();
            var viewModel = new ProductEditorViewModel(
                new EmptyScopeFactory(),
                NullLogger<ProductEditorViewModel>.Instance);
            var window = new ProductEditorWindow(viewModel);
            try
            {
                window.Show();
                window.Measure(new Size(980, 820));
                window.Arrange(new Rect(0, 0, 980, 820));
                window.UpdateLayout();

                var cost = (TextBox)window.FindName("ProductEditorCostPriceInput")!;
                var sale = (TextBox)window.FindName("ProductEditorSalePriceInput")!;
                var opening = (TextBox)window.FindName("ProductEditorInitialStockInput")!;
                var minimum = (TextBox)window.FindName("ProductEditorMinimumStockInput")!;

                Assert.Equal(string.Empty, cost.Text);
                Assert.Equal(string.Empty, sale.Text);
                Assert.Equal(string.Empty, opening.Text);
                Assert.Equal(string.Empty, minimum.Text);
                Assert.Equal(NumericInputMode.MoneyVnd, NumericInputBehavior.GetMode(cost));
                Assert.Equal(NumericInputMode.MoneyVnd, NumericInputBehavior.GetMode(sale));
                Assert.Equal(NumericInputMode.SignedInteger, NumericInputBehavior.GetMode(opening));
                Assert.Equal(NumericInputMode.NonNegativeInteger, NumericInputBehavior.GetMode(minimum));
                Assert.NotNull(AdornerLayer.GetAdornerLayer(cost)?.GetAdorners(cost));
                Assert.Equal(string.Empty, viewModel.CostPriceText);
                Assert.Equal(string.Empty, viewModel.InitialStockQuantityText);

                foreach (var character in "35000")
                {
                    RaiseTextInput(cost, character.ToString());
                }
                window.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.DataBind,
                    new Action(() => { }));
                Assert.Equal("35.000", cost.Text);
                Assert.Equal("35.000", viewModel.CostPriceText);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Bulk_price_controls_group_input_and_send_the_exact_integer_request()
    {
        RunOnSta(() =>
        {
            EnsureApplication();
            var row = new ProductRowViewModel(new ProductListItemDto(
                1, 2, "Đồ uống", "SP001", null, "Sữa", "Hộp",
                10000, 25000, 10, 3, 5, true, false, true, false, true));
            var service = new CapturingBulkService();
            using var viewModel = new BulkProductViewModel(
                [row],
                service,
                [new CategoryOptionDto(2, "Đồ uống", 0)]);
            var window = new BulkProductWindow(viewModel);
            try
            {
                window.Show();
                window.UpdateLayout();
                var sale = (TextBox)window.FindName("BulkSalePriceInput")!;
                var cost = (TextBox)window.FindName("BulkCostPriceInput")!;

                foreach (var character in "35000")
                {
                    RaiseTextInput(sale, character.ToString());
                }
                foreach (var character in "12000")
                {
                    RaiseTextInput(cost, character.ToString());
                }
                window.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.DataBind,
                    new Action(() => { }));

                Assert.Equal("35.000", sale.Text);
                Assert.Equal("12.000", cost.Text);
                viewModel.PreviewCommand.Execute(null);
                window.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.DataBind,
                    new Action(() => { }));

                Assert.Equal(35000, service.LastRequest!.SalePrice);
                Assert.Equal(12000, service.LastRequest.CostPrice);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Product_editor_profit_is_neutral_until_both_prices_are_valid()
    {
        var viewModel = new ProductEditorViewModel(
            new EmptyScopeFactory(),
            NullLogger<ProductEditorViewModel>.Instance);

        Assert.Equal("—", viewModel.ProfitPreviewText);
        viewModel.CostPriceText = "20000";
        Assert.Equal("—", viewModel.ProfitPreviewText);

        viewModel.SalePriceText = "50000";

        Assert.Contains("30.000", viewModel.ProfitPreviewText, StringComparison.Ordinal);
        Assert.False(viewModel.IsProfitNegative);
    }

    [Fact]
    public void Bulk_reference_price_display_uses_vietnamese_grouping()
    {
        var row = new ProductRowViewModel(new ProductListItemDto(
            1, 2, "Đồ uống", "SP001", null, "Sữa", "Hộp",
            10000, 35000, 10, 3, 5, true, false, true, false, true));

        var previewRow = new BulkProductPreviewRowViewModel(
            row,
            BulkProductOperationType.SetPrices);

        Assert.Contains("35.000 đ", previewRow.BeforeValue, StringComparison.Ordinal);
        Assert.Contains("10.000 đ", previewRow.BeforeValue, StringComparison.Ordinal);
    }

    [Fact]
    public void Opening_stock_respects_allow_negative_stock_contract()
    {
        var viewModel = new ProductEditorViewModel(
            new EmptyScopeFactory(),
            NullLogger<ProductEditorViewModel>.Instance);

        viewModel.InitialStockQuantityText = "-1500";
        Assert.NotNull(viewModel.InitialStockError);

        viewModel.AllowNegativeStock = true;

        Assert.Null(viewModel.InitialStockError);
        Assert.Contains("-1.500", viewModel.OpeningBalancePreviewText, StringComparison.Ordinal);
    }

    private static void EnsureApplication()
    {
        if (global::System.Windows.Application.Current is null)
        {
            var application = new POS.Wpf.App();
            application.InitializeComponent();
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
    }

    private static void RaiseTextInput(TextBox textBox, string text)
    {
        var composition = new TextComposition(InputManager.Current, textBox, text);
        var eventArgs = new TextCompositionEventArgs(
            InputManager.Current.PrimaryKeyboardDevice,
            composition)
        {
            RoutedEvent = TextCompositionManager.PreviewTextInputEvent,
            Source = textBox
        };
        textBox.RaiseEvent(eventArgs);
    }

    private sealed class EmptyScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new EmptyScope();
    }

    private sealed class EmptyScope : IServiceScope, IAsyncDisposable
    {
        public IServiceProvider ServiceProvider { get; } = new EmptyServiceProvider();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class CapturingBulkService : IBulkProductOperationService
    {
        public BulkProductOperationRequest? LastRequest { get; private set; }

        public Task<Result<BulkProductPreview>> PreviewAsync(
            BulkProductOperationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var preview = new BulkProductPreview(
                Guid.NewGuid(),
                request,
                [],
                0,
                1,
                true,
                []);
            return Task.FromResult(Result.Success(preview));
        }

        public Task<Result<BulkProductOperationResult>> CommitAsync(
            BulkProductPreview preview,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(
                new BulkProductOperationResult(
                    preview.PreviewId,
                    true,
                    preview.Request.Selection.Count,
                    preview.ChangeCount,
                    preview.NoOpCount,
                    [])));
    }
}
