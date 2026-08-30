using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Orders;
using POS.Domain.Enums;
using POS.Wpf.ViewModels;
using POS.Wpf.Services;
using POS.Wpf.Views;
using System.Globalization;
using System.Xml.Linq;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class OrderReturnUiTests
{
    [Fact]
    public async Task Double_submit_must_invoke_process_service_once()
    {
        var service = new StubService { HoldProcess = true };
        using var viewModel = CreateLoadedViewModel(service, true);
        await viewModel.LoadAsync();
        viewModel.Reason = "Hàng lỗi";
        viewModel.Lines[0].ReturnQuantity = 1;

        viewModel.SubmitCommand.Execute(null);
        await service.ProcessStarted.Task;
        viewModel.SubmitCommand.Execute(null);

        Assert.Equal(1, service.ProcessCalls);
        Assert.True(viewModel.IsSubmitting);
        service.CompleteProcess();
        await service.ProcessCompleted.Task;
        Assert.Equal(1, service.ProcessCalls);
    }

    [Fact]
    public async Task Cancelled_confirmation_must_not_call_process_or_change_input()
    {
        var service = new StubService();
        using var viewModel = CreateLoadedViewModel(service, false);
        await viewModel.LoadAsync();
        viewModel.Reason = "  Hàng lỗi  ";
        viewModel.Lines[0].ReturnQuantity = 1;
        var requestId = viewModel.ClientRequestId;

        viewModel.SubmitCommand.Execute(null);

        Assert.Equal(0, service.ProcessCalls);
        Assert.Equal(requestId, viewModel.ClientRequestId);
        Assert.Equal("  Hàng lỗi  ", viewModel.Reason);
        Assert.False(viewModel.IsSuccessful);
    }

    [Fact]
    public async Task Retry_after_failure_must_reuse_same_client_request_id()
    {
        var service = new StubService();
        using var viewModel = CreateLoadedViewModel(service, true);
        await viewModel.LoadAsync();
        viewModel.Reason = "Hàng lỗi";
        viewModel.Lines[0].ReturnQuantity = 1;
        var requestId = viewModel.ClientRequestId;

        service.ProcessResult = Result.Failure<OrderReturnResultDto>(
            new AppError("TEST", "Thử lại"));
        await ExecuteAndWaitAsync(viewModel, service);
        service.ProcessResult = StubService.Success(requestId);
        await ExecuteAndWaitAsync(viewModel, service);

        Assert.Equal(2, service.ProcessCalls);
        Assert.All(service.Requests, request => Assert.Equal(requestId, request.ClientRequestId));
        Assert.True(viewModel.IsSuccessful);
    }

    [Fact]
    public async Task Dispose_during_load_must_cancel_pending_request()
    {
        var service = new StubService { HoldLoad = true };
        var viewModel = CreateLoadedViewModel(service, true);

        var load = viewModel.LoadAsync();
        await service.LoadStarted.Task;
        viewModel.Dispose();
        await load;

        Assert.True(service.LoadToken.IsCancellationRequested);
        Assert.Null(viewModel.Message);
    }

    [Fact]
    public void OrderReturnUi_remaining_and_preview_must_be_deterministic()
    {
        var line = new OrderReturnLineViewModel(
            new ReturnableOrderLineDto(
                1, 2, "P1", "Snapshot", "Cái",
                3, 1, 2, 7, true, true))
        {
            ReturnQuantity = 1
        };

        Assert.Equal(2, line.RemainingQuantity);
        Assert.Equal(3, line.PreviewRefundAmount);
        Assert.True(line.IsArchived);
    }

    [Fact]
    public void Quantity_editing_must_start_empty_and_keep_invalid_input_visible()
    {
        var line = new OrderReturnLineViewModel(
            new ReturnableOrderLineDto(
                1, 2, "P1", "Snapshot", "Cái",
                3, 1, 2, 7, true, true));

        Assert.Equal(string.Empty, line.ReturnQuantityText);
        Assert.Equal(string.Empty, line.RestockQuantityText);

        line.ReturnQuantityText = "1";
        Assert.Equal(1, line.ReturnQuantity);
        Assert.True(line.IsValid);

        line.ReturnQuantityText = string.Empty;
        Assert.Equal(0, line.ReturnQuantity);
        Assert.Equal(0, line.PreviewRefundAmount);
        Assert.True(line.IsValid);

        line.ReturnQuantityText = "3";
        Assert.Equal(3, line.ReturnQuantity);
        Assert.False(line.IsValid);
        Assert.Equal(0, line.PreviewRefundAmount);

        line.ReturnQuantityText = "not-a-number";
        Assert.Equal(-1, line.ReturnQuantity);
        Assert.False(line.IsValid);
    }

    [Fact]
    public void Restock_editing_must_not_silently_clamp_to_return_quantity()
    {
        var line = new OrderReturnLineViewModel(
            new ReturnableOrderLineDto(
                1, 2, "P1", "Snapshot", "Cái",
                3, 1, 2, 7, true, true))
        {
            ReturnQuantity = 1
        };

        line.RestockQuantityText = "2";

        Assert.Equal(2, line.RestockQuantity);
        Assert.False(line.IsValid);
    }

    [Fact]
    public void OrderReturnUi_new_dialog_must_create_new_request_id()
    {
        var service = new StubService();
        var confirmation = new ConfirmationService(true);
        var first = new OrderReturnViewModel(service, confirmation, 1);
        var second = new OrderReturnViewModel(service, confirmation, 1);

        Assert.NotEqual(Guid.Empty, first.ClientRequestId);
        Assert.NotEqual(first.ClientRequestId, second.ClientRequestId);
    }

    [Fact]
    public void OrderReturnUi_retry_must_keep_request_id_and_show_vietqr_warning()
    {
        var viewModel = new OrderReturnViewModel(
            new StubService(), new ConfirmationService(true), 1);
        var requestId = viewModel.ClientRequestId;
        viewModel.RefundMethod = PaymentMethod.VietQr;

        Assert.Equal(requestId, viewModel.ClientRequestId);
        Assert.Contains("không tự chuyển tiền", viewModel.VietQrWarning);
    }

    [Fact]
    public void OrderReturnUi_view_model_must_not_inject_repository_or_dbcontext()
    {
        var constructors = typeof(OrderReturnViewModel).GetConstructors();
        Assert.All(
            constructors.SelectMany(constructor => constructor.GetParameters()),
            parameter =>
            {
                Assert.DoesNotContain("Repository", parameter.ParameterType.Name);
                Assert.DoesNotContain("DbContext", parameter.ParameterType.Name);
            });
    }

    [Fact]
    public void Return_quantity_editor_must_be_visibly_editable()
    {
        var editor = FindNamedElement("ReturnQuantityEditor");

        Assert.Equal("TextBox", editor.Name.LocalName);
        Assert.Equal("{StaticResource QuantityEditorStyle}", Attribute(editor, "Style"));
    }

    [Fact]
    public void Restock_quantity_editor_must_be_visibly_editable()
    {
        var editor = FindNamedElement("RestockQuantityEditor");

        Assert.Equal("TextBox", editor.Name.LocalName);
        Assert.Equal("{StaticResource QuantityEditorStyle}", Attribute(editor, "Style"));
    }

    [Fact]
    public void Quantity_editors_must_use_two_way_property_changed_binding()
    {
        AssertEditableBinding(FindNamedElement("ReturnQuantityEditor"), "ReturnQuantityText");
        AssertEditableBinding(FindNamedElement("RestockQuantityEditor"), "RestockQuantityText");
    }

    [Fact]
    public void Read_only_columns_must_not_be_editable()
    {
        var document = LoadReturnXaml();
        var expectedBindings = new[]
        {
            "ProductCode", "ProductName", "SoldQuantity", "ReturnedQuantity",
            "RemainingQuantity", "PreviewRefundAmount"
        };

        foreach (var path in expectedBindings)
        {
            var column = document.Descendants()
                .Single(element => element.Name.LocalName == "DataGridTextColumn" &&
                    Attribute(element, "Binding").Contains(path, StringComparison.Ordinal));
            Assert.Equal("True", Attribute(column, "IsReadOnly"));
        }
    }

    [Theory]
    [InlineData("Lý do trả hàng *")]
    [InlineData("Phương thức hoàn tiền")]
    [InlineData("Mã tham chiếu")]
    public void Form_fields_must_have_visible_labels(string label) =>
        Assert.Contains(
            LoadReturnXaml().Descendants()
                .Where(element => element.Name.LocalName == "TextBlock"),
            element => Attribute(element, "Text") == label);

    [Fact]
    public void Reason_field_must_have_visible_label() =>
        Form_fields_must_have_visible_labels("Lý do trả hàng *");

    [Fact]
    public void Refund_method_field_must_have_visible_label() =>
        Form_fields_must_have_visible_labels("Phương thức hoàn tiền");

    [Fact]
    public void Refund_reference_field_must_have_visible_label() =>
        Form_fields_must_have_visible_labels("Mã tham chiếu");

    [Fact]
    public void Reason_validation_must_render_as_validation_text_not_button()
    {
        var matches = LoadReturnXaml().Descendants()
            .Where(element => Attribute(element, "Text") == "Lý do bắt buộc")
            .ToArray();

        Assert.Single(matches);
        Assert.Equal("TextBlock", matches[0].Name.LocalName);
        Assert.Equal("{StaticResource DangerBrush}", Attribute(matches[0], "Foreground"));
    }

    [Fact]
    public void Submit_button_text_must_describe_return_action() =>
        Assert.Equal("Xác nhận trả hàng", Attribute(FindNamedElement("SubmitButton"), "Content"));

    [Fact]
    public void Submit_button_must_not_contain_irreversible_warning_as_label() =>
        Assert.DoesNotContain(
            "không thể sửa",
            Attribute(FindNamedElement("SubmitButton"), "Content"),
            StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Irreversible_warning_must_remain_in_confirmation()
    {
        var source = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src", "POS.Wpf", "ViewModels",
                "OrderReturnViewModel.cs"));

        Assert.Contains(
            "Chứng từ trả hàng không thể sửa hoặc xóa sau khi hoàn tất.",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Return_window_must_show_short_usage_instruction() =>
        Assert.Contains(
            LoadReturnXaml().Descendants()
                .Where(element => element.Name.LocalName == "TextBlock"),
            element => Attribute(element, "Text") ==
                "Nhập số lượng khách trả và số lượng hàng đủ điều kiện nhập lại kho.");

    [Fact]
    public void Return_window_must_not_allow_user_added_rows()
    {
        var grid = FindNamedElement("ReturnLinesGrid");

        Assert.Equal("False", Attribute(grid, "CanUserAddRows"));
    }

    [Fact]
    public void Return_window_must_construct_on_STA_without_binding_exception()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (global::System.Windows.Application.Current is null)
                {
                    var application = new POS.Wpf.App();
                    application.InitializeComponent();
                }
                using var viewModel = new OrderReturnViewModel(
                    new StubService(), new ConfirmationService(false), 1);
                var window = new OrderReturnWindow(viewModel);
                window.Measure(new global::System.Windows.Size(1000, 620));
                window.Arrange(new global::System.Windows.Rect(0, 0, 1000, 620));
                window.UpdateLayout();
                window.Close();
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

    [Fact]
    public void Editable_fields_must_remain_two_way()
    {
        AssertEditableBinding(FindNamedElement("ReturnQuantityEditor"), "ReturnQuantityText");
        AssertEditableBinding(FindNamedElement("RestockQuantityEditor"), "RestockQuantityText");
        AssertEditableBinding(FindNamedElement("ReasonTextBox"), "Reason");
        AssertEditableBinding(FindNamedElement("RefundMethodComboBox"), "RefundMethod", "SelectedItem");
        AssertEditableBinding(FindNamedElement("RefundReferenceTextBox"), "RefundReference");
    }

    [Fact]
    public void Read_only_fields_must_remain_one_way()
    {
        var xaml = File.ReadAllText(ReturnXamlPath);
        foreach (var path in new[]
                 {
                     "OrderCode", "Lines", "ProductCode", "ProductName",
                     "SoldQuantity", "ReturnedQuantity", "RemainingQuantity",
                     "PreviewRefundAmount", "VietQrWarning", "Message",
                     "TotalRefundAmount"
                 })
        {
            var binding = BindingContaining(xaml, path);
            Assert.Contains("Mode=OneWay", binding, StringComparison.Ordinal);
            Assert.DoesNotContain("Mode=TwoWay", binding, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void No_new_hex_colors() =>
        Assert.DoesNotMatch("#[0-9A-Fa-f]{3,8}", File.ReadAllText(ReturnXamlPath));

    [Fact]
    public void No_cost_price()
    {
        var xaml = File.ReadAllText(ReturnXamlPath);
        Assert.DoesNotContain("CostPrice", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Giá vốn", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Return_window_fits_1366x768()
    {
        var window = LoadReturnXaml().Root!;

        Assert.True(double.Parse(Attribute(window, "Width"), CultureInfo.InvariantCulture) <= 1366);
        Assert.True(double.Parse(Attribute(window, "Height"), CultureInfo.InvariantCulture) <= 768);
        Assert.True(double.Parse(Attribute(window, "MaxWidth"), CultureInfo.InvariantCulture) <= 1366);
        Assert.True(double.Parse(Attribute(window, "MaxHeight"), CultureInfo.InvariantCulture) <= 768);
    }

    [Fact]
    public void Confirmation_defaults_to_no()
    {
        var source = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src", "POS.Wpf", "Services",
                "OrderReturnWindowService.cs"));

        Assert.Contains("MessageBoxResult.No", source, StringComparison.Ordinal);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null &&
                   !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
                directory = directory.Parent;
            return directory?.FullName ??
                throw new InvalidOperationException("Không tìm thấy repository root.");
        }
    }

    private static string ReturnXamlPath =>
        Path.Combine(RepositoryRoot, "src", "POS.Wpf", "Views", "OrderReturnWindow.xaml");

    private static XDocument LoadReturnXaml() =>
        XDocument.Load(ReturnXamlPath, LoadOptions.PreserveWhitespace);

    private static XElement FindNamedElement(string name) =>
        LoadReturnXaml().Descendants()
            .Single(element => Attribute(element, "Name") == name);

    private static string Attribute(XElement element, string localName) =>
        element.Attributes()
            .SingleOrDefault(attribute => attribute.Name.LocalName == localName)
            ?.Value ?? string.Empty;

    private static void AssertEditableBinding(
        XElement element,
        string path,
        string attributeName = "Text")
    {
        var binding = Attribute(element, attributeName);
        Assert.Contains(path, binding, StringComparison.Ordinal);
        Assert.Contains("Mode=TwoWay", binding, StringComparison.Ordinal);
        Assert.Contains("UpdateSourceTrigger=PropertyChanged", binding, StringComparison.Ordinal);
    }

    private static string BindingContaining(string xaml, string path)
    {
        var pathIndex = xaml.IndexOf(path, StringComparison.Ordinal);
        Assert.True(pathIndex >= 0, $"Không tìm thấy binding {path}.");
        var start = xaml.LastIndexOf("{Binding", pathIndex, StringComparison.Ordinal);
        var end = xaml.IndexOf('}', pathIndex);
        Assert.True(start >= 0 && end > start, $"Binding {path} không hợp lệ.");
        return xaml[start..(end + 1)];
    }

    private static OrderReturnViewModel CreateLoadedViewModel(
        StubService service,
        bool confirmation) =>
        new(service, new ConfirmationService(confirmation), 1);

    private static async Task ExecuteAndWaitAsync(
        OrderReturnViewModel viewModel,
        StubService service)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(OrderReturnViewModel.IsSubmitting) &&
                !viewModel.IsSubmitting)
                completion.TrySetResult();
        }
        viewModel.PropertyChanged += OnChanged;
        viewModel.SubmitCommand.Execute(null);
        if (!viewModel.IsSubmitting)
            completion.TrySetResult();
        await completion.Task;
        viewModel.PropertyChanged -= OnChanged;
        await service.ProcessCompleted.Task;
        service.ResetProcessCompletion();
    }

    private sealed class StubService : IOrderReturnService
    {
        private TaskCompletionSource<Result<OrderReturnResultDto>> _process =
            NewProcessSource();
        public bool HoldProcess { get; init; }
        public bool HoldLoad { get; init; }
        public int ProcessCalls { get; private set; }
        public List<OrderReturnRequest> Requests { get; } = [];
        public CancellationToken LoadToken { get; private set; }
        public TaskCompletionSource ProcessStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ProcessCompleted { get; private set; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Result<OrderReturnResultDto> ProcessResult { get; set; } =
            Result.Failure<OrderReturnResultDto>(new AppError("TEST", "test"));

        public Task<Result<OrderReturnResultDto>> ProcessAsync(
            OrderReturnRequest request, CancellationToken cancellationToken = default)
        {
            ProcessCalls++;
            Requests.Add(request);
            ProcessStarted.TrySetResult();
            if (!HoldProcess)
            {
                ProcessCompleted.TrySetResult();
                return Task.FromResult(ProcessResult);
            }
            return _process.Task;
        }

        public Task<Result<IReadOnlyList<OrderReturnSummaryDto>>> GetReturnsByOrderIdAsync(
            int orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<OrderReturnSummaryDto>>([]));

        public async Task<Result<ReturnableOrderDto>> GetReturnableOrderAsync(
            int orderId, CancellationToken cancellationToken = default)
        {
            LoadToken = cancellationToken;
            LoadStarted.TrySetResult();
            if (HoldLoad)
            {
                var cancelled = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = cancellationToken.Register(
                    () => cancelled.TrySetCanceled(cancellationToken));
                await cancelled.Task;
            }
            return Result.Success(new ReturnableOrderDto(
                orderId, "ORD-1", DateTimeOffset.UnixEpoch, "Cashier",
                PaymentMethod.Cash,
                [new(1, 2, "P1", "Snapshot", "Cái", 2, 0, 2, 100, true, false)],
                []));
        }

        public void CompleteProcess()
        {
            _process.TrySetResult(ProcessResult);
            ProcessCompleted.TrySetResult();
        }

        public void ResetProcessCompletion()
        {
            ProcessCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public static Result<OrderReturnResultDto> Success(Guid requestId) =>
            Result.Success(new OrderReturnResultDto(
                10, requestId, 1, DateTimeOffset.UnixEpoch, 50, false,
                [new(1, 2, "P1", "Snapshot", 1, 0, 50)]));

        private static TaskCompletionSource<Result<OrderReturnResultDto>>
            NewProcessSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ConfirmationService(bool result) :
        IOrderReturnConfirmationService
    {
        public bool Confirm(string message) => result;
    }
}
