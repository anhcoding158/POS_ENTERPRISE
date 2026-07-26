using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace POS.Wpf.Services;

/// <summary>
/// Hiển thị màn chọn cách đưa VietQR tới khách hàng.
///
/// Cùng một giao diện được dùng cho:
/// - cấu hình mặc định của máy POS;
/// - lựa chọn tạm thời khi chế độ là Hỏi mỗi giao dịch.
/// </summary>
public sealed class VietQrDisplayModeDialogService
{
    private readonly VietQrDisplayPreferenceStore
        _preferenceStore;

    public VietQrDisplayModeDialogService(
        VietQrDisplayPreferenceStore preferenceStore)
    {
        _preferenceStore =
            preferenceStore ??
            throw new ArgumentNullException(
                nameof(preferenceStore));
    }

    public Task ShowConfigurationAsync(
        Window owner)
    {
        ArgumentNullException.ThrowIfNull(
            owner);

        owner.Dispatcher.VerifyAccess();

        _ =
            ShowSelectionWindow(
                owner,
                title:
                    "CÁCH HIỂN THỊ VIETQR",

                subtitle:
                    "Chọn cách mặc định để khách nhận mã QR " +
                    "trên máy POS này.",

                initialMode:
                    _preferenceStore.Load(),

                includeAskEveryTime:
                    true,

                saveAsDefault:
                    true);

        return Task.CompletedTask;
    }

    public VietQrDisplayMode?
        ChooseForCurrentPayment(
            Window? owner)
    {
        owner?.Dispatcher.VerifyAccess();

        var initialMode =
            _preferenceStore.Load();

        if (initialMode ==
            VietQrDisplayMode.AskEveryTime)
        {
            initialMode =
                VietQrDisplayPreferenceStore
                    .GetDefaultMode();
        }

        return ShowSelectionWindow(
            owner,
            title:
                "ĐƯA MÃ QR TỚI KHÁCH",

            subtitle:
                "Chọn cách dùng cho giao dịch hiện tại. " +
                "Lựa chọn này không thay đổi cấu hình mặc định.",

            initialMode:
                initialMode,

            includeAskEveryTime:
                false,

            saveAsDefault:
                false);
    }

