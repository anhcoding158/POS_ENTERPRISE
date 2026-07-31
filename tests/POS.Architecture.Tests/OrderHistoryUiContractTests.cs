using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Orders;
using POS.Application.DTOs.Printing;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Wpf.Commands;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class OrderHistoryUiContractTests
{
    [Fact]
    public void Shell_history_button_must_bind_open_order_history_command()
    {
        var button = FindShellNavigationButton("Lịch sử đơn hàng");

        Assert.Equal(
            "{Binding OpenOrderHistoryCommand}",
            (string?)button.Attribute("Command"));
    }

    [Fact]
    public void Shell_history_button_must_not_be_hardcoded_disabled()
    {
        var button = FindShellNavigationButton("Lịch sử đơn hàng");

        Assert.NotEqual(
            "False",
            (string?)button.Attribute("IsEnabled"));
    }

    [Fact]
    public void Administrator_permission_policy_must_include_view_reports()
    {
        Assert.True(
            RolePermissionPolicy.HasPermission(
                POS.Domain.Enums.Role.Administrator,
                SystemCapability.ViewReports));
    }

    [Fact]
    public void Shell_history_command_must_be_executable_for_view_reports_user()
    {
        var context = CreateShellContext(hasViewReports: true);

        Assert.True(
            context.ViewModel.OpenOrderHistoryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Shell_history_command_must_not_execute_without_view_reports()
    {
        var context = CreateShellContext(hasViewReports: false);

        Assert.False(
            context.ViewModel.OpenOrderHistoryCommand.CanExecute(null));

        context.ViewModel.OpenOrderHistoryCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(0, context.WindowService.ShowCalls);
    }

    [Fact]
    public async Task Open_history_command_must_call_window_service_once()
    {
        var context = CreateShellContext(hasViewReports: true);

        await ExecuteAsync(context.ViewModel.OpenOrderHistoryCommand);

        Assert.Equal(1, context.WindowService.ShowCalls);
    }

    [Fact]
    public void Window_service_must_keep_scope_alive_until_window_closes()
    {
        var source = ReadRepositoryFile(
            "src", "POS.Wpf", "Services", "OrderHistoryWindowService.cs");

        var createScope = source.IndexOf(
            "using var scope = _scopeFactory.CreateScope()",
            StringComparison.Ordinal);
        var showDialog = source.IndexOf(
            "window.ShowDialog()",
            StringComparison.Ordinal);

        Assert.True(createScope >= 0);
        Assert.True(showDialog > createScope);
    }

    [Fact]
    public void Window_service_must_dispose_scope_after_window_closes()
    {
        var source = ReadRepositoryFile(
            "src", "POS.Wpf", "Services", "OrderHistoryWindowService.cs");

        Assert.Contains(
            "using var scope = _scopeFactory.CreateScope()",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.ShowDialog()",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Second_open_must_activate_existing_window_instead_of_creating_duplicate()
    {
        var source = ReadRepositoryFile(
            "src", "POS.Wpf", "Services", "OrderHistoryWindowService.cs");

        Assert.Contains("_openWindow.Activate()", source, StringComparison.Ordinal);
        Assert.Contains("_openWindow.Focus()", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Order_history_window_must_open_with_empty_result()
    {
        var service = new FakeHistoryService();
        using var viewModel = CreateViewModel(service);

        await ExecuteAsync(viewModel.LoadCommand);

        Assert.Empty(viewModel.Orders);
        Assert.Equal(1, service.SearchCalls);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Initial_load_failure_must_not_close_window()
    {
        var service = new FakeHistoryService
        {
            SearchResult = Result.Failure<PagedResult<OrderHistoryListItemDto>>(
                new AppError("TEST.LOAD", "Không thể tải dữ liệu."))
        };
        using var viewModel = CreateViewModel(service);

        await ExecuteAsync(viewModel.LoadCommand);

        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Empty(viewModel.Orders);
    }

    [Fact]
    public void Order_history_xaml_must_only_reference_existing_static_resources()
    {
        var xaml = ReadRepositoryFile(
            "src", "POS.Wpf", "Views", "OrderHistoryWindow.xaml");
        var matches = System.Text.RegularExpressions.Regex.Matches(
            xaml,
            @"\{StaticResource\s+([^}\s]+)\}");
        var resourceFiles = new[]
        {
            ReadRepositoryFile("src", "POS.Wpf", "App.xaml"),
            ReadRepositoryFile("src", "POS.Wpf", "Themes", "Colors.xaml"),
            ReadRepositoryFile("src", "POS.Wpf", "Themes", "Controls.xaml"),
            ReadRepositoryFile("src", "POS.Wpf", "Themes", "Typography.xaml")
        };
        var keys = resourceFiles
            .SelectMany(text => System.Text.RegularExpressions.Regex.Matches(
                text,
                "x:Key=\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(
            matches.Select(match => match.Groups[1].Value),
            key => Assert.Contains(key, keys));
    }

    [Fact]
    public void Order_history_window_must_not_be_topmost_permanently()
    {
        var document = System.Xml.Linq.XDocument.Parse(
            ReadRepositoryFile(
                "src", "POS.Wpf", "Views", "OrderHistoryWindow.xaml"));

        Assert.NotEqual(
            "True",
            (string?)document.Root?.Attribute("Topmost"));
    }

    [Fact]
    public void Order_history_open_path_must_not_depend_on_repository_or_DbContext()
    {
        var sources = string.Concat(
            ReadRepositoryFile("src", "POS.Wpf", "ViewModels", "ShellViewModel.cs"),
            ReadRepositoryFile("src", "POS.Wpf", "Views", "ShellWindow.xaml.cs"));

        Assert.DoesNotContain("IOrderRepository", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("PosDbContext", sources, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_history_filter_must_show_today_completed_orders()
    {
        using var viewModel = CreateViewModel();

        Assert.Equal(DateTime.Today, viewModel.FromDate);
        Assert.Equal(DateTime.Today, viewModel.ToDate);
        Assert.Equal(OrderStatus.Completed, viewModel.SelectedStatusFilter.Value);
        Assert.Null(viewModel.SelectedPaymentMethodFilter.Value);
        Assert.Equal(25, viewModel.PageSize);
    }

    [Fact]
    public void Local_date_range_must_convert_to_utc_without_hardcoded_offset()
    {
        var date = new DateTime(2026, 7, 27);

        var range = OrderHistoryViewModel.ConvertLocalDateRangeToUtc(date, date);
        var expectedStart = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(date, DateTimeKind.Unspecified),
            TimeZoneInfo.Local);
        var expectedEnd = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(
                date.AddDays(1).AddMilliseconds(-1),
                DateTimeKind.Unspecified),
            TimeZoneInfo.Local);

        Assert.Equal(expectedStart, range.FromUtc);
        Assert.Equal(expectedEnd, range.ToUtc);
    }

    [Fact]
    public void Local_date_range_must_use_inclusive_millisecond_boundary()
    {
        var date = new DateTime(2026, 7, 27);

        var range = OrderHistoryViewModel.ConvertLocalDateRangeToUtc(date, date);

        Assert.Equal(
            TimeSpan.FromDays(1) - TimeSpan.FromMilliseconds(1),
            range.ToUtc - range.FromUtc);
    }

    [Fact]
    public void Status_filters_must_use_typed_values()
    {
        using var viewModel = CreateViewModel();

        Assert.Contains(viewModel.StatusFilters, option => option.Value is null);
        Assert.Contains(
            viewModel.StatusFilters,
            option => option.Value == OrderStatus.Completed);
    }

    [Fact]
    public void Payment_filters_must_use_typed_values()
    {
        using var viewModel = CreateViewModel();

        Assert.Contains(
            viewModel.PaymentMethodFilters,
            option => option.Value is null);
        Assert.Contains(
            viewModel.PaymentMethodFilters,
            option => option.Value == PaymentMethod.Cash);
        Assert.Contains(
            viewModel.PaymentMethodFilters,
            option => option.Value == PaymentMethod.VietQr);
    }

    [Fact]
    public async Task Invalid_date_range_must_not_call_service()
    {
        var service = new FakeHistoryService();
        using var viewModel = CreateViewModel(service);
        viewModel.FromDate = DateTime.Today;
        viewModel.ToDate = DateTime.Today.AddDays(-1);

        await ExecuteAsync(viewModel.SearchCommand);

        Assert.Equal(0, service.SearchCalls);
        Assert.NotNull(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Search_must_reset_page()
    {
        var service = new FakeHistoryService
        {
            SearchResult = SuccessPage(pageNumber: 2, totalCount: 60)
        };
        using var viewModel = CreateViewModel(service);
        await ExecuteAsync(viewModel.NextPageCommand, requireCanExecute: false);

        await ExecuteAsync(viewModel.SearchCommand);

        Assert.Equal(1, service.LastRequest?.PageNumber);
    }

    [Fact]
    public async Task Reset_filters_must_load_once()
    {
        var service = new FakeHistoryService();
        using var viewModel = CreateViewModel(service);
        viewModel.SearchText = "HD";
        viewModel.FromDate = null;
        viewModel.ToDate = null;

        await ExecuteAsync(viewModel.ResetFiltersCommand);

        Assert.Equal(1, service.SearchCalls);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(DateTime.Today, viewModel.FromDate);
    }

    [Fact]
    public async Task Selecting_order_must_load_details_once()
    {
        var service = new FakeHistoryService
        {
            DetailsResult = SuccessDetails(hasSnapshot: true)
        };
        using var viewModel = CreateViewModel(service);

        viewModel.SelectedOrder = CreateRow(17);
        await WaitForPropertyAsync(
            viewModel,
            nameof(OrderHistoryViewModel.IsLoadingDetails),
            () => !viewModel.IsLoadingDetails);

        Assert.Equal(1, service.DetailsCalls);
    }

    [Fact]
    public async Task Clearing_selection_must_clear_details_without_service_call()
    {
        var service = new FakeHistoryService
        {
            DetailsResult = SuccessDetails(hasSnapshot: true)
        };
        using var viewModel = CreateViewModel(service);
        viewModel.SelectedOrder = CreateRow(17);
        await WaitForPropertyAsync(
            viewModel,
            nameof(OrderHistoryViewModel.IsLoadingDetails),
            () => !viewModel.IsLoadingDetails);

        viewModel.SelectedOrder = null;

        Assert.Empty(viewModel.SelectedOrderLines);
        Assert.Equal(1, service.DetailsCalls);
    }

    [Fact]
    public async Task Missing_snapshot_must_disable_reprint()
    {
        var service = new FakeHistoryService
        {
            DetailsResult = SuccessDetails(hasSnapshot: false)
        };
        using var viewModel = CreateViewModel(service);

        viewModel.SelectedOrder = CreateRow(17);
        await WaitForPropertyAsync(
            viewModel,
            nameof(OrderHistoryViewModel.IsLoadingDetails),
            () => !viewModel.IsLoadingDetails);

        Assert.False(viewModel.CanOpenReceipt);
        Assert.Contains("chưa có snapshot", viewModel.ReceiptAvailabilityMessage);
    }

    [Fact]
    public async Task Successful_reprint_must_open_preview_once()
    {
        var service = new FakeHistoryService
        {
            DetailsResult = SuccessDetails(hasSnapshot: true),
            ReprintResult = Result.Success(CreateReceipt())
        };
        var preview = new FakePreviewService();
        using var viewModel = CreateViewModel(service, preview);
        viewModel.SelectedOrder = CreateRow(17);
        await WaitForPropertyAsync(
            viewModel,
            nameof(OrderHistoryViewModel.IsLoadingDetails),
            () => !viewModel.IsLoadingDetails);

        await ExecuteAsync(viewModel.OpenReceiptCommand);

        Assert.Equal(1, service.ReprintCalls);
        Assert.Equal(1, preview.Calls);
    }

    [Fact]
    public async Task Reprint_failure_must_not_open_preview()
    {
        var service = new FakeHistoryService
        {
            DetailsResult = SuccessDetails(hasSnapshot: true)
        };
        var preview = new FakePreviewService();
        using var viewModel = CreateViewModel(service, preview);
        viewModel.SelectedOrder = CreateRow(17);
        await WaitForPropertyAsync(
            viewModel,
            nameof(OrderHistoryViewModel.IsLoadingDetails),
            () => !viewModel.IsLoadingDetails);

        await ExecuteAsync(viewModel.OpenReceiptCommand);

        Assert.Equal(1, service.ReprintCalls);
        Assert.Equal(0, preview.Calls);
    }

    [Fact]
    public void Order_history_view_model_must_not_depend_on_repository_or_DbContext()
    {
        var parameters = typeof(OrderHistoryViewModel)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(IOrderRepository), parameters);
        Assert.DoesNotContain(typeof(PosDbContext), parameters);
    }

    [Fact]
    public void Shell_history_command_must_use_window_service_abstraction()
    {
        var parameters = typeof(ShellViewModel)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType);

        Assert.Contains(typeof(IOrderHistoryWindowService), parameters);
    }

    [Fact]
    public void Window_service_must_create_scoped_window()
    {
        var parameters = typeof(OrderHistoryWindowService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType);

        Assert.Contains(
            typeof(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory),
            parameters);
    }

    [Fact]
    public void Order_history_line_view_model_must_not_expose_cost_price()
    {
        var names = typeof(OrderHistoryLineViewModel)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("UnitCostPrice", names);
        Assert.DoesNotContain("CostPrice", names);
    }

    [Fact]
    public void No_new_hex_color_must_be_added_to_OrderHistoryWindow()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "POS.Wpf",
            "Views",
            "OrderHistoryWindow.xaml");
        var text = File.ReadAllText(path);

        Assert.DoesNotMatch("#[0-9A-Fa-f]{3,8}", text);
    }

    [Fact]
    public void Order_history_window_must_fit_1366_by_768()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "POS.Wpf",
            "Views",
            "OrderHistoryWindow.xaml");
        var document = System.Xml.Linq.XDocument.Load(path);
        var root = document.Root;
        Assert.NotNull(root);

        Assert.True((double?)root!.Attribute("MinWidth") <= 1366);
        Assert.True((double?)root.Attribute("MinHeight") <= 768);
    }

    [Fact]
    public void Order_history_paging_display_must_not_use_two_way_binding()
    {
        var document = LoadOrderHistoryXaml();

        AssertBindingMode(document, "CurrentPage", "OneWay");
        AssertBindingMode(document, "TotalPages", "OneWay");
        AssertBindingMode(document, "TotalCount", "OneWay");
    }

    [Fact]
    public void Read_only_order_history_properties_must_not_be_two_way_targets()
    {
        var document = LoadOrderHistoryXaml();
        var properties = typeof(OrderHistoryViewModel)
            .GetProperties()
            .Where(property => property.SetMethod?.IsPublic != true)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        var bindings = GetBindings(document)
            .Where(binding => properties.Contains(binding.Path))
            .ToArray();

        Assert.NotEmpty(bindings);
        Assert.All(
            bindings,
            binding =>
            {
                Assert.NotEqual("TwoWay", binding.Mode);
                Assert.NotEqual("OneWayToSource", binding.Mode);
                if (binding.Element.Name.LocalName == "Run" &&
                    binding.Attribute.Name.LocalName == "Text")
                {
                    Assert.Equal("OneWay", binding.Mode);
                }
            });
    }

    [Fact]
    public void Editable_history_filters_must_keep_two_way_binding()
    {
        var document = LoadOrderHistoryXaml();
        var editableProperties = new[]
        {
            "SearchText",
            "FromDate",
            "ToDate",
            "SelectedStatusFilter",
            "SelectedPaymentMethodFilter",
            "SelectedOrder"
        };

        Assert.All(
            editableProperties,
            property => AssertBindingMode(document, property, "TwoWay"));
    }

    [Fact]
    public void Order_history_window_must_construct_without_read_only_binding_exception()
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var application = new POS.Wpf.App();
                    application.InitializeComponent();
                }
                var window = new OrderHistoryWindow(CreateViewModel());
                window.Close();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(captured);
    }

    [Fact]
    public void Paging_commands_must_remain_the_only_way_to_change_current_page()
    {
        var document = LoadOrderHistoryXaml();
        var bindings = GetBindings(document).ToArray();

        Assert.Contains(
            bindings,
            binding => binding.Path == "PreviousPageCommand" &&
                       binding.Attribute.Name.LocalName == "Command" &&
                       binding.Element.Name.LocalName == "Button");
        Assert.Contains(
            bindings,
            binding => binding.Path == "NextPageCommand" &&
                       binding.Attribute.Name.LocalName == "Command" &&
                       binding.Element.Name.LocalName == "Button");
        AssertBindingMode(document, "CurrentPage", "OneWay");
    }

    [Fact]
    public void Closing_view_model_must_cancel_pending_requests()
    {
        var service = new FakeHistoryService
        {
            PendingSearch = new TaskCompletionSource<
                Result<PagedResult<OrderHistoryListItemDto>>>(
                    TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var viewModel = CreateViewModel(service);
        viewModel.LoadCommand.Execute(null);

        viewModel.Dispose();

        Assert.True(service.LastSearchToken.IsCancellationRequested);
    }

    private static OrderHistoryViewModel CreateViewModel(
        FakeHistoryService? service = null,
        FakePreviewService? preview = null) =>
        new(
            service ?? new FakeHistoryService(),
            preview ?? new FakePreviewService(),
            NullLogger<OrderHistoryViewModel>.Instance);

    private static ReceiptRequest CreateReceipt()
    {
        var line = new ReceiptLineDto(
            1,
            1,
            "SP-01",
            "Sản phẩm",
            "Cái",
            1,
            10_000,
            0,
            10_000,
            10_000,
            0,
            10_000,
            null,
            []);
        return new ReceiptRequest(
            new ReceiptStoreSnapshotDto("Cửa hàng"),
            ReceiptCopyKind.Reprint,
            1,
            17,
            "HD-17",
            "Thu ngân",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            PaymentMethod.Cash,
            10_000,
            0,
            10_000,
            10_000,
            0,
            [line],
            paidAtUtc: DateTimeOffset.UtcNow);
    }

    private static async Task ExecuteAsync(
        AsyncRelayCommand command,
        bool requireCanExecute = true)
    {
        if (requireCanExecute)
        {
            Assert.True(command.CanExecute(null));
        }
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var began = false;
        void Handler(object? sender, EventArgs args)
        {
            if (command.IsExecuting)
            {
                began = true;
            }
            else if (began)
            {
                completion.TrySetResult();
            }
        }
        command.CanExecuteChanged += Handler;
        command.Execute(null);
        if (!command.IsExecuting && !began)
        {
            completion.TrySetResult();
        }
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        command.CanExecuteChanged -= Handler;
    }

    private static async Task WaitForPropertyAsync(
        OrderHistoryViewModel viewModel,
        string propertyName,
        Func<bool> condition)
    {
        if (condition())
        {
            await Task.Yield();
            if (condition())
            {
                return;
            }
        }
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == propertyName && condition())
            {
                completion.TrySetResult();
            }
        }
        viewModel.PropertyChanged += Handler;
        if (condition())
        {
            completion.TrySetResult();
        }
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        viewModel.PropertyChanged -= Handler;
    }

    private static Result<PagedResult<OrderHistoryListItemDto>> SuccessPage(
        int pageNumber = 1,
        int totalCount = 0) =>
        Result.Success(
            new PagedResult<OrderHistoryListItemDto>(
                [],
                pageNumber,
                25,
                totalCount));

    private static Result<OrderHistoryDetailsDto> SuccessDetails(
        bool hasSnapshot) =>
        Result.Success(
            new OrderHistoryDetailsDto(
                17,
                "HD-17",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                1,
                "Thu ngân",
                OrderStatus.Completed,
                PaymentMethod.Cash,
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                null,
                null,
                hasSnapshot,
                []));

    private static OrderHistoryRowViewModel CreateRow(int id) =>
        new(
            new OrderHistoryListItemDto(
                id,
                $"HD-{id}",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                1,
                "Thu ngân",
                OrderStatus.Completed,
                PaymentMethod.Cash,
                0,
                0,
                0,
                0,
                0));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ??
            throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }

    private static System.Xml.Linq.XElement FindShellNavigationButton(
        string text)
    {
        var document = System.Xml.Linq.XDocument.Parse(
            ReadRepositoryFile(
                "src", "POS.Wpf", "Views", "ShellWindow.xaml"));
        var presentation =
            System.Xml.Linq.XNamespace.Get(
                "http://schemas.microsoft.com/winfx/2006/xaml/presentation");

        return document
            .Descendants(presentation + "Button")
            .Single(button => button
                .Descendants(presentation + "TextBlock")
                .Any(label =>
                    string.Equals(
                        (string?)label.Attribute("Text"),
                        text,
                        StringComparison.Ordinal)));
    }

    private static string ReadRepositoryFile(
        params string[] pathParts) =>
        File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                Path.Combine(pathParts)));

    private static System.Xml.Linq.XDocument LoadOrderHistoryXaml() =>
        System.Xml.Linq.XDocument.Parse(
            ReadRepositoryFile(
                "src", "POS.Wpf", "Views", "OrderHistoryWindow.xaml"));

    private static IEnumerable<XamlBindingContract> GetBindings(
        System.Xml.Linq.XDocument document)
    {
        foreach (var element in document.Descendants())
        {
            foreach (var attribute in element.Attributes())
            {
                var value = attribute.Value;
                if (!value.StartsWith("{Binding ", StringComparison.Ordinal))
                {
                    continue;
                }

                var body = value["{Binding ".Length..^1];
                var parts = body.Split(
                    ',',
                    StringSplitOptions.TrimEntries |
                    StringSplitOptions.RemoveEmptyEntries);
                var path = parts[0].StartsWith("Path=", StringComparison.Ordinal)
                    ? parts[0]["Path=".Length..]
                    : parts[0];
                var mode = parts
                    .Skip(1)
                    .FirstOrDefault(part =>
                        part.StartsWith("Mode=", StringComparison.Ordinal));

                yield return new XamlBindingContract(
                    element,
                    attribute,
                    path,
                    mode?["Mode=".Length..]);
            }
        }
    }

    private static void AssertBindingMode(
        System.Xml.Linq.XDocument document,
        string path,
        string expectedMode)
    {
        var binding = GetBindings(document)
            .Single(candidate => candidate.Path == path);

        Assert.Equal(expectedMode, binding.Mode);
    }

    private static ShellTestContext CreateShellContext(
        bool hasViewReports)
    {
        var services = new ServiceCollection()
            .BuildServiceProvider();
        var windowService = new FakeOrderHistoryWindowService();
        var viewModel = new ShellViewModel(
            services.GetRequiredService<IServiceScopeFactory>(),
            new FakeProductDialogService(),
            new FakeCategoryManagementDialogService(),
            new FakeInventoryDialogService(),
            windowService,
            new FakePermissionService(hasViewReports),
            NullLogger<ShellViewModel>.Instance);
        return new ShellTestContext(viewModel, windowService);
    }

    private sealed record ShellTestContext(
        ShellViewModel ViewModel,
        FakeOrderHistoryWindowService WindowService);

    private sealed record XamlBindingContract(
        System.Xml.Linq.XElement Element,
        System.Xml.Linq.XAttribute Attribute,
        string Path,
        string? Mode);

    private sealed class FakeOrderHistoryWindowService :
        IOrderHistoryWindowService
    {
        public int ShowCalls { get; private set; }

        public Task ShowAsync()
        {
            ShowCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePermissionService(
        bool hasViewReports) : IPermissionService
    {
        public bool HasPermission(SystemCapability permission) =>
            permission != SystemCapability.ViewReports ||
            hasViewReports;

        public Result Authorize(SystemCapability permission) =>
            HasPermission(permission)
                ? Result.Success()
                : Result.Failure(
                    new AppError("TEST.FORBIDDEN", "Không có quyền."));
    }

    private sealed class FakeProductDialogService :
        IProductDialogService
    {
        public Task<bool> ShowCreateAsync() => Task.FromResult(false);
        public Task<bool> ShowEditAsync(int productId) => Task.FromResult(false);
    }

    private sealed class FakeCategoryManagementDialogService :
        ICategoryManagementDialogService
    {
        public Task ShowAsync() => Task.CompletedTask;
    }

    private sealed class FakeInventoryDialogService :
        IInventoryDialogService
    {
        public Task<bool> ShowAdjustmentAsync(int productId) =>
            Task.FromResult(false);

        public Task ShowHistoryAsync(int? productId = null) =>
            Task.CompletedTask;
    }

    private sealed class FakePreviewService : IReceiptPreviewService
    {
        public int Calls { get; private set; }
        public Task ShowAsync(
            ReceiptRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHistoryService : IOrderHistoryService
    {
        public int SearchCalls { get; private set; }
        public int DetailsCalls { get; private set; }
        public int ReprintCalls { get; private set; }
        public OrderHistorySearchRequest? LastRequest { get; private set; }
        public CancellationToken LastSearchToken { get; private set; }
        public TaskCompletionSource<
            Result<PagedResult<OrderHistoryListItemDto>>>? PendingSearch { get; init; }
        public Result<PagedResult<OrderHistoryListItemDto>> SearchResult { get; init; } =
            SuccessPage();
        public Result<OrderHistoryDetailsDto> DetailsResult { get; init; } =
            SuccessDetails(false);
        public Result<ReceiptRequest> ReprintResult { get; init; } =
            Result.Failure<ReceiptRequest>(
                new AppError("TEST.REPRINT", "Không có hóa đơn."));

        public Task<Result<PagedResult<OrderHistoryListItemDto>>> SearchAsync(
            OrderHistorySearchRequest request,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            LastRequest = request;
            LastSearchToken = cancellationToken;
            return PendingSearch?.Task ?? Task.FromResult(SearchResult);
        }

        public Task<Result<OrderHistoryDetailsDto>> GetDetailsAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            DetailsCalls++;
            return Task.FromResult(DetailsResult);
        }

        public Task<Result<ReceiptRequest>> GetReprintReceiptAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            ReprintCalls++;
            return Task.FromResult(ReprintResult);
        }
    }
}
