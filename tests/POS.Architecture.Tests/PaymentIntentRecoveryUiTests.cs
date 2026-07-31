using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentRecoveryUiTests
{
    private static readonly string ViewModel = Read(
        "src", "POS.Wpf", "ViewModels", "SalesViewModel.cs");
    private static readonly string View = Read(
        "src", "POS.Wpf", "Views", "SalesWindow.xaml");
    private static readonly string Service = Read(
        "src", "POS.Application", "Services", "PaymentIntentService.cs");

    [Theory]
    [InlineData("Created")]
    [InlineData("Presented")]
    [InlineData("Confirmed")]
    public void Pending_status_is_offered_after_restart(string status)
    {
        Assert.Contains("RecoverPendingAsync", ViewModel, StringComparison.Ordinal);
        Assert.Contains(status, Service, StringComparison.Ordinal);
        Assert.Contains("PendingPaymentIntents", View, StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_does_not_auto_create_order()
    {
        var method = RecoveryLoadMethod();
        Assert.DoesNotContain("CheckoutAsync", method, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "service.CreateAsync",
            method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmed_intent_has_no_silent_abandon_action()
    {
        Assert.Contains(
            "không tạo mã mới",
            Service,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AbandonPaymentIntent", ViewModel, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("Expired")]
    public void Terminal_intent_is_not_pending(string status)
    {
        var repository = Read(
            "src", "POS.Infrastructure", "Persistence", "Repositories",
            "PaymentIntentRepository.cs");
        Assert.Contains("Created", repository, StringComparison.Ordinal);
        Assert.Contains("Presented", repository, StringComparison.Ordinal);
        Assert.Contains("Confirmed", repository, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"PaymentIntentStatus.{status}",
            repository,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_query_runs_only_after_schema_upgrade()
    {
        var app = Read("src", "POS.Wpf", "App.xaml.cs");
        Assert.True(
            app.IndexOf("InitializeDatabaseAsync", StringComparison.Ordinal) <
            app.IndexOf("RunSessionLoopAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void Recovery_query_failure_does_not_lock_SalesWindow()
    {
        var method = RecoveryLoadMethod();
        Assert.Contains("catch (Exception exception)", method, StringComparison.Ordinal);
        Assert.Contains("finally", method, StringComparison.Ordinal);
        Assert.Contains("source.Dispose()", method, StringComparison.Ordinal);
    }

    private static string RecoveryLoadMethod()
    {
        var start = ViewModel.IndexOf(
            "private async Task LoadPaymentIntentRecoveryAsync()",
            StringComparison.Ordinal);
        var end = ViewModel.IndexOf(
            "private async Task LoadCheckoutRecoveryAsync()",
            start,
            StringComparison.Ordinal);
        return ViewModel[start..end];
    }

    private static string Read(params string[] parts)
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. parts]));
    }
}
