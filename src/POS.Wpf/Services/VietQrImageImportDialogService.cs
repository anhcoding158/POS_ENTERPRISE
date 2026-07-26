using Microsoft.Win32;
using POS.Application.Abstractions.Payments;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace POS.Wpf.Services;

/// <summary>
/// Chọn ảnh QR nhận tiền, giải mã payload VietQR và lưu
/// an toàn bằng IVietQrPayloadStore.
///
/// Giao diện chỉ hiển thị trạng thái và ảnh xem trước.
/// Payload kỹ thuật không được đưa ra màn hình.
/// </summary>
public sealed class VietQrImageImportDialogService
{
    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    private readonly IVietQrImageDecoder _decoder;
    private readonly IVietQrPayloadStore _payloadStore;

    public VietQrImageImportDialogService(
        IVietQrImageDecoder decoder,
        IVietQrPayloadStore payloadStore)
    {
        _decoder = decoder ??
            throw new ArgumentNullException(nameof(decoder));

        _payloadStore = payloadStore ??
            throw new ArgumentNullException(nameof(payloadStore));
    }

    public Task ShowAsync(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        owner.Dispatcher.VerifyAccess();

        BuildWindow(owner).ShowDialog();

        return Task.CompletedTask;
    }

    private Window BuildWindow(Window owner)
    {
        var background =
            FindBrush(
                owner,
                "AppBackgroundBrush",
                "#F4F0EA");

        var surface =
            FindBrush(
                owner,
                "SurfaceBrush",
                "#FFFFFF");

        var surfaceMuted =
            FindBrush(
                owner,
                "SurfaceMutedBrush",
                "#F8F4EE");

        var border =
            FindBrush(
                owner,
                "BorderBrush",
                "#DDD2C6");

        var primaryText =
            FindBrush(
                owner,
                "TextPrimaryBrush",
                "#2E2925");

        var secondaryText =
            FindBrush(
                owner,
                "TextSecondaryBrush",
                "#6D655D");

        var mutedText =
            FindBrush(
                owner,
                "TextMutedBrush",
                "#958A80");

        var gold =
            FindBrush(
                owner,
                "GoldBrush",
                "#A8772F");

        var success =
            FindBrush(
                owner,
                "SuccessBrush",
                "#16834A");

        var danger =
            FindBrush(
                owner,
                "DangerBrush",
                "#B42318");

        var window =
            new Window
            {
                Owner = owner,
                Title = "POS Enterprise - Cấu hình VietQR",
                Width = 960,
                Height = 650,
                MinWidth = 860,
                MinHeight = 590,
                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,
                ResizeMode =
                    ResizeMode.CanResize,
                ShowInTaskbar = false,
                Background = background,
                FontFamily =
                    new FontFamily("Segoe UI"),
                FontSize = 12,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

        var previewImage =
            new Image
            {
                Stretch = Stretch.Uniform
            };

        RenderOptions.SetBitmapScalingMode(
            previewImage,
            BitmapScalingMode.HighQuality);

        var previewTitle =
            new TextBlock
            {
                Text = "Chưa chọn ảnh QR",
                Foreground = primaryText,
                FontSize = 15,
                FontWeight =
                    FontWeights.SemiBold,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                TextAlignment =
                    TextAlignment.Center,
                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0)
            };

        var previewPlaceholder =
            new StackPanel
            {
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        previewPlaceholder.Children.Add(
            new TextBlock
            {
                Text = "QR",
                Foreground = gold,
                FontFamily =
                    new FontFamily("Georgia"),
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment =
                    HorizontalAlignment.Center
            });

        previewPlaceholder.Children.Add(
            previewTitle);

        previewPlaceholder.Children.Add(
            new TextBlock
            {
                Text =
                    "Chọn ảnh QR nhận tiền được tạo " +
                    "từ ứng dụng ngân hàng.",
                Foreground = secondaryText,
                FontSize = 10.5,
                TextAlignment =
                    TextAlignment.Center,
                TextWrapping =
                    TextWrapping.Wrap,
                Margin =
                    new Thickness(
                        20,
                        6,
                        20,
                        0)
            });

        var configurationTitle =
            new TextBlock
            {
                Foreground = primaryText,
                FontFamily =
                    new FontFamily("Georgia"),
                FontSize = 18,
                FontWeight =
                    FontWeights.SemiBold,
                TextWrapping =
                    TextWrapping.Wrap
            };

        var configurationDescription =
            new TextBlock
            {
                Foreground = secondaryText,
                FontSize = 10.5,
                TextWrapping =
                    TextWrapping.Wrap,
                LineHeight = 17,
                Margin =
                    new Thickness(
                        0,
                        7,
                        0,
                        0)
            };

        var configurationBadge =
            new TextBlock
            {
                FontSize = 10.5,
                FontWeight =
                    FontWeights.Bold,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        var fileNameText =
            new TextBlock
            {
                Text = "Chưa chọn file ảnh mới.",
                Foreground = primaryText,
                FontSize = 12.5,
                FontWeight =
                    FontWeights.SemiBold,
                TextWrapping =
                    TextWrapping.Wrap
            };

        var fileInfoText =
            new TextBlock
            {
                Text =
                    "Hỗ trợ PNG, JPG, JPEG và BMP.",
                Foreground = mutedText,
                FontSize = 10,
                TextWrapping =
                    TextWrapping.Wrap,
                Margin =
                    new Thickness(
                        0,
                        5,
                        0,
                        0)
            };

        var statusText =
            new TextBlock
            {
                FontSize = 10.5,
                TextWrapping =
                    TextWrapping.Wrap,
                LineHeight = 17,
                Margin =
                    new Thickness(
                        0,
                        10,
                        0,
                        0)
            };

        var chooseButton =
            CreateButton(
                owner,
                "CHỌN ẢNH QR",
                "PrimaryButtonStyle",
                145);

        var saveButton =
            CreateButton(
                owner,
                "LƯU QR CHO CỬA HÀNG",
                "PrimaryButtonStyle",
                190);

        var deleteButton =
            CreateButton(
                owner,
                "XÓA CẤU HÌNH",
                "SecondaryButtonStyle",
                140);

        var closeButton =
            CreateButton(
                owner,
                "ĐÓNG",
                "SecondaryButtonStyle",
                110);

        string? pendingPayload = null;

        void ApplyConfiguredState(
            bool isConfigured)
        {
            configurationBadge.Text =
                isConfigured
                    ? "ĐÃ CẤU HÌNH"
                    : "CHƯA CẤU HÌNH";

            configurationBadge.Foreground =
                isConfigured
                    ? success
                    : danger;

            configurationTitle.Text =
                isConfigured
                    ? "QR nhận tiền đang sẵn sàng"
                    : "Chưa có QR nhận tiền";

            configurationDescription.Text =
                isConfigured
                    ? "Quầy bán hàng đang sử dụng QR đã lưu " +
                      "để tạo mã theo đúng số tiền của từng đơn. " +
                      "Chọn ảnh mới khi cần thay tài khoản nhận tiền."
                    : "Tải ảnh QR nhận tiền từ ứng dụng ngân hàng. " +
                      "Hệ thống sẽ đọc và lưu cấu hình để dùng " +
                      "cho từng giao dịch.";

            statusText.Text =
                isConfigured
                    ? "Máy này đã có QR ngân hàng được lưu an toàn."
                    : "Hãy chọn ảnh QR nhận tiền của cửa hàng.";

            statusText.Foreground =
                isConfigured
                    ? success
                    : secondaryText;

            deleteButton.IsEnabled =
                isConfigured;
        }

        ApplyConfiguredState(
            _payloadStore.IsConfigured);

        saveButton.IsEnabled = false;

        chooseButton.Click +=
            async (_, _) =>
            {
                var dialog =
                    new OpenFileDialog
                    {
                        Title =
                            "Chọn ảnh QR nhận tiền của cửa hàng",

                        Filter =
                            "Ảnh QR (*.png;*.jpg;*.jpeg;*.bmp)|" +
                            "*.png;*.jpg;*.jpeg;*.bmp|" +
                            "Tất cả file (*.*)|*.*",

                        Multiselect = false,
                        CheckFileExists = true,
                        CheckPathExists = true
                    };

                if (dialog.ShowDialog(window) != true)
                {
                    return;
                }

                chooseButton.IsEnabled = false;
                saveButton.IsEnabled = false;
                pendingPayload = null;

                statusText.Foreground =
                    secondaryText;

                statusText.Text =
                    "Đang đọc và kiểm tra ảnh QR...";

                try
                {
                    var bytes =
                        await File.ReadAllBytesAsync(
                            dialog.FileName);

                    previewImage.Source =
                        CreateBitmapImage(bytes);

                    previewPlaceholder.Visibility =
                        Visibility.Collapsed;

                    var fileInfo =
                        new FileInfo(
                            dialog.FileName);

                    fileNameText.Text =
                        Path.GetFileName(
                            dialog.FileName);

                    fileInfoText.Text =
                        $"{FormatFileSize(fileInfo.Length)} • " +
                        $"{fileInfo.Extension.ToUpperInvariant()}";

                    var result =
                        _decoder.DecodePayload(
                            bytes);

                    if (result.IsFailure)
                    {
                        statusText.Foreground =
                            danger;

                        statusText.Text =
                            result.Error.Message;

                        return;
                    }

                    pendingPayload =
                        result.Value;

                    saveButton.IsEnabled =
                        true;

                    statusText.Foreground =
                        success;

                    statusText.Text =
                        "Đã đọc QR thành công. " +
                        "Kiểm tra đúng ảnh rồi bấm " +
                        "Lưu QR cho cửa hàng.";
                }
                catch (Exception exception)
                {
                    statusText.Foreground =
                        danger;

                    statusText.Text =
                        "Không thể đọc ảnh QR. " +
                        exception
                            .GetBaseException()
                            .Message;
                }
                finally
                {
                    chooseButton.IsEnabled =
                        true;
                }
            };

        saveButton.Click +=
            (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(
                        pendingPayload))
                {
                    return;
                }

                var confirmation =
                    MessageBox.Show(
                        window,
                        "Lưu ảnh QR này làm tài khoản nhận tiền " +
                        "của cửa hàng?\n\n" +
                        "Cấu hình QR cũ trên máy sẽ được thay thế.",
                        "Xác nhận lưu VietQR",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.No);

                if (confirmation !=
                    MessageBoxResult.Yes)
                {
                    return;
                }

                var result =
                    _payloadStore.Save(
                        pendingPayload);

                if (result.IsFailure)
                {
                    statusText.Foreground =
                        danger;

                    statusText.Text =
                        result.Error.Message;

                    return;
                }

                pendingPayload = null;
                saveButton.IsEnabled = false;

                ApplyConfiguredState(
                    isConfigured: true);

                statusText.Text =
                    "Đã lưu QR thành công. " +
                    "Quầy bán hàng có thể dùng VietQR ngay.";
            };

        deleteButton.Click +=
            (_, _) =>
            {
                var confirmation =
                    MessageBox.Show(
                        window,
                        "Xóa cấu hình VietQR trên máy này?\n\n" +
                        "Quầy bán hàng sẽ không thể dùng VietQR " +
                        "cho đến khi tải ảnh mới.",
                        "Xóa cấu hình VietQR",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.No);

                if (confirmation !=
                    MessageBoxResult.Yes)
                {
                    return;
                }

                var result =
                    _payloadStore.Delete();

                if (result.IsFailure)
                {
                    statusText.Foreground =
                        danger;

                    statusText.Text =
                        result.Error.Message;

                    return;
                }

                pendingPayload = null;
                previewImage.Source = null;

                previewPlaceholder.Visibility =
                    Visibility.Visible;

                previewTitle.Text =
                    "Chưa chọn ảnh QR";

                fileNameText.Text =
                    "Chưa chọn file ảnh mới.";

                fileInfoText.Text =
                    "Hỗ trợ PNG, JPG, JPEG và BMP.";

                saveButton.IsEnabled = false;

                ApplyConfiguredState(
                    isConfigured: false);

                statusText.Text =
                    "Đã xóa cấu hình VietQR trên máy.";
            };

        closeButton.Click +=
            (_, _) =>
                window.Close();

        var root =
            new Grid();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition());

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        root.Children.Add(
            BuildHeader(
                configurationBadge,
                primaryText,
                secondaryText,
                gold));

        var content =
            new Grid
            {
                Margin =
                    new Thickness(
                        22,
                        0,
                        22,
                        18)
            };

        content.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(400)
            });

        content.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(18)
            });

        content.ColumnDefinitions.Add(
            new ColumnDefinition());

        var previewGrid =
            new Grid();

        previewGrid.Children.Add(
            previewImage);

        previewGrid.Children.Add(
            previewPlaceholder);

        var previewCard =
            new Border
            {
                Background = surface,
                BorderBrush = border,
                BorderThickness =
                    new Thickness(1),
                CornerRadius =
                    new CornerRadius(16),
                Padding =
                    new Thickness(18),
                Child = previewGrid
            };

        content.Children.Add(
            previewCard);

        var filePanel =
            new StackPanel();

        filePanel.Children.Add(
            fileNameText);

        filePanel.Children.Add(
            fileInfoText);

        var fileCard =
            new Border
            {
                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0),

                Padding =
                    new Thickness(14),

                Background = surfaceMuted,
                BorderBrush = border,
                BorderThickness =
                    new Thickness(1),
                CornerRadius =
                    new CornerRadius(11),
                Child = filePanel
            };

        var guidePanel =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0)
            };

        guidePanel.Children.Add(
            CreateGuideRow(
                "1",
                "Mở ứng dụng ngân hàng và tải ảnh " +
                "QR nhận tiền rõ nét.",
                gold,
                primaryText));

        guidePanel.Children.Add(
            CreateGuideRow(
                "2",
                "Chọn ảnh, kiểm tra hình xem trước " +
                "rồi lưu cho cửa hàng.",
                gold,
                primaryText));

        guidePanel.Children.Add(
            CreateGuideRow(
                "3",
                "Mỗi đơn sẽ tự tạo QR mới với đúng " +
                "số tiền cần thanh toán.",
                gold,
                primaryText));

        var actionPanel =
            new WrapPanel
            {
                Margin =
                    new Thickness(
                        0,
                        20,
                        0,
                        0)
            };

        actionPanel.Children.Add(
            chooseButton);

        actionPanel.Children.Add(
            saveButton);

        actionPanel.Children.Add(
            deleteButton);

        var informationContent =
            new StackPanel();

        informationContent.Children.Add(
            configurationTitle);

        informationContent.Children.Add(
            configurationDescription);

        informationContent.Children.Add(
            fileCard);

        informationContent.Children.Add(
            guidePanel);

        informationContent.Children.Add(
            actionPanel);

        informationContent.Children.Add(
            statusText);

        var informationCard =
            new Border
            {
                Background = surface,
                BorderBrush = border,
                BorderThickness =
                    new Thickness(1),
                CornerRadius =
                    new CornerRadius(16),
                Padding =
                    new Thickness(22),
                Child =
                    informationContent
            };

        Grid.SetColumn(
            informationCard,
            2);

        content.Children.Add(
            informationCard);

        Grid.SetRow(
            content,
            1);

        root.Children.Add(
            content);

        var footer =
            new Grid
            {
                Margin =
                    new Thickness(
                        24,
                        12,
                        24,
                        14)
            };

        footer.ColumnDefinitions.Add(
            new ColumnDefinition());

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        footer.Children.Add(
            new TextBlock
            {
                Text =
                    "Payload được bảo vệ bằng Windows DPAPI " +
                    "trên tài khoản Windows hiện tại.",
                Foreground = secondaryText,
                FontSize = 10,
                VerticalAlignment =
                    VerticalAlignment.Center,
                TextWrapping =
                    TextWrapping.Wrap
            });

        Grid.SetColumn(
            closeButton,
            1);

        footer.Children.Add(
            closeButton);

        Grid.SetRow(
            footer,
            2);

        root.Children.Add(
            footer);

        window.Content = root;

        return window;
    }

    private static Grid BuildHeader(
        TextBlock configurationBadge,
        Brush primaryText,
        Brush secondaryText,
        Brush gold)
    {
        var titlePanel =
            new StackPanel();

        titlePanel.Children.Add(
            new TextBlock
            {
                Text = "CẤU HÌNH VIETQR",
                Foreground = primaryText,
                FontFamily =
                    new FontFamily("Georgia"),
                FontSize = 24,
                FontWeight =
                    FontWeights.SemiBold
            });

        titlePanel.Children.Add(
            new TextBlock
            {
                Text =
                    "Tải ảnh QR nhận tiền từ ứng dụng ngân hàng. " +
                    "Không cần nhập thông tin tài khoản thủ công.",
                Foreground = secondaryText,
                FontSize = 10.5,
                Margin =
                    new Thickness(
                        0,
                        5,
                        0,
                        0),
                TextWrapping =
                    TextWrapping.Wrap
            });

        var grid =
            new Grid
            {
                Margin =
                    new Thickness(
                        24,
                        18,
                        24,
                        16)
            };

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        grid.Children.Add(
            titlePanel);

        var badgeBorder =
            new Border
            {
                Padding =
                    new Thickness(
                        12,
                        8,
                        12,
                        8),
                BorderBrush = gold,
                BorderThickness =
                    new Thickness(1),
                CornerRadius =
                    new CornerRadius(10),
                Child = configurationBadge
            };

        Grid.SetColumn(
            badgeBorder,
            1);

        grid.Children.Add(
            badgeBorder);

        return grid;
    }

    private static Grid CreateGuideRow(
        string number,
        string text,
        Brush gold,
        Brush primaryText)
    {
        var grid =
            new Grid
            {
                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        11)
            };

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        grid.Children.Add(
            new Border
            {
                Width = 30,
                Height = 30,
                Background =
                    new SolidColorBrush(
                        Color.FromRgb(
                            248,
                            240,
                            219)),
                BorderBrush = gold,
                BorderThickness =
                    new Thickness(1),
                CornerRadius =
                    new CornerRadius(15),
                Child =
                    new TextBlock
                    {
                        Text = number,
                        Foreground = gold,
                        FontWeight =
                            FontWeights.Bold,
                        HorizontalAlignment =
                            HorizontalAlignment.Center,
                        VerticalAlignment =
                            VerticalAlignment.Center
                    }
            });

        var textBlock =
            new TextBlock
            {
                Text = text,
                Foreground = primaryText,
                FontSize = 10.5,
                TextWrapping =
                    TextWrapping.Wrap,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Margin =
                    new Thickness(
                        11,
                        0,
                        0,
                        0)
            };

        Grid.SetColumn(
            textBlock,
            1);

        grid.Children.Add(
            textBlock);

        return grid;
    }

    private static Button CreateButton(
        FrameworkElement owner,
        string text,
        string styleKey,
        double minWidth)
    {
        var button =
            new Button
            {
                Content = text,
                MinWidth = minWidth,
                Height = 42,
                Padding =
                    new Thickness(
                        16,
                        0,
                        16,
                        0),
                Margin =
                    new Thickness(
                        0,
                        0,
                        9,
                        7),
                Cursor = Cursors.Hand
            };

        if (owner.TryFindResource(
                styleKey)
            is Style style)
        {
            button.Style = style;
        }

        return button;
    }

    private static BitmapImage CreateBitmapImage(
        byte[] bytes)
    {
        using var stream =
            new MemoryStream(
                bytes,
                writable: false);

        var bitmap =
            new BitmapImage();

        bitmap.BeginInit();

        bitmap.CacheOption =
            BitmapCacheOption.OnLoad;

        bitmap.StreamSource =
            stream;

        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }

    private static string FormatFileSize(
        long bytes)
    {
        if (bytes < 1024)
        {
            return
                $"{bytes.ToString(
                    "N0",
                    VietnameseCulture)} byte";
        }

        var kilobytes =
            bytes / 1024d;

        if (kilobytes < 1024)
        {
            return
                $"{kilobytes.ToString(
                    "N1",
                    VietnameseCulture)} KB";
        }

        return
            $"{(kilobytes / 1024d).ToString(
                "N2",
                VietnameseCulture)} MB";
    }

    private static Brush FindBrush(
        FrameworkElement owner,
        string resourceKey,
        string fallbackHex)
    {
        if (owner.TryFindResource(
                resourceKey)
            is Brush brush)
        {
            return brush;
        }

        var color =
            (Color)
            ColorConverter.ConvertFromString(
                fallbackHex);

        var fallback =
            new SolidColorBrush(
                color);

        fallback.Freeze();

        return fallback;
    }
}