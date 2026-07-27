using POS.Wpf.ViewModels;

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using POS.Domain.Constants;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ thêm và sửa sản phẩm.
/// </summary>
public partial class ProductEditorWindow :
    global::System.Windows.Window
{
    private const long MaximumImageFileSize =
        5L * 1024L * 1024L;

    private const int MaximumImageDimension =
        4096;

    private const long MaximumImagePixelCount =
        16_000_000L;

    private readonly ProductEditorViewModel
        _viewModel;

    private readonly string
        _managedImageDirectory;

    private readonly string
        _originalImagePath;

    private string? _stagedImagePath;
    private bool _saveCommitted;
    private bool _cleanupCompleted;

    public ProductEditorWindow(
        ProductEditorViewModel viewModel)
    {
        _viewModel =
            viewModel ??
            throw new ArgumentNullException(
                nameof(viewModel));

        InitializeComponent();

        DataContext = _viewModel;

        _managedImageDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData),
                "POS Enterprise",
                "product-images");

        _originalImagePath =
            _viewModel.ImagePath;

        ShowInitialImage();

        _viewModel.RequestClose +=
            OnRequestClose;

        Closed += OnWindowClosed;
    }

    private void OnRequestClose(
        bool? dialogResult)
    {
        if (dialogResult == true)
        {
            _saveCommitted = true;

            if (!PathsEqual(
                    _originalImagePath,
                    _viewModel.ImagePath))
            {
                TryDeleteManagedImage(
                    _originalImagePath);
            }
        }

        DialogResult = dialogResult;
    }

    private void OnChooseProductImage(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        if (!_viewModel.CanEdit)
        {
            return;
        }

        var dialog =
            new OpenFileDialog
            {
                Title = "Chọn ảnh sản phẩm",
                Filter =
                    "Ảnh sản phẩm (*.png;*.jpg;*.jpeg;*.bmp)|" +
                    "*.png;*.jpg;*.jpeg;*.bmp",
                CheckFileExists = true,
                Multiselect = false
            };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        string? destinationPath = null;

        try
        {
            var sourceFile =
                new FileInfo(
                    dialog.FileName);

            if (sourceFile.Length <= 0)
            {
                throw new InvalidDataException(
                    "Tệp ảnh đang trống.");
            }

            if (sourceFile.Length >
                MaximumImageFileSize)
            {
                throw new InvalidDataException(
                    "Tệp ảnh vượt quá dung lượng tối đa 5 MB.");
            }

            var extension =
                Path.GetExtension(
                        sourceFile.Name)
                    .ToLowerInvariant();

            if (extension is not
                (".png" or ".jpg" or ".jpeg" or ".bmp"))
            {
                throw new InvalidDataException(
                    "Chỉ hỗ trợ ảnh PNG, JPG hoặc BMP.");
            }

            LoadValidatedBitmap(
                sourceFile.FullName);

            Directory.CreateDirectory(
                _managedImageDirectory);

            destinationPath =
                Path.Combine(
                    _managedImageDirectory,
                    Guid.NewGuid()
                        .ToString("N") +
                    extension);

            if (destinationPath.Length >
                BusinessRules.Products
                    .ImagePathMaxLength)
            {
                throw new InvalidDataException(
                    "Đường dẫn lưu ảnh vượt quá giới hạn hệ thống.");
            }

            File.Copy(
                sourceFile.FullName,
                destinationPath,
                overwrite: false);

            var preview =
                LoadValidatedBitmap(
                    destinationPath);

            TryDeleteManagedImage(
                _stagedImagePath);

            _stagedImagePath =
                destinationPath;

            _viewModel.ImagePath =
                destinationPath;

            ShowImage(
                preview,
                Path.GetFileName(
                    destinationPath));
        }
        catch (Exception exception)
        {
            if (destinationPath is not null &&
                !PathsEqual(
                    destinationPath,
                    _stagedImagePath))
            {
                TryDeleteManagedImage(
                    destinationPath);
            }

            ShowImageError(
                GetFriendlyImageError(
                    exception));
        }
    }

    private void OnClearProductImage(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        if (!_viewModel.CanEdit)
        {
            return;
        }

        TryDeleteManagedImage(
            _stagedImagePath);

        _stagedImagePath = null;
        _viewModel.ImagePath = string.Empty;

        ShowPlaceholder(
            "Chưa chọn ảnh");
    }

    private void OnWindowClosed(
        object? sender,
        EventArgs e)
    {
        CleanupStagedImage();

        _viewModel.RequestClose -=
            OnRequestClose;

        Closed -= OnWindowClosed;
    }

    private void ShowInitialImage()
    {
        if (string.IsNullOrWhiteSpace(
                _originalImagePath))
        {
            ShowPlaceholder(
                "Chưa chọn ảnh");

            return;
        }

        var fileName =
            GetDisplayFileName(
                _originalImagePath);

        try
        {
            var bitmap =
                LoadValidatedBitmap(
                    _originalImagePath);

            ShowImage(
                bitmap,
                fileName);
        }
        catch
        {
            ShowPlaceholder(
                string.IsNullOrWhiteSpace(
                    fileName)
                    ? "Không thể tải ảnh"
                    : $"{fileName} · Không thể tải ảnh");
        }
    }

    private static BitmapImage LoadValidatedBitmap(
        string path)
    {
        using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        var bitmap =
            new BitmapImage();

        bitmap.BeginInit();
        bitmap.CacheOption =
            BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();

        var pixelCount =
            checked(
                (long)bitmap.PixelWidth *
                bitmap.PixelHeight);

        if (bitmap.PixelWidth <= 0 ||
            bitmap.PixelHeight <= 0)
        {
            throw new InvalidDataException(
                "Tệp không chứa ảnh hợp lệ.");
        }

        if (bitmap.PixelWidth >
                MaximumImageDimension ||
            bitmap.PixelHeight >
                MaximumImageDimension ||
            pixelCount >
                MaximumImagePixelCount)
        {
            throw new InvalidDataException(
                "Ảnh vượt quá giới hạn kích thước 4096 px " +
                "hoặc 16 triệu pixel.");
        }

        if (bitmap.CanFreeze)
        {
            bitmap.Freeze();
        }

        return bitmap;
    }

    private void ShowImage(
        BitmapSource bitmap,
        string fileName)
    {
        ProductImagePreview.Background =
            new ImageBrush(
                bitmap)
            {
                Stretch = Stretch.UniformToFill
            };

        ProductImagePlaceholder.Visibility =
            global::System.Windows
                .Visibility.Collapsed;

        ProductImageFileNameText.Text =
            fileName;
    }

    private void ShowPlaceholder(
        string fileName)
    {
        ProductImagePreview.Background =
            FindResource(
                "SurfaceMutedBrush")
                as Brush;

        ProductImagePlaceholder.Visibility =
            global::System.Windows
                .Visibility.Visible;

        ProductImageFileNameText.Text =
            fileName;
    }

    private void CleanupStagedImage()
    {
        if (_cleanupCompleted)
        {
            return;
        }

        _cleanupCompleted = true;

        if (!_saveCommitted)
        {
            TryDeleteManagedImage(
                _stagedImagePath);
        }

        _stagedImagePath = null;
    }

    private void TryDeleteManagedImage(
        string? path)
    {
        if (!IsPathInsideManagedDirectory(
                path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Cleanup is best effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup is best effort.
        }
    }

    private bool IsPathInsideManagedDirectory(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var root =
                Path.GetFullPath(
                        _managedImageDirectory)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            var candidate =
                Path.GetFullPath(
                    path);

            return candidate.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  NotSupportedException or
                  PathTooLongException)
        {
            return false;
        }
    }

    private static bool PathsEqual(
        string? first,
        string? second)
    {
        if (string.IsNullOrWhiteSpace(first) ||
            string.IsNullOrWhiteSpace(second))
        {
            return string.Equals(
                first ?? string.Empty,
                second ?? string.Empty,
                StringComparison.Ordinal);
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(first),
                Path.GetFullPath(second),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(
                first,
                second,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string GetDisplayFileName(
        string path)
    {
        try
        {
            return Path.GetFileName(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    private void ShowImageError(
        string message)
    {
        global::System.Windows.MessageBox.Show(
            this,
            message,
            "Không thể chọn ảnh",
            global::System.Windows
                .MessageBoxButton.OK,
            global::System.Windows
                .MessageBoxImage.Warning);
    }

    private static string GetFriendlyImageError(
        Exception exception)
    {
        return exception switch
        {
            InvalidDataException =>
                exception.Message,

            UnauthorizedAccessException =>
                "POS không có quyền đọc hoặc lưu tệp ảnh này.",

            IOException =>
                "Không thể đọc hoặc lưu tệp ảnh. " +
                "Vui lòng kiểm tra tệp rồi thử lại.",

            NotSupportedException =>
                "Định dạng ảnh không được hỗ trợ.",

            _ =>
                "Tệp đã chọn không phải ảnh hợp lệ " +
                "hoặc không thể được xử lý."
        };
    }
}