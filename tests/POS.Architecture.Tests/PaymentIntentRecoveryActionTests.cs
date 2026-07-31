using POS.Application.DTOs.Payments;
using POS.Domain.Enums;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentRecoveryActionTests
{
    [Fact]
    public void Created_recovery_displays_persisted_data()
    {
        var item = Item(PaymentIntentStatus.Created);

        Assert.Equal("ĐÃ TẠO MÃ · CHƯA HIỂN THỊ QR", item.StateTitle);
        Assert.Equal("PAY persisted", item.TransferContent);
        Assert.Equal("payload-persisted", item.PayloadText);
        Assert.Contains("35.000", item.AmountText, StringComparison.Ordinal);
        Assert.NotEmpty(item.CreatedAtText);
        Assert.NotEmpty(item.ExpiresAtText);
    }

    [Fact]
    public void Presented_recovery_displays_persisted_QR()
    {
        var item = Item(PaymentIntentStatus.Presented);

        Assert.Equal("ĐÃ HIỂN THỊ QR · CHƯA XÁC NHẬN TIỀN", item.StateTitle);
        Assert.Equal("payload-persisted", item.PayloadText);
        Assert.Equal(
            "Hệ thống không tự kiểm tra giao dịch ngân hàng. Chỉ xác nhận sau khi nhân viên đã kiểm tra tiền thực tế.",
            item.Warning);
    }

    [Fact]
    public void Legacy_confirmed_snapshot_is_manual_review()
    {
        var item = Item(PaymentIntentStatus.Confirmed, stale: true);

        Assert.True(item.IsManualReview);
        Assert.False(item.CanRetryCheckout);
        Assert.False(item.CanCancel);
        Assert.Contains("Không yêu cầu khách chuyển thêm", item.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_review_has_no_retry_when_unsafe() =>
        Assert.False(Item(PaymentIntentStatus.Confirmed, stale: true).CanRetryCheckout);

    [Fact]
    public void Manual_review_has_no_cancel_or_abandon() =>
        Assert.False(Item(PaymentIntentStatus.Confirmed, stale: true).CanCancel);

    [Fact]
    public void Recovery_actions_use_persisted_payload_and_release_busy_state()
    {
        var source = Read("src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");
        var method = Slice(source, "private async Task ShowPaymentIntentQrAsync()",
            "private async Task ConfirmPaymentIntentRecoveryAsync()");

        Assert.Contains("GetByIdAsync(pending.Id)", method, StringComparison.Ordinal);
        Assert.Contains("latest.Value.PayloadText", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Build(", method, StringComparison.Ordinal);
        Assert.Contains("finally", method, StringComparison.Ordinal);
        Assert.Contains("IsProcessingRecovery = false", method, StringComparison.Ordinal);
    }

    [Fact]
    public void Created_recovery_render_success_marks_presented_once()
    {
        var source = Read("src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");
        var method = Slice(source, "private async Task ShowPaymentIntentQrAsync()",
            "private async Task ConfirmPaymentIntentRecoveryAsync()");
        var dialog = method.IndexOf("ShowPresentationAsync(", StringComparison.Ordinal);
        var mark = method.IndexOf("MarkPresentedAsync(pending.Id)", StringComparison.Ordinal);

        Assert.True(dialog >= 0);
        Assert.True(dialog < mark);
        Assert.Equal(mark, method.LastIndexOf("MarkPresentedAsync(pending.Id)", StringComparison.Ordinal));
    }

    [Fact]
    public void Confirmed_window_close_defaults_to_stay()
    {
        var source = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml.cs");
        var method = Slice(source, "private void OnWindowClosing(", "private void OnWindowClosed(");

        Assert.Contains("hasConfirmedRecovery", method, StringComparison.Ordinal);
        Assert.Contains("MessageBoxResult.No", method, StringComparison.Ordinal);
        Assert.Contains("Không yêu cầu khách chuyển thêm", method, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true", method, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmed_retry_in_progress_blocks_close_temporarily()
    {
        var source = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml.cs");
        var method = Slice(source, "private void OnWindowClosing(", "private void OnWindowClosed(");
        Assert.Contains("IsProcessingRecovery", method, StringComparison.Ordinal);
    }

    [Fact]
    public void Retry_failure_displays_error_in_recovery_panel()
    {
        var source = Read("src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");
        var method = Slice(source, "private async Task RetryPaymentIntentRecoveryAsync(",
            "private async Task ShowPaymentIntentQrAsync()");
        var view = Read("src", "POS.Wpf", "Views", "SalesWindow.xaml");

        Assert.Contains("PaymentIntentRecoveryError = result.AppError.Message", method,
            StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding PaymentIntentRecoveryError}\"", view,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Retry_failure_reenables_action_and_exception_does_not_remain_busy()
    {
        var source = Read("src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");
        var method = Slice(source, "private async Task RetryPaymentIntentRecoveryAsync(",
            "private async Task ShowPaymentIntentQrAsync()");

        Assert.Contains("catch (Exception exception)", method, StringComparison.Ordinal);
        Assert.Contains("finally", method, StringComparison.Ordinal);
        Assert.Contains("IsProcessingRecovery = false", method, StringComparison.Ordinal);
    }

    [Fact]
    public void Retry_uses_only_payment_intent_id_and_does_not_regenerate_or_reconfirm()
    {
        var source = Read("src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");
        var method = Slice(source, "private async Task RetryPaymentIntentRecoveryAsync(",
            "private async Task ShowPaymentIntentQrAsync()");

        Assert.Contains("RetryConfirmedPaymentIntentAsync(paymentIntentId)", method,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePaymentIntentRequest", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Build(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmReceivedAsync", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.NewGuid", method, StringComparison.Ordinal);
    }

    [Fact]
    public void Retry_success_refreshes_pending_recovery_and_clears_inline_error()
    {
        var source = Read("src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");
        var method = Slice(source, "private async Task RetryPaymentIntentRecoveryAsync(",
            "private async Task ShowPaymentIntentQrAsync()");

        Assert.Contains("ReloadRecoveryStateAsync(openHighestPriority: false)", method,
            StringComparison.Ordinal);
        Assert.Contains("PaymentIntentRecoveryError = null", method,
            StringComparison.Ordinal);
    }

    private static PaymentIntentRecoveryItemViewModel Item(
        PaymentIntentStatus status,
        bool stale = false) =>
        new(new PaymentIntentPendingDto(
            7, "VQ-0007", status, 35_000, "VND", "PAY persisted",
            "payload-persisted", "970415", "123", "POS TEST",
            new DateTimeOffset(2026, 7, 30, 1, 2, 3, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 30, 1, 2, 3, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 30, 1, 17, 3, TimeSpan.Zero),
            null,
            status == PaymentIntentStatus.Created && !stale,
            status is PaymentIntentStatus.Created or PaymentIntentStatus.Presented && !stale,
            status == PaymentIntentStatus.Presented && !stale,
            status is PaymentIntentStatus.Created or PaymentIntentStatus.Presented,
            status == PaymentIntentStatus.Confirmed && !stale,
            stale,
            null));

    private static string Slice(string source, string startText, string endText)
    {
        var start = source.IndexOf(startText, StringComparison.Ordinal);
        var end = source.IndexOf(endText, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    private static string Read(params string[] parts)
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. parts]));
    }
}
