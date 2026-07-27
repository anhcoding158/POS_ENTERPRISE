using System.Xml.Linq;
using POS.Application.DTOs.Checkout;
using POS.Domain.Enums;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class CheckoutRecoveryUiTests
{
    [Fact]
    public void Recovery_view_model_distinguishes_prepared_and_completed()
    {
        var prepared = new CheckoutRecoveryItemViewModel(CreateRecovery(
            CheckoutRequestStatus.Prepared,
            orderId: null,
            orderCode: null,
            canRetry: true,
            canAbandon: true));
        var completed = new CheckoutRecoveryItemViewModel(CreateRecovery(
            CheckoutRequestStatus.Completed,
            orderId: 7,
            orderCode: "ORD-7",
            canRetry: false,
            canAbandon: false));

        Assert.True(prepared.IsPrepared);
        Assert.False(prepared.IsCompleted);
        Assert.Contains("chưa hoàn tất", prepared.StateTitle, StringComparison.OrdinalIgnoreCase);
        Assert.True(completed.IsCompleted);
        Assert.False(completed.IsPrepared);
        Assert.Contains("đã hoàn tất", completed.StateTitle, StringComparison.OrdinalIgnoreCase);
        Assert.True(completed.CanOpenReceipt);
    }

    [Fact]
    public void Recovery_ui_read_only_bindings_are_one_way()
    {
        var bindings = LoadXaml().Descendants()
            .SelectMany(element => element.Attributes())
            .Select(attribute => attribute.Value)
            .Where(value => value.StartsWith("{Binding SelectedRecovery.", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(bindings);
        Assert.All(bindings, binding =>
            Assert.Contains("Mode=OneWay", binding, StringComparison.Ordinal));
    }

    [Fact]
    public void Editable_sales_bindings_remain_two_way()
    {
        var bindings = LoadXaml().Descendants()
            .SelectMany(element => element.Attributes())
            .Select(attribute => attribute.Value)
            .ToArray();

        Assert.Contains(
            bindings,
            binding => binding.Contains("CashReceivedText", StringComparison.Ordinal) &&
                       binding.Contains("Mode=TwoWay", StringComparison.Ordinal));
        Assert.Contains(
            bindings,
            binding => binding.Contains("OrderNotes", StringComparison.Ordinal) &&
                       binding.Contains("Mode=TwoWay", StringComparison.Ordinal));
    }

    [Fact]
    public void Recovery_ui_has_visible_actions_and_safe_layout()
    {
        var xaml = File.ReadAllText(SalesXamlPath);

        Assert.Contains("Command=\"{Binding RetryRecoveryCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding AbandonRecoveryCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding AcknowledgeRecoveryCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenRecoveryReceiptCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"560\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_confirmation_defaults_to_no()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "POS.Wpf",
            "Services",
            "ICheckoutRecoveryConfirmationService.cs"));

        Assert.Contains("MessageBoxResult.No", source, StringComparison.Ordinal);
        Assert.Contains(
            "Giao dịch này chưa tạo đơn hàng. Bỏ giao dịch dang dở?",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_ui_exposes_no_cost_secrets_raw_json_or_fingerprint()
    {
        var xaml = File.ReadAllText(SalesXamlPath);
        var recoveryBlock = xaml[
            xaml.IndexOf("<!-- Durable checkout recovery -->", StringComparison.Ordinal)..
            xaml.IndexOf("<!-- Checkout transaction lock -->", StringComparison.Ordinal)];

        Assert.DoesNotContain("Cost", recoveryBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", recoveryBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Json", recoveryBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fingerprint", recoveryBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("#[0-9A-Fa-f]{3,8}", recoveryBlock);
    }

    [Fact]
    public void Recovery_view_model_does_not_inject_repository_or_dbcontext()
    {
        var constructorTypes = typeof(CheckoutRecoveryItemViewModel)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.FullName ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            constructorTypes,
            type => type.Contains("Repository", StringComparison.Ordinal) ||
                    type.Contains("DbContext", StringComparison.Ordinal));
    }

    private static CheckoutRecoveryDto CreateRecovery(
        CheckoutRequestStatus status,
        int? orderId,
        string? orderCode,
        bool canRetry,
        bool canAbandon) =>
        new(
            Guid.NewGuid(),
            status,
            DateTimeOffset.UtcNow,
            orderId,
            orderCode,
            50_000,
            PaymentMethod.Cash,
            [new CheckoutRecoveryLineDto(1, "P1", "Cà phê", "ly", 1, 50_000, 50_000)],
            PreparedRequest: null,
            canRetry,
            canAbandon);

    private static XDocument LoadXaml() =>
        XDocument.Load(SalesXamlPath, LoadOptions.PreserveWhitespace);

    private static string SalesXamlPath =>
        Path.Combine(RepositoryRoot, "src", "POS.Wpf", "Views", "SalesWindow.xaml");

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null &&
                   !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ??
                   throw new InvalidOperationException("Không tìm thấy repository root.");
        }
    }
}
