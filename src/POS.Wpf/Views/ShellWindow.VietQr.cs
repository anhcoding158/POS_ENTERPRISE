using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Payments;
using POS.Domain.Enums;
using POS.Wpf.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POS.Wpf.Views;

/// <summary>
/// Phần mở rộng cấu hình VietQR của ShellWindow.
///
/// Giữ riêng:
/// - cấu hình ảnh QR nhận tiền;
/// - cấu hình cách đưa QR tới khách.
///
/// Không trộn logic này vào ShellWindow.xaml.cs.
/// </summary>
public partial class ShellWindow
{
    private Button?
        _vietQrImageButton;

    private Button?
        _vietQrDisplayModeButton;

    protected override void OnContentRendered(
        EventArgs e)
    {
        base.OnContentRendered(
            e);

        ConfigureVietQrNavigation();
    }

    protected override void OnClosed(
        EventArgs e)
    {
        if (_vietQrImageButton is not null)
        {
            _vietQrImageButton.Click -=
                OnOpenVietQrImageImportClick;

            _vietQrImageButton =
                null;
        }

        if (_vietQrDisplayModeButton is not null)
        {
            _vietQrDisplayModeButton.Click -=
                OnOpenVietQrDisplayModeClick;

            _vietQrDisplayModeButton =
                null;
        }

        base.OnClosed(
            e);
    }

    private void ConfigureVietQrNavigation()
    {
        if (_vietQrImageButton is not null ||
            _vietQrDisplayModeButton is not null)
        {
            return;
        }

        var role =
            _currentUserService.Role;

        if (role != Role.Administrator &&
            role != Role.Manager)
        {
            return;
        }

        if (SalesNavigationButton.Parent is not
                StackPanel navigationPanel)
        {
            return;
        }

        var imageButton =
            CreateNavigationButton(
                toolTip:
                    "Tải hoặc thay đổi ảnh QR nhận tiền của cửa hàng",

                icon:
                    "QR",

                title:
                    "Cấu hình VietQR");

        imageButton.Click +=
            OnOpenVietQrImageImportClick;

        var displayModeButton =
            CreateNavigationButton(
                toolTip:
                    "Chọn cách đưa mã VietQR tới khách hàng",

                icon:
                    "▣",

                title:
                    "Hiển thị VietQR");

        displayModeButton.Click +=
            OnOpenVietQrDisplayModeClick;

        var salesIndex =
            navigationPanel.Children
                .IndexOf(
                    SalesNavigationButton);

        var imageIndex =
            salesIndex >= 0
                ? salesIndex + 1
                : navigationPanel
                    .Children
                    .Count;

        navigationPanel.Children.Insert(
            imageIndex,
            imageButton);

        navigationPanel.Children.Insert(
            imageIndex + 1,
            displayModeButton);

        _vietQrImageButton =
            imageButton;

        _vietQrDisplayModeButton =
            displayModeButton;
    }

    private Button CreateNavigationButton(
        string toolTip,
        string icon,
        string title)
    {
        var button =
            new Button
            {
                ToolTip =
                    toolTip,

                Cursor =
                    Cursors.Hand,

                Content =
                    CreateNavigationContent(
                        icon,
                        title)
            };

        if (TryFindResource(
                "NavigationButtonStyle")
            is Style style)
        {
            button.Style =
                style;
        }

        return button;
    }

    private static StackPanel
        CreateNavigationContent(
            string icon,
            string title)
    {
        var panel =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    icon,

                Width =
                    32,

                FontSize =
                    12,

                FontWeight =
                    FontWeights.Bold,

                VerticalAlignment =
                    VerticalAlignment.Center
            });

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    title,

                VerticalAlignment =
                    VerticalAlignment.Center
            });

        return panel;
    }

    private async void
        OnOpenVietQrImageImportClick(
            object sender,
            RoutedEventArgs e)
    {
        SetVietQrNavigationEnabled(
            isEnabled:
                false);

        try
        {
            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var decoder =
                scope.ServiceProvider
                    .GetRequiredService<
                        IVietQrImageDecoder>();

            var payloadStore =
                scope.ServiceProvider
                    .GetRequiredService<
                        IVietQrPayloadStore>();

            var dialogService =
                new VietQrImageImportDialogService(
                    decoder,
                    payloadStore);

            await dialogService
                .ShowAsync(
                    this);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                "Không thể mở cấu hình VietQR.\n\n" +
                exception
                    .GetBaseException()
                    .Message,
                "POS Enterprise",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetVietQrNavigationEnabled(
                isEnabled:
                    true);
        }
    }

    private async void
        OnOpenVietQrDisplayModeClick(
            object sender,
            RoutedEventArgs e)
    {
        SetVietQrNavigationEnabled(
            isEnabled:
                false);

        try
        {
            var preferenceStore =
                new VietQrDisplayPreferenceStore();

            var dialogService =
                new VietQrDisplayModeDialogService(
                    preferenceStore);

            await dialogService
                .ShowConfigurationAsync(
                    this);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                "Không thể mở cấu hình cách hiển thị VietQR.\n\n" +
                exception
                    .GetBaseException()
                    .Message,
                "POS Enterprise",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetVietQrNavigationEnabled(
                isEnabled:
                    true);
        }
    }

    private void SetVietQrNavigationEnabled(
        bool isEnabled)
    {
        if (_vietQrImageButton is not null)
        {
            _vietQrImageButton.IsEnabled =
                isEnabled;
        }

        if (_vietQrDisplayModeButton is not null)
        {
            _vietQrDisplayModeButton.IsEnabled =
                isEnabled;
        }
    }
}