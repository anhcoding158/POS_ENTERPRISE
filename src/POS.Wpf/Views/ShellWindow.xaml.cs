using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Domain.Enums;
using POS.Wpf.Authorization;
using POS.Wpf.ViewModels;
using POS.Wpf.Services;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ chính của ứng dụng.
///
/// Chức năng:
/// - hiển thị thông tin người đang đăng nhập;
/// - phản ánh quyền người dùng lên trạng thái các nút;
/// - xử lý đăng xuất và quay lại LoginWindow.
/// </summary>
public partial class ShellWindow :
    global::System.Windows.Window
{
    private readonly ShellViewModel
        _viewModel;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IPermissionService
        _permissionService;

    private readonly IServiceScopeFactory
        _scopeFactory;

    private global::System.Windows.Controls.Button?
        _logoutButton;

    private bool _logoutInProgress;
    private bool _userCardConfigured;
    private bool _permissionsConfigured;

    public ShellWindow(
        ShellViewModel viewModel,
        ICurrentUserService currentUserService,
        IPermissionService permissionService,
        IServiceScopeFactory scopeFactory)
    {
        _viewModel =
            viewModel ??
            throw new ArgumentNullException(
                nameof(viewModel));

        _currentUserService =
            currentUserService ??
            throw new ArgumentNullException(
                nameof(currentUserService));

        _permissionService =
            permissionService ??
            throw new ArgumentNullException(
                nameof(permissionService));

        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

        if (!_currentUserService.IsAuthenticated)
        {
            throw new InvalidOperationException(
                "Không thể mở màn hình chính khi chưa đăng nhập.");
        }

        InitializeComponent();

        DataContext =
            _viewModel;

        Loaded +=
            OnWindowLoaded;

        Closed +=
            OnWindowClosed;

        PreviewKeyDown +=
            OnPreviewKeyDown;
    }

    /// <summary>
    /// True khi Shell đóng do người dùng đăng xuất.
    ///
    /// App sẽ mở LoginWindow thay vì kết thúc tiến trình.
    /// </summary>
    public bool LogoutRequested
    {
        get;
        private set;
    }

    private async void OnWindowLoaded(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        Loaded -=
            OnWindowLoaded;

        /*
         * Đợi WPF hoàn thành Visual Tree.
         *
         * Các button Command và phần tử template chỉ có thể
         * tìm thấy ổn định sau thời điểm Window Loaded.
         */
        await Dispatcher.InvokeAsync(
            () =>
            {
                ConfigureAuthenticatedUserCard();
                ConfigureRolePermissions();
            },
            global::System.Windows.Threading
                .DispatcherPriority.Loaded);

        await _viewModel.InitializeAsync();
    }

    /// <summary>
    /// Disable những command không được phép theo role.
    ///
    /// Không thay thế việc phân quyền ở Application Service.
    /// Đây chỉ là phản hồi trực quan cho người dùng.
    /// </summary>
    private void ConfigureRolePermissions()
    {
        if (_permissionsConfigured)
        {
            return;
        }

        var permissionState =
            ShellPermissionState.Create(
                _permissionService);

        ApplyCommandPermission(
            _viewModel.AddProductCommand,
            permissionState.CanManageProducts,
            SystemCapability.ManageProducts);

        ApplyCommandPermission(
            _viewModel.EditProductCommand,
            permissionState.CanManageProducts,
            SystemCapability.ManageProducts);

        ApplyCommandPermission(
            _viewModel.ToggleProductActiveCommand,
            permissionState.CanManageProducts,
            SystemCapability.ManageProducts);

        ApplyMenuItemPermission(
            ToggleProductArchiveMenuItem,
            permissionState.CanManageProducts,
            SystemCapability.ManageProducts);

        if (!permissionState.CanManageProducts)
        {
            ToggleProductActiveButton.IsEnabled =
                false;

            ToggleProductActiveButton.Opacity =
                0.48;

            ToggleProductActiveButton.Cursor =
                global::System.Windows.Input
                    .Cursors.Arrow;

            ToggleProductActiveButton.ToolTip =
                "Tài khoản hiện tại không có quyền quản lý sản phẩm.";

            global::System.Windows.Controls
                .ToolTipService
                .SetShowOnDisabled(
                    ToggleProductActiveButton,
                    true);
        }

        ApplyCommandPermission(
            _viewModel.OpenCategoryManagementCommand,
            permissionState.CanManageCategories,
            SystemCapability.ManageCategories);

        ApplyCommandPermission(
            _viewModel.AdjustInventoryCommand,
            permissionState.CanAdjustInventory,
            SystemCapability.AdjustInventory);

        ApplyMenuItemPermission(
            AdjustInventoryMenuItem,
            permissionState.CanAdjustInventory,
            SystemCapability.AdjustInventory);

        ApplyCommandPermission(
            _viewModel.ViewInventoryHistoryCommand,
            permissionState.CanViewInventoryHistory,
            SystemCapability.ViewInventoryHistory);

        ApplyCommandPermission(
            _viewModel.OpenOrderHistoryCommand,
            permissionState.CanViewReports,
            SystemCapability.ViewReports);

        ApplyMenuItemPermission(
            InventoryHistoryMenuItem,
            permissionState.CanViewInventoryHistory,
            SystemCapability.ViewInventoryHistory);

        var canUseCheckout =
        _permissionService.HasPermission(
        SystemCapability.UseCheckout);

        SalesNavigationButton.IsEnabled =
            canUseCheckout;

        SalesNavigationButton.ToolTip =
            canUseCheckout
                ? "Mở quầy bán hàng"
                : "Tài khoản hiện tại không có quyền thực hiện bán hàng.";
        _permissionsConfigured =
            true;
    }

    private void ApplyCommandPermission(
        global::System.Windows.Input.ICommand command,
        bool isAllowed,
        SystemCapability permission)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        if (isAllowed)
        {
            /*
             * Không ép IsEnabled = true.
             *
             * Command vẫn tự quyết định trạng thái dựa trên:
             * - IsLoading;
             * - sản phẩm đang chọn;
             * - sản phẩm có theo dõi kho hay không.
             */
            return;
        }

        var permissionName =
            RolePermissionPolicy.GetDisplayName(
                permission);

        var deniedMessage =
            $"Tài khoản hiện tại không có quyền " +
            $"{permissionName}.";

        var matchingButtons =
            FindVisualChildren<
                    global::System.Windows.Controls.Button>(
                    this)
                .Where(
                    button =>
                        ReferenceEquals(
                            button.Command,
                            command))
                .ToArray();

        foreach (var button in matchingButtons)
        {
            button.IsEnabled =
                false;

            button.Opacity =
                0.48;

            button.Cursor =
                global::System.Windows.Input
                    .Cursors.Arrow;

            button.ToolTip =
                deniedMessage;

            global::System.Windows.Controls
                .ToolTipService
                .SetShowOnDisabled(
                    button,
                    true);
        }
    }

    private static void ApplyMenuItemPermission(
        global::System.Windows.Controls.MenuItem menuItem,
        bool isAllowed,
        SystemCapability permission)
    {
        if (isAllowed)
        {
            return;
        }

        var permissionName =
            RolePermissionPolicy.GetDisplayName(
                permission);

        menuItem.IsEnabled =
            false;

        menuItem.Opacity =
            0.48;

        menuItem.ToolTip =
            $"Tài khoản hiện tại không có quyền " +
            $"{permissionName}.";

        global::System.Windows.Controls
            .ToolTipService
            .SetShowOnDisabled(
                menuItem,
                true);
    }

    private void ConfigureAuthenticatedUserCard()
    {
        if (_userCardConfigured)
        {
            return;
        }

        var fullName =
            string.IsNullOrWhiteSpace(
                _currentUserService.FullName)
                ? _currentUserService.Username ??
                  "Người dùng"
                : _currentUserService.FullName.Trim();

        var username =
            _currentUserService.Username?
                .Trim() ??
            string.Empty;

        var roleText =
            GetRoleDisplayName(
                _currentUserService.Role);

        Title =
            $"POS Enterprise — {fullName}";

        var nameTextBlock =
            FindTextBlockByExactText(
                this,
                "Quản trị viên");

        if (nameTextBlock is null)
        {
            return;
        }

        nameTextBlock.Text =
            fullName;

        nameTextBlock.ToolTip =
            fullName;

        var informationPanel =
            global::System.Windows.Media
                .VisualTreeHelper
                .GetParent(
                    nameTextBlock)
            as global::System.Windows.Controls
                .StackPanel;

        if (informationPanel is not null)
        {
            ConfigureUserInformation(
                informationPanel,
                roleText,
                username);
        }

        var horizontalPanel =
            informationPanel is null
                ? null
                : global::System.Windows.Media
                    .VisualTreeHelper
                    .GetParent(
                        informationPanel)
                    as global::System.Windows.Controls
                        .StackPanel;

        if (horizontalPanel is not null)
        {
            ConfigureAvatar(
                horizontalPanel,
                fullName);

            ReplaceArrowWithLogoutButton(
                horizontalPanel);
        }

        _userCardConfigured =
            true;
    }

    private static void ConfigureUserInformation(
        global::System.Windows.Controls.StackPanel
            informationPanel,
        string roleText,
        string username)
    {
        if (informationPanel.Children.Count < 2 ||
            informationPanel.Children[1] is not
                global::System.Windows.Controls
                    .TextBlock detailTextBlock)
        {
            return;
        }

        detailTextBlock.Text =
            string.IsNullOrWhiteSpace(
                username)
                ? roleText
                : $"{roleText} • @{username}";

        detailTextBlock.ToolTip =
            detailTextBlock.Text;

        detailTextBlock.TextTrimming =
            global::System.Windows
                .TextTrimming.CharacterEllipsis;

        detailTextBlock.MaxWidth =
            220;
    }

    private static void ConfigureAvatar(
        global::System.Windows.Controls.StackPanel
            horizontalPanel,
        string displayName)
    {
        if (horizontalPanel.Children.Count == 0 ||
            horizontalPanel.Children[0] is not
                global::System.Windows.Controls.Border
                    avatarBorder ||
            avatarBorder.Child is not
                global::System.Windows.Controls.TextBlock
                    avatarTextBlock)
        {
            return;
        }

        var initial =
            displayName
                .Trim()
                .FirstOrDefault(
                    character =>
                        !char.IsWhiteSpace(
                            character));

        avatarTextBlock.Text =
            initial == default
                ? "U"
                : char.ToUpperInvariant(
                        initial)
                    .ToString();

        avatarBorder.ToolTip =
            displayName;
    }

    private void ReplaceArrowWithLogoutButton(
        global::System.Windows.Controls.StackPanel
            horizontalPanel)
    {
        if (_logoutButton is not null)
        {
            return;
        }

        /*
         * Phần tử cuối ban đầu là biểu tượng mũi tên.
         */
        if (horizontalPanel.Children.Count >= 3)
        {
            horizontalPanel.Children.RemoveAt(
                horizontalPanel.Children.Count - 1);
        }

        var logoutButton =
            new global::System.Windows.Controls.Button
            {
                MinWidth =
                    100,

                Height =
                    36,

                Margin =
                    new global::System.Windows.Thickness(
                        2,
                        0,
                        0,
                        0),

                ToolTip =
                    "Đăng xuất khỏi phiên làm việc " +
                    "(Ctrl + Shift + L)",

                Cursor =
                    global::System.Windows.Input
                        .Cursors.Hand
            };

        logoutButton.Content =
            CreateLogoutButtonContent();

        if (TryFindResource(
                "SecondaryButtonStyle")
            is global::System.Windows.Style style)
        {
            logoutButton.Style =
                style;
        }

        logoutButton.Click +=
            OnLogoutButtonClick;

        horizontalPanel.Children.Add(
            logoutButton);

        _logoutButton =
            logoutButton;
    }

    private static global::System.Windows.Controls
        .StackPanel
        CreateLogoutButtonContent()
    {
        var panel =
            new global::System.Windows.Controls.StackPanel
            {
                Orientation =
                    global::System.Windows.Controls
                        .Orientation.Horizontal,

                HorizontalAlignment =
                    global::System.Windows
                        .HorizontalAlignment.Center
            };

        panel.Children.Add(
            new global::System.Windows.Controls.TextBlock
            {
                Text =
                    "\uE8AC",

                FontFamily =
                    new global::System.Windows.Media
                        .FontFamily(
                            "Segoe MDL2 Assets"),

                FontSize =
                    12,

                Margin =
                    new global::System.Windows.Thickness(
                        0,
                        0,
                        7,
                        0),

                VerticalAlignment =
                    global::System.Windows
                        .VerticalAlignment.Center
            });

        panel.Children.Add(
            new global::System.Windows.Controls.TextBlock
            {
                Text =
                    "Đăng xuất",

                FontSize =
                    11,

                FontWeight =
                    global::System.Windows
                        .FontWeights.SemiBold,

                VerticalAlignment =
                    global::System.Windows
                        .VerticalAlignment.Center
            });

        return panel;
    }

    private async void OnLogoutButtonClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        await RequestLogoutAsync();
    }

    private void OnToggleProductActiveClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        var command =
            _viewModel.ToggleProductActiveCommand;

        if (!command.CanExecute(
                parameter: null))
        {
            return;
        }

        var selectedProduct =
            _viewModel.SelectedProduct;

        if (selectedProduct is null)
        {
            return;
        }

        if (selectedProduct.IsActive)
        {
            var confirmation =
                global::System.Windows.MessageBox.Show(
                    this,
                    $"Bạn có chắc muốn ngừng bán sản phẩm\n" +
                    $"“{selectedProduct.Name}” không?\n\n" +
                    "Sản phẩm sẽ không còn xuất hiện tại quầy bán hàng, " +
                    "nhưng tồn kho, hình ảnh và lịch sử giao dịch " +
                    "vẫn được giữ nguyên.",
                    "Ngừng bán sản phẩm",
                    global::System.Windows
                        .MessageBoxButton.YesNo,
                    global::System.Windows
                        .MessageBoxImage.Warning,
                    global::System.Windows
                        .MessageBoxResult.No);

            if (confirmation !=
                global::System.Windows
                    .MessageBoxResult.Yes)
            {
                return;
            }
        }

        command.Execute(
            parameter: null);
    }

    private void OnMoreProductActionsClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        if (sender is not
            global::System.Windows.Controls.Button button ||
            button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget =
            button;

        button.ContextMenu.IsOpen =
            true;
    }

    private void OnToggleProductArchiveClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        var command =
            _viewModel.ToggleProductArchiveCommand;

        if (!command.CanExecute(
                parameter: null))
        {
            return;
        }

        var selectedProduct =
            _viewModel.SelectedProduct;

        if (selectedProduct is null)
        {
            return;
        }

        var confirmation =
            selectedProduct.IsArchived
                ? global::System.Windows.MessageBox.Show(
                    this,
                    $"Khôi phục sản phẩm “{selectedProduct.Name}”?\n\n" +
                    "Sau khi khôi phục, sản phẩm vẫn ở trạng thái ngừng bán.\n" +
                    "Bạn cần bấm “Bán lại” riêng nếu muốn đưa sản phẩm ra quầy.",
                    "Khôi phục sản phẩm",
                    global::System.Windows
                        .MessageBoxButton.YesNo,
                    global::System.Windows
                        .MessageBoxImage.Question,
                    global::System.Windows
                        .MessageBoxResult.No)
                : global::System.Windows.MessageBox.Show(
                    this,
                    $"Bạn có chắc muốn lưu trữ sản phẩm\n" +
                    $"“{selectedProduct.Name}” không?\n\n" +
                    "Sản phẩm sẽ bị ngừng bán và ẩn khỏi danh sách thông thường.\n" +
                    "Tồn kho, hình ảnh và toàn bộ lịch sử vẫn được giữ nguyên.\n" +
                    "Bạn có thể khôi phục sản phẩm sau này.",
                    "Lưu trữ sản phẩm",
                    global::System.Windows
                        .MessageBoxButton.YesNo,
                    global::System.Windows
                        .MessageBoxImage.Warning,
                    global::System.Windows
                        .MessageBoxResult.No);

        if (confirmation !=
            global::System.Windows
                .MessageBoxResult.Yes)
        {
            return;
        }

        command.Execute(
            parameter: null);
    }

    private void OnFilterButtonClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        ProductFilterPopup.IsOpen =
            !ProductFilterPopup.IsOpen;
    }

    private void OnCloseProductFilterClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        ProductFilterPopup.IsOpen =
            false;
    }

    private async void OnPreviewKeyDown(
        object sender,
        global::System.Windows.Input
            .KeyEventArgs e)
    {
        var modifiers =
            global::System.Windows.Input
                .Keyboard.Modifiers;

        var controlPressed =
            modifiers.HasFlag(
                global::System.Windows.Input
                    .ModifierKeys.Control);

        var shiftPressed =
            modifiers.HasFlag(
                global::System.Windows.Input
                    .ModifierKeys.Shift);

        if (!controlPressed ||
            !shiftPressed ||
            e.Key !=
                global::System.Windows.Input.Key.L)
        {
            return;
        }

        e.Handled =
            true;

        await RequestLogoutAsync();
    }

    private async Task RequestLogoutAsync()
    {
        if (_logoutInProgress)
        {
            return;
        }

        var displayName =
            string.IsNullOrWhiteSpace(
                _currentUserService.FullName)
                ? _currentUserService.Username ??
                  "tài khoản hiện tại"
                : _currentUserService.FullName.Trim();

        var confirmation =
            global::System.Windows.MessageBox.Show(
                $"Bạn có chắc muốn đăng xuất khỏi " +
                $"tài khoản “{displayName}” không?\n\n" +
                "Phiên làm việc hiện tại sẽ được kết thúc.",
                "Xác nhận đăng xuất",
                global::System.Windows
                    .MessageBoxButton.YesNo,
                global::System.Windows
                    .MessageBoxImage.Question,
                global::System.Windows
                    .MessageBoxResult.No);

        if (confirmation !=
            global::System.Windows
                .MessageBoxResult.Yes)
        {
            return;
        }

        _logoutInProgress =
            true;

        if (_logoutButton is not null)
        {
            _logoutButton.IsEnabled =
                false;

            _logoutButton.Content =
                "Đang đăng xuất...";
        }

        try
        {
            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var authService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IAuthService>();

            var result =
                authService.Logout();

            if (result.IsFailure)
            {
                throw new InvalidOperationException(
                    result.AppError.Message);
            }

            LogoutRequested =
                true;

            global::System.Windows.Application
                .Current
                .ShutdownMode =
                    global::System.Windows
                        .ShutdownMode
                        .OnExplicitShutdown;

            Close();
        }
        catch (Exception exception)
        {
            _logoutInProgress =
                false;

            if (_logoutButton is not null)
            {
                _logoutButton.IsEnabled =
                    true;

                _logoutButton.Content =
                    CreateLogoutButtonContent();
            }

            global::System.Windows.MessageBox.Show(
                "Không thể đăng xuất.\n\n" +
                exception
                    .GetBaseException()
                    .Message,
                "POS Enterprise",
                global::System.Windows
                    .MessageBoxButton.OK,
                global::System.Windows
                    .MessageBoxImage.Error);
        }
    }

    private void OnWindowClosed(
        object? sender,
        EventArgs e)
    {
        Loaded -=
            OnWindowLoaded;

        Closed -=
            OnWindowClosed;

        PreviewKeyDown -=
            OnPreviewKeyDown;

        if (_logoutButton is not null)
        {
            _logoutButton.Click -=
                OnLogoutButtonClick;

            _logoutButton =
                null;
        }
    }

    private static string GetRoleDisplayName(
        Role? role)
    {
        return role switch
        {
            Role.Administrator =>
                "Quản trị viên",

            Role.Manager =>
                "Quản lý",

            Role.Cashier =>
                "Thu ngân",

            Role.InventoryStaff =>
                "Nhân viên kho",

            _ =>
                "Người dùng"
        };
    }

    private static IEnumerable<TControl>
        FindVisualChildren<TControl>(
            global::System.Windows.DependencyObject parent)
        where TControl :
            global::System.Windows.DependencyObject
    {
        ArgumentNullException.ThrowIfNull(
            parent);

        var childCount =
            global::System.Windows.Media
                .VisualTreeHelper
                .GetChildrenCount(
                    parent);

        for (var index = 0;
             index < childCount;
             index++)
        {
            var child =
                global::System.Windows.Media
                    .VisualTreeHelper
                    .GetChild(
                        parent,
                        index);

            if (child is TControl matchingChild)
            {
                yield return matchingChild;
            }

            foreach (var descendant in
                     FindVisualChildren<TControl>(
                         child))
            {
                yield return descendant;
            }
        }
    }

    private static global::System.Windows.Controls
        .TextBlock?
        FindTextBlockByExactText(
            global::System.Windows.DependencyObject
                parent,
            string expectedText)
    {
        var childCount =
            global::System.Windows.Media
                .VisualTreeHelper
                .GetChildrenCount(
                    parent);

        for (var index = 0;
             index < childCount;
             index++)
        {
            var child =
                global::System.Windows.Media
                    .VisualTreeHelper
                    .GetChild(
                        parent,
                        index);

            if (child is
                    global::System.Windows.Controls
                        .TextBlock textBlock &&
                string.Equals(
                    textBlock.Text,
                    expectedText,
                    StringComparison.Ordinal))
            {
                return textBlock;
            }

            var nestedResult =
                FindTextBlockByExactText(
                    child,
                    expectedText);

            if (nestedResult is not null)
            {
                return nestedResult;
            }
        }

        return null;
    }

    private async void OnOpenSalesClick(
    object sender,
    global::System.Windows
        .RoutedEventArgs e)
    {
        try
        {
            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var salesWindowService =
                scope.ServiceProvider
                    .GetRequiredService<
                        ISalesWindowService>();

            await salesWindowService
                .ShowAsync();

            /*
             * Thanh toán có thể đã thay đổi tồn kho.
             * Yêu cầu Shell tải lại dữ liệu sau khi đóng quầy.
             */
            await _viewModel
                .RefreshAfterExternalChangeAsync();
        }
        catch (Exception exception)
        {
            global::System.Windows
                .MessageBox.Show(
                    this,
                    "Không thể mở quầy bán hàng.\n\n" +
                    exception
                        .GetBaseException()
                        .Message,
                    "POS Enterprise",
                    global::System.Windows
                        .MessageBoxButton.OK,
                    global::System.Windows
                        .MessageBoxImage.Error);
        }
    }
}
