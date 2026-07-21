using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Authentication;
using POS.Domain.Enums;
using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ chính của ứng dụng.
///
/// Hiển thị phiên người dùng hiện tại và gửi yêu cầu
/// đăng xuất về vòng đời ứng dụng.
/// </summary>
public partial class ShellWindow :
    global::System.Windows.Window
{
    private readonly ShellViewModel
        _viewModel;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly IServiceScopeFactory
        _scopeFactory;

    private global::System.Windows.Controls.Button?
        _logoutButton;

    private bool
        _logoutInProgress;

    public ShellWindow(
        ShellViewModel viewModel,
        ICurrentUserService currentUserService,
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

        ConfigureAuthenticatedUserCard();

        Loaded +=
            OnWindowLoaded;

        Closed +=
            OnWindowClosed;

        PreviewKeyDown +=
            OnPreviewKeyDown;
    }

    /// <summary>
    /// True khi cửa sổ đóng do người dùng chọn đăng xuất.
    ///
    /// App sẽ mở lại LoginWindow thay vì tắt ứng dụng.
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

        await _viewModel.InitializeAsync();
    }

    private void ConfigureAuthenticatedUserCard()
    {
        var fullName =
            string.IsNullOrWhiteSpace(
                _currentUserService.FullName)
                ? _currentUserService.Username ??
                  "Người dùng"
                : _currentUserService.FullName;

        var username =
            _currentUserService.Username ??
            string.Empty;

        var roleText =
            GetRoleDisplayName(
                _currentUserService.Role);

        Title =
            $"POS Enterprise — {fullName}";

        /*
         * ShellWindow.xaml hiện có một thẻ tài khoản
         * với Text="Quản trị viên".
         *
         * Ta dùng nó làm điểm neo để không phải thay thế
         * toàn bộ file XAML rất lớn.
         */
        var nameTextBlock =
            FindTextBlockByExactText(
                this,
                "Quản trị viên");

        if (nameTextBlock is null)
        {
            /*
             * Phím Ctrl + Shift + L vẫn hoạt động ngay cả
             * khi giao diện tài khoản bị thay đổi sau này.
             */
            return;
        }

        nameTextBlock.Text =
            fullName;

        var informationPanel =
            global::System.Windows.Media
                .VisualTreeHelper
                .GetParent(
                    nameTextBlock)
            as global::System.Windows.Controls
                .StackPanel;

        if (informationPanel is not null &&
            informationPanel.Children.Count >= 2 &&
            informationPanel.Children[1] is
                global::System.Windows.Controls
                    .TextBlock detailTextBlock)
        {
            /*
             * Bỏ binding LastUpdatedText ở dòng phụ của card,
             * thay bằng đúng vai trò và username phiên hiện tại.
             */
            detailTextBlock.Text =
                username.Length == 0
                    ? roleText
                    : $"{roleText} • @{username}";
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

        if (horizontalPanel is null)
        {
            return;
        }

        ConfigureAvatar(
            horizontalPanel,
            fullName);

        ReplaceArrowWithLogoutButton(
            horizontalPanel);
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
    }

    private void ReplaceArrowWithLogoutButton(
        global::System.Windows.Controls.StackPanel
            horizontalPanel)
    {
        /*
         * Phần tử cuối hiện là biểu tượng mũi tên
         * của card Administrator cũ.
         */
        if (horizontalPanel.Children.Count >= 3)
        {
            horizontalPanel.Children.RemoveAt(
                horizontalPanel.Children.Count - 1);
        }

        var logoutButton =
            new global::System.Windows.Controls.Button
            {
                Content =
                    "Đăng xuất",

                MinWidth =
                    92,

                Height =
                    34,

                Margin =
                    new global::System.Windows.Thickness(
                        2,
                        0,
                        0,
                        0),

                ToolTip =
                    "Đăng xuất khỏi phiên làm việc " +
                    "(Ctrl + Shift + L)"
            };

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

    private async void OnLogoutButtonClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        await RequestLogoutAsync();
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

        e.Handled = true;

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
                : _currentUserService.FullName;

        var confirmation =
            global::System.Windows.MessageBox.Show(
                $"Bạn có chắc muốn đăng xuất khỏi " +
                $"tài khoản “{displayName}” không?\n\n" +
                "Mọi cửa sổ nghiệp vụ đang mở cần được " +
                "hoàn tất trước khi đăng xuất.",
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

        _logoutInProgress = true;

        if (_logoutButton is not null)
        {
            _logoutButton.IsEnabled =
                false;

            _logoutButton.Content =
                "Đang thoát...";
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
                    result.Error.Message);
            }

            LogoutRequested = true;

            /*
             * Bắt buộc giữ ứng dụng sống để App mở lại
             * LoginWindow sau khi ShellWindow đóng.
             */
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
            _logoutInProgress = false;

            if (_logoutButton is not null)
            {
                _logoutButton.IsEnabled =
                    true;

                _logoutButton.Content =
                    "Đăng xuất";
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

            _logoutButton = null;
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
}