    private VietQrDisplayMode?
        ShowSelectionWindow(
            Window? owner,
            string title,
            string subtitle,
            VietQrDisplayMode initialMode,
            bool includeAskEveryTime,
            bool saveAsDefault)
    {
        if (!Enum.IsDefined(
                initialMode))
        {
            initialMode =
                VietQrDisplayPreferenceStore
                    .GetDefaultMode();
        }

        var background =
            FindBrush(
                owner,
                "AppBackgroundBrush",
                "#F7F3EC");

        var surface =
            FindBrush(
                owner,
                "SurfaceBrush",
                "#FFFEFB");

        var surfaceMuted =
            FindBrush(
                owner,
                "SurfaceMutedBrush",
                "#FBF8F2");

        var border =
            FindBrush(
                owner,
                "BorderBrush",
                "#E9E0D4");

        var borderStrong =
            FindBrush(
                owner,
                "BorderStrongBrush",
                "#D9CDBD");

        var primaryText =
            FindBrush(
                owner,
                "TextPrimaryBrush",
                "#2C221D");

        var secondaryText =
            FindBrush(
                owner,
                "TextSecondaryBrush",
                "#71655E");

        var mutedText =
            FindBrush(
                owner,
                "TextMutedBrush",
                "#9A8E85");

        var accent =
            FindBrush(
                owner,
                "AccentBrush",
                "#88151B");

        var accentSoft =
            FindBrush(
                owner,
                "AccentSoftBrush",
                "#F7E9E9");

        var gold =
            FindBrush(
                owner,
                "GoldBrush",
                "#C7973E");

        var goldSoft =
            FindBrush(
                owner,
                "GoldSoftBrush",
                "#F8EFDE");

        var success =
            FindBrush(
                owner,
                "SuccessBrush",
                "#34774B");

        var window =
            new Window
            {
                Owner =
                    owner,

                Title =
                    "POS Enterprise - " +
                    VietQrDisplayPreferenceStore
                        .GetDisplayName(
                            initialMode),

                Width =
                    980,

                Height =
                    690,

                MinWidth =
                    860,

                MinHeight =
                    620,

                WindowStartupLocation =
                    owner is null
                        ? WindowStartupLocation
                            .CenterScreen
                        : WindowStartupLocation
                            .CenterOwner,

                ResizeMode =
                    ResizeMode.CanResize,

                ShowInTaskbar =
                    false,

                Background =
                    background,

                FontFamily =
                    new FontFamily(
                        "Segoe UI"),

                FontSize =
                    12,

                UseLayoutRounding =
                    true,

                SnapsToDevicePixels =
                    true
            };

        var selectedMode =
            initialMode;

        var optionVisuals =
            new List<OptionVisual>();

        var currentModeText =
            new TextBlock
            {
                Foreground =
                    secondaryText,

                FontSize =
                    10.5,

                FontWeight =
                    FontWeights.SemiBold,

                TextWrapping =
                    TextWrapping.Wrap
            };

        var statusText =
            new TextBlock
            {
                Foreground =
                    secondaryText,

                FontSize =
                    10.5,

                TextWrapping =
                    TextWrapping.Wrap,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        var saveButton =
            CreateButton(
                owner,
                saveAsDefault
                    ? "LƯU CẤU HÌNH"
                    : "TIẾP TỤC",

                "PrimaryButtonStyle",
                minWidth:
                    saveAsDefault
                        ? 170
                        : 130);

        var cancelButton =
            CreateButton(
                owner,
                "HỦY",
                "SecondaryButtonStyle",
                minWidth:
                    100);

        var options =
            includeAskEveryTime
                ? new[]
                {
                    VietQrDisplayMode
                        .CustomerDisplay,

                    VietQrDisplayMode
                        .CashierDisplay,

                    VietQrDisplayMode
                        .PrintSlip,

                    VietQrDisplayMode
                        .AskEveryTime
                }
                : new[]
                {
                    VietQrDisplayMode
                        .CustomerDisplay,

                    VietQrDisplayMode
                        .CashierDisplay,

                    VietQrDisplayMode
                        .PrintSlip
                };

        void RefreshSelection()
        {
            foreach (var visual in
                     optionVisuals)
            {
                var isSelected =
                    visual.Mode ==
                    selectedMode;

                visual.Card.Background =
                    isSelected
                        ? goldSoft
                        : surface;

                visual.Card.BorderBrush =
                    isSelected
                        ? gold
                        : borderStrong;

                visual.Card.BorderThickness =
                    isSelected
                        ? new Thickness(2)
                        : new Thickness(1);

                visual.Indicator.Background =
                    isSelected
                        ? accentSoft
                        : surfaceMuted;

                visual.Indicator.BorderBrush =
                    isSelected
                        ? accent
                        : borderStrong;

                visual.IndicatorText.Text =
                    isSelected
                        ? "✓"
                        : string.Empty;

                visual.IndicatorText.Foreground =
                    isSelected
                        ? accent
                        : mutedText;
            }

            currentModeText.Text =
                $"Đang chọn: " +
                $"{VietQrDisplayPreferenceStore.GetDisplayName(selectedMode)}";

            window.Title =
                "POS Enterprise - " +
                VietQrDisplayPreferenceStore
                    .GetDisplayName(
                        selectedMode);
        }

        var optionGrid =
            new UniformGrid
            {
                Columns =
                    includeAskEveryTime
                        ? 2
                        : 3,

                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0)
            };

        foreach (var mode in
                 options)
        {
            var visual =
                CreateOptionCard(
                    mode,
                    surface,
                    surfaceMuted,
                    borderStrong,
                    primaryText,
                    secondaryText,
                    mutedText,
                    gold);

            visual.Card.MouseLeftButtonUp +=
                (_, _) =>
                {
                    selectedMode =
                        mode;

                    RefreshSelection();
                };

            visual.Card.MouseEnter +=
                (_, _) =>
                {
                    if (selectedMode !=
                        mode)
                    {
                        visual.Card.BorderBrush =
                            gold;
                    }
                };

            visual.Card.MouseLeave +=
                (_, _) =>
                    RefreshSelection();

            optionVisuals.Add(
                visual);

            optionGrid.Children.Add(
                visual.Card);
        }

        saveButton.Click +=
            (_, _) =>
            {
                try
                {
                    if (saveAsDefault)
                    {
                        _preferenceStore.Save(
                            selectedMode);

                        statusText.Foreground =
                            success;

                        statusText.Text =
                            "Đã lưu cách hiển thị VietQR " +
                            "cho máy POS này.";
                    }

                    window.DialogResult =
                        true;
                }
                catch (Exception exception)
                {
                    statusText.Foreground =
                        accent;

                    statusText.Text =
                        "Không thể lưu cấu hình: " +
                        exception
                            .GetBaseException()
                            .Message;
                }
            };

        cancelButton.Click +=
            (_, _) =>
            {
                window.DialogResult =
                    false;
            };

        var root =
            new Grid();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(96)
            });

        root.RowDefinitions.Add(
            new RowDefinition());

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(82)
            });

        root.Children.Add(
            BuildHeader(
                title,
                subtitle,
                gold));

        var contentPanel =
            new StackPanel();

        contentPanel.Children.Add(
            new TextBlock
            {
                Text =
                    saveAsDefault
                        ? "CẤU HÌNH TRÊN MÁY NÀY"
                        : "GIAO DỊCH HIỆN TẠI",

                Foreground =
                    gold,

                FontSize =
                    10,

                FontWeight =
                    FontWeights.Bold
            });

        contentPanel.Children.Add(
            new TextBlock
            {
                Text =
                    saveAsDefault
                        ? "Mỗi máy bán hàng có thể dùng một cách " +
                          "hiển thị khác nhau, phù hợp với phần cứng " +
                          "của từng quầy."
                        : "Thu ngân vẫn phải kiểm tra tiền thực tế " +
                          "đã vào tài khoản trước khi xác nhận.",

                Foreground =
                    primaryText,

                FontFamily =
                    new FontFamily(
                        "Georgia"),

                FontSize =
                    20,

                FontWeight =
                    FontWeights.SemiBold,

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(
                        0,
                        7,
                        0,
                        0)
            });

        contentPanel.Children.Add(
            optionGrid);

        contentPanel.Children.Add(
            new Border
            {
                Margin =
                    new Thickness(
                        0,
                        14,
                        0,
                        0),

                Padding =
                    new Thickness(
                        14,
                        11,
                        14,
                        11),

                Background =
                    surfaceMuted,

                BorderBrush =
                    border,

                BorderThickness =
                    new Thickness(1),

                CornerRadius =
                    new CornerRadius(10),

                Child =
                    currentModeText
            });

        var scrollViewer =
            new ScrollViewer
            {
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,

                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,

                Padding =
                    new Thickness(
                        22,
                        18,
                        22,
                        18),

                Content =
                    contentPanel
            };

        Grid.SetRow(
            scrollViewer,
            1);

        root.Children.Add(
            scrollViewer);

        var footer =
            new Grid
            {
                Margin =
                    new Thickness(
                        22,
                        12,
                        22,
                        14)
            };

        footer.ColumnDefinitions.Add(
            new ColumnDefinition());

        footer.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        statusText.Text =
            saveAsDefault
                ? "Lựa chọn được lưu riêng trên máy POS này."
                : "Lựa chọn chỉ áp dụng cho giao dịch đang mở.";

        footer.Children.Add(
            statusText);

        var buttons =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal
            };

        cancelButton.Margin =
            new Thickness(
                0,
                0,
                10,
                0);

        buttons.Children.Add(
            cancelButton);

        buttons.Children.Add(
            saveButton);

        Grid.SetColumn(
            buttons,
            1);

        footer.Children.Add(
            buttons);

        Grid.SetRow(
            footer,
            2);

        root.Children.Add(
            footer);

        window.Content =
            root;

        RefreshSelection();

        var dialogResult =
            window.ShowDialog();

        return dialogResult ==
               true
            ? selectedMode
            : null;
    }

    private static Border BuildHeader(
        string title,
        string subtitle,
        Brush gold)
    {
        var titlePanel =
            new StackPanel
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        titlePanel.Children.Add(
            new TextBlock
            {
                Text =
                    title,

                Foreground =
                    gold,

                FontFamily =
                    new FontFamily(
                        "Georgia"),

                FontSize =
                    24,

                FontWeight =
                    FontWeights.SemiBold
            });

        titlePanel.Children.Add(
            new TextBlock
            {
                Text =
                    subtitle,

                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            187,
                            175,
                            162)),

                FontSize =
                    10.5,

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(
                        0,
                        5,
                        0,
                        0)
            });

        return new Border
        {
            Background =
                new SolidColorBrush(
                    Color.FromRgb(
                        27,
                        24,
                        22)),

            BorderBrush =
                new SolidColorBrush(
                    Color.FromRgb(
                        116,
                        86,
                        48)),

            BorderThickness =
                new Thickness(
                    0,
                    0,
                    0,
                    1),

            Padding =
                new Thickness(
                    24,
                    0,
                    24,
                    0),

            Child =
                titlePanel
        };
    }

    private static OptionVisual
        CreateOptionCard(
            VietQrDisplayMode mode,
            Brush surface,
            Brush surfaceMuted,
            Brush borderStrong,
            Brush primaryText,
            Brush secondaryText,
            Brush mutedText,
            Brush gold)
    {
        var indicatorText =
            new TextBlock
            {
                Foreground =
                    mutedText,

                FontSize =
                    16,

                FontWeight =
                    FontWeights.Bold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        var indicator =
            new Border
            {
                Width =
                    34,

                Height =
                    34,

                Background =
                    surfaceMuted,

                BorderBrush =
                    borderStrong,

                BorderThickness =
                    new Thickness(1),

                CornerRadius =
                    new CornerRadius(17),

                Child =
                    indicatorText
            };

        var icon =
            new TextBlock
            {
                Text =
                    GetModeIcon(
                        mode),

                FontFamily =
                    new FontFamily(
                        "Segoe MDL2 Assets"),

                Foreground =
                    gold,

                FontSize =
                    21,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        var titleText =
            new TextBlock
            {
                Text =
                    VietQrDisplayPreferenceStore
                        .GetDisplayName(
                            mode),

                Foreground =
                    primaryText,

                FontFamily =
                    new FontFamily(
                        "Georgia"),

                FontSize =
                    16,

                FontWeight =
                    FontWeights.SemiBold,

                TextWrapping =
                    TextWrapping.Wrap
            };

        var descriptionText =
            new TextBlock
            {
                Text =
                    VietQrDisplayPreferenceStore
                        .GetDescription(
                            mode),

                Foreground =
                    secondaryText,

                FontSize =
                    10.5,

                TextWrapping =
                    TextWrapping.Wrap,

                LineHeight =
                    16,

                Margin =
                    new Thickness(
                        0,
                        6,
                        0,
                        0)
            };

        var heading =
            new Grid();

        heading.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(36)
            });

        heading.ColumnDefinitions.Add(
            new ColumnDefinition());

        heading.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        heading.Children.Add(
            icon);

        Grid.SetColumn(
            titleText,
            1);

        heading.Children.Add(
            titleText);

        Grid.SetColumn(
            indicator,
            2);

        heading.Children.Add(
            indicator);

        var content =
            new StackPanel();

        content.Children.Add(
            heading);

        content.Children.Add(
            descriptionText);

        var card =
            new Border
            {
                MinHeight =
                    150,

                Margin =
                    new Thickness(
                        0,
                        0,
                        12,
                        12),

                Padding =
                    new Thickness(
                        18),

                Background =
                    surface,

                BorderBrush =
                    borderStrong,

                BorderThickness =
                    new Thickness(1),

                CornerRadius =
                    new CornerRadius(14),

                Cursor =
                    Cursors.Hand,

                Child =
                    content
            };

        return new OptionVisual(
            mode,
            card,
            indicator,
            indicatorText);
    }

    private static string GetModeIcon(
        VietQrDisplayMode mode)
    {
        return mode switch
        {
            VietQrDisplayMode.CustomerDisplay =>
                "\uE7F4",

            VietQrDisplayMode.CashierDisplay =>
                "\uE7F8",

            VietQrDisplayMode.PrintSlip =>
                "\uE749",

            VietQrDisplayMode.AskEveryTime =>
                "\uE897",

            _ =>
                "\uE8A1"
        };
    }

    private static Button CreateButton(
        FrameworkElement? owner,
        string text,
        string styleKey,
        double minWidth)
    {
        var button =
            new Button
            {
                Content =
                    text,

                MinWidth =
                    minWidth,

                Height =
                    44,

                Padding =
                    new Thickness(
                        16,
                        0,
                        16,
                        0),

                Cursor =
                    Cursors.Hand
            };

        if (owner?
                .TryFindResource(
                    styleKey)
            is Style style)
        {
            button.Style =
                style;
        }

        return button;
    }

    private static Brush FindBrush(
        FrameworkElement? owner,
        string resourceKey,
        string fallbackHex)
    {
        if (owner?
                .TryFindResource(
                    resourceKey)
            is Brush brush)
        {
            return brush;
        }

        if (global::System.Windows.Application.Current?
        .TryFindResource(
            resourceKey)
            is Brush applicationBrush)
        {
            return applicationBrush;
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

    private sealed record OptionVisual(
        VietQrDisplayMode Mode,
        Border Card,
        Border Indicator,
        TextBlock IndicatorText);
}