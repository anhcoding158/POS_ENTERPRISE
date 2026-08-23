using POS.Application.Abstractions.Services;
using POS.Infrastructure.Support;
using POS.Wpf.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class AutomaticBackupUiTests
{
    [Theory]
    [InlineData(AutomaticBackupStatus.Running, "đang sao lưu")]
    [InlineData(AutomaticBackupStatus.Success, "thành công")]
    [InlineData(AutomaticBackupStatus.DeferredBusy, "tạm hoãn")]
    [InlineData(AutomaticBackupStatus.Failed, "chưa hoàn tất")]
    [InlineData(AutomaticBackupStatus.SuccessWithRetentionWarning, "cảnh báo retention")]
    [InlineData(AutomaticBackupStatus.StateCorrupt, "không đọc được lịch sử")]
    [InlineData(AutomaticBackupStatus.Cancelled, "đã dừng")]
    public void Typed_status_maps_to_non_modal_vietnamese_text(AutomaticBackupStatus status, string expected)
    {
        var source = new AutomaticBackupStatusSource();
        source.Publish(new(status));
        using var viewModel = new AutomaticBackupStatusViewModel(source);
        Assert.Contains(expected, viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Disposed_subscriber_does_not_crash_publisher()
    {
        var source = new AutomaticBackupStatusSource();
        var viewModel = new AutomaticBackupStatusViewModel(source);
        viewModel.Dispose();
        var exception = Record.Exception(() => source.Publish(new(AutomaticBackupStatus.Success)));
        Assert.Null(exception);
    }

    [Fact]
    public void Shell_surface_is_non_modal_and_contains_no_dialog_or_message_box_handler()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "ShellWindow.xaml"));
        Assert.Contains("AutomaticBackupStatusSurface", xaml, StringComparison.Ordinal);
        Assert.Contains("StatusText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"OnOpenAutomaticBackup", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Di_uses_singleton_coordinator_and_orchestrator_without_captive_scoped_service()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        typeof(POS.Wpf.App).GetMethod("ConfigureApplicationServices",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [services, configuration]);
        Assert.Equal(ServiceLifetime.Singleton,
            Assert.Single(services, item => item.ServiceType == typeof(IBackupCoordinator)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton,
            Assert.Single(services, item => item.ServiceType == typeof(IAutomaticBackupService)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped,
            Assert.Single(services, item => item.ServiceType == typeof(IManualBackupService)).Lifetime);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        { ValidateOnBuild = true, ValidateScopes = true });
        Assert.NotNull(provider.GetRequiredService<IAutomaticBackupService>());
    }
}
