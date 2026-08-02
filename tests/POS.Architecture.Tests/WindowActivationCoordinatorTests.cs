using Microsoft.Extensions.Hosting;
using POS.Wpf;
using POS.Wpf.Services;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class WindowActivationCoordinatorTests
{
    [Fact]
    public void Production_configuration_keeps_environment_above_json()
    {
        const string variableName =
            "Infrastructure__DatabasePath";

        const string jsonValue =
            "json-default.db";

        const string environmentValue =
            "environment-override.db";

        var previousValue =
            Environment.GetEnvironmentVariable(
                variableName,
                EnvironmentVariableTarget.Process);

        var testRoot =
            Path.Combine(
                Path.GetTempPath(),
                $"POS-R21-Configuration-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            testRoot);

        try
        {
            File.WriteAllText(
                Path.Combine(
                    testRoot,
                    "appsettings.json"),
                $$"""
                {
                  "Infrastructure": {
                    "DatabasePath": "{{jsonValue}}"
                  }
                }
                """);

            Environment.SetEnvironmentVariable(
                variableName,
                null,
                EnvironmentVariableTarget.Process);

            var jsonOnlyBuilder =
                CreateProductionConfigurationBuilder(
                    testRoot);

            Assert.Equal(
                jsonValue,
                jsonOnlyBuilder.Configuration[
                    "Infrastructure:DatabasePath"]);

            Environment.SetEnvironmentVariable(
                variableName,
                environmentValue,
                EnvironmentVariableTarget.Process);

            var overriddenBuilder =
                CreateProductionConfigurationBuilder(
                    testRoot);

            Assert.Equal(
                environmentValue,
                overriddenBuilder.Configuration[
                    "Infrastructure:DatabasePath"]);

            Environment.SetEnvironmentVariable(
                variableName,
                null,
                EnvironmentVariableTarget.Process);

            var fallbackBuilder =
                CreateProductionConfigurationBuilder(
                    testRoot);

            Assert.Equal(
                jsonValue,
                fallbackBuilder.Configuration[
                    "Infrastructure:DatabasePath"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variableName,
                previousValue,
                EnvironmentVariableTarget.Process);

            if (Directory.Exists(testRoot))
            {
                Directory.Delete(
                    testRoot,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void Request_before_target_ready_is_processed_after_target_registration()
    {
        var target =
            new FakeActivationTarget
            {
                IsVisible = false
            };

        var service =
            new RecordingActivationService();

        var coordinator =
            new WindowActivationCoordinator(
                service);

        coordinator.RequestActivation();
        coordinator.SetTarget(target);

        Assert.True(
            coordinator.HasPendingActivation);
        Assert.Equal(
            0,
            service.ActivationCount);

        target.IsVisible = true;
        coordinator.NotifyTargetReady();

        Assert.False(
            coordinator.HasPendingActivation);
        Assert.Equal(
            1,
            service.ActivationCount);
    }

    [Fact]
    public void Multiple_requests_do_not_create_an_unbounded_queue()
    {
        var target =
            new FakeActivationTarget
            {
                IsVisible = true
            };

        var service =
            new RecordingActivationService
            {
                ActivationResult = true
            };

        var coordinator =
            new WindowActivationCoordinator(
                service);

        coordinator.SetTarget(target);
        coordinator.RequestActivation();
        coordinator.RequestActivation();
        coordinator.RequestActivation();

        Assert.False(
            coordinator.HasPendingActivation);
        Assert.Equal(
            3,
            service.ActivationCount);
    }

    [Fact]
    public void Failed_activation_keeps_one_pending_request()
    {
        var target =
            new FakeActivationTarget
            {
                IsVisible = true
            };

        var service =
            new RecordingActivationService
            {
                ActivationResult = false
            };

        var coordinator =
            new WindowActivationCoordinator(
                service);

        coordinator.SetTarget(target);
        coordinator.RequestActivation();

        Assert.True(
            coordinator.HasPendingActivation);

        service.ActivationResult = true;
        coordinator.NotifyTargetReady();

        Assert.False(
            coordinator.HasPendingActivation);
    }

    [Fact]
    public void Minimized_target_is_restored_before_activation()
    {
        var target =
            new FakeActivationTarget
            {
                IsVisible = true,
                IsMinimized = true
            };

        var service =
            new WindowActivationService();

        Assert.True(
            service.TryActivate(target));

        Assert.Equal(
            ["restore", "activate"],
            target.Events);
    }

    [Fact]
    public void Activation_failure_requests_taskbar_attention_without_throwing()
    {
        var target =
            new FakeActivationTarget
            {
                IsVisible = true,
                ActivationResult = false
            };

        var service =
            new WindowActivationService();

        Assert.False(
            service.TryActivate(target));

        Assert.Equal(
            1,
            target.AttentionCount);
    }

    private sealed class RecordingActivationService :
        IWindowActivationService
    {
        public bool ActivationResult { get; set; } = true;

        public int ActivationCount { get; private set; }

        public bool TryActivate(
            IWindowActivationTarget target)
        {
            ActivationCount++;

            return ActivationResult;
        }
    }

    private static HostApplicationBuilder
        CreateProductionConfigurationBuilder(
            string testRoot)
    {
        var builder =
            Host.CreateApplicationBuilder(
                new HostApplicationBuilderSettings
                {
                    ContentRootPath =
                        testRoot,
                    EnvironmentName =
                        "ConfigurationPrecedenceTest"
                });

        App.ConfigureApplicationConfiguration(
            builder);

        return builder;
    }

    private sealed class FakeActivationTarget :
        IWindowActivationTarget
    {
        public bool IsVisible { get; set; }

        public bool IsMinimized { get; set; }

        public bool ActivationResult { get; set; } = true;

        public int AttentionCount { get; private set; }

        public List<string> Events { get; } = [];

        public void Restore()
        {
            Events.Add("restore");
            IsMinimized = false;
        }

        public bool Activate()
        {
            Events.Add("activate");

            return ActivationResult;
        }

        public void RequestAttention()
        {
            Events.Add("attention");
            AttentionCount++;
        }
    }
}
