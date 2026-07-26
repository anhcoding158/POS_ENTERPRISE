using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace POS.Wpf.Services;

/// <summary>
/// Cách đưa mã VietQR tới khách hàng trong lúc thanh toán.
/// </summary>
public enum VietQrDisplayMode
{
    /// <summary>
    /// Ưu tiên mở màn hình khách hàng trên màn hình thứ hai.
    /// Nếu máy chỉ có một màn hình, tiếp tục dùng màn thu ngân.
    /// </summary>
    CustomerDisplay = 1,

    /// <summary>
    /// Chỉ hiển thị cửa sổ VietQR trên màn hình thu ngân.
    /// </summary>
    CashierDisplay = 2,

    /// <summary>
    /// Tự mở hộp thoại in phiếu QR khi bắt đầu thanh toán.
    /// Thu ngân vẫn chọn hoặc xác nhận máy in trong Windows.
    /// </summary>
    PrintSlip = 3,

    /// <summary>
    /// Hỏi thu ngân cách đưa QR tới khách trong từng giao dịch.
    /// </summary>
    AskEveryTime = 4
}

/// <summary>
/// Lưu lựa chọn cách hiển thị VietQR riêng trên từng máy POS.
///
/// File không chứa payload QR, tài khoản ngân hàng,
/// mã giao dịch hoặc dữ liệu khách hàng.
/// </summary>
public sealed class VietQrDisplayPreferenceStore
{
    private const int CurrentVersion =
        1;

    private const string ApplicationFolderName =
        "POS Enterprise";

    private const string SettingsFileName =
        "vietqr-display-settings.json";

    private static readonly Encoding Utf8WithoutBom =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private static readonly object SyncRoot =
        new();

    private readonly string _filePath;

    public VietQrDisplayPreferenceStore()
        : this(
            CreateDefaultFilePath())
    {
    }

    /// <summary>
    /// Constructor có đường dẫn riêng phục vụ kiểm thử.
    /// Production sử dụng constructor không tham số.
    /// </summary>
    public VietQrDisplayPreferenceStore(
        string filePath)
    {
        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "Đường dẫn file cấu hình không được để trống.",
                nameof(filePath));
        }

        _filePath =
            Path.GetFullPath(
                filePath.Trim());
    }

    public string FilePath =>
        _filePath;

    public VietQrDisplayMode Load()
    {
        lock (SyncRoot)
        {
            if (!File.Exists(
                    _filePath))
            {
                return GetDefaultMode();
            }

            try
            {
                var json =
                    File.ReadAllText(
                        _filePath,
                        Utf8WithoutBom);

                var settings =
                    JsonSerializer.Deserialize<
                        VietQrDisplayPreferenceFile>(
                            json,
                            JsonOptions);

                if (settings is null ||
                    settings.Version !=
                    CurrentVersion ||
                    !Enum.IsDefined(
                        settings.Mode))
                {
                    return GetDefaultMode();
                }

                return settings.Mode;
            }
            catch (Exception exception)
                when (exception is
                          IOException or
                          UnauthorizedAccessException or
                          JsonException or
                          NotSupportedException)
            {
                /*
                 * File mất, hỏng hoặc do phiên bản khác tạo
                 * không được làm ứng dụng POS ngừng hoạt động.
                 */
                return GetDefaultMode();
            }
        }
    }

    public void Save(
        VietQrDisplayMode mode)
    {
        if (!Enum.IsDefined(
                mode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Chế độ hiển thị VietQR không hợp lệ.");
        }

        lock (SyncRoot)
        {
            var directory =
                Path.GetDirectoryName(
                    _filePath);

            if (string.IsNullOrWhiteSpace(
                    directory))
            {
                throw new InvalidOperationException(
                    "Không xác định được thư mục lưu cấu hình VietQR.");
            }

            Directory.CreateDirectory(
                directory);

            var settings =
                new VietQrDisplayPreferenceFile
                {
                    Version =
                        CurrentVersion,

                    Mode =
                        mode
                };

            var json =
                JsonSerializer.Serialize(
                    settings,
                    JsonOptions);

            var temporaryPath =
                _filePath +
                ".tmp";

            try
            {
                File.WriteAllText(
                    temporaryPath,
                    json,
                    Utf8WithoutBom);

                File.Move(
                    temporaryPath,
                    _filePath,
                    overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(
                            temporaryPath))
                    {
                        File.Delete(
                            temporaryPath);
                    }
                }
                catch
                {
                    /*
                     * Xóa file tạm là best-effort.
                     */
                }
            }
        }
    }

    public static VietQrDisplayMode GetDefaultMode()
    {
        return VietQrDisplayMode
            .CustomerDisplay;
    }

    public static string GetDisplayName(
        VietQrDisplayMode mode)
    {
        return mode switch
        {
            VietQrDisplayMode.CustomerDisplay =>
                "Màn hình khách hàng",

            VietQrDisplayMode.CashierDisplay =>
                "Màn hình thu ngân",

            VietQrDisplayMode.PrintSlip =>
                "Mở in phiếu QR",

            VietQrDisplayMode.AskEveryTime =>
                "Hỏi mỗi giao dịch",

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(mode),
                    mode,
                    "Chế độ hiển thị VietQR không hợp lệ.")
        };
    }

    public static string GetDescription(
        VietQrDisplayMode mode)
    {
        return mode switch
        {
            VietQrDisplayMode.CustomerDisplay =>
                "Ưu tiên mở QR toàn màn hình trên màn hình thứ hai. " +
                "Nếu máy chỉ có một màn hình, hệ thống vẫn dùng " +
                "cửa sổ VietQR trên quầy.",

            VietQrDisplayMode.CashierDisplay =>
                "Chỉ mở QR trên màn hình thu ngân. " +
                "Nút in phiếu QR và toàn bộ bước xác nhận vẫn giữ nguyên.",

            VietQrDisplayMode.PrintSlip =>
                "Khi bắt đầu thanh toán, hệ thống tự mở hộp thoại in " +
                "phiếu QR. Thu ngân chọn máy in rồi tiếp tục xác nhận tiền.",

            VietQrDisplayMode.AskEveryTime =>
                "Mỗi giao dịch VietQR, thu ngân chọn dùng màn hình khách, " +
                "màn hình quầy hoặc mở in phiếu QR.",

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(mode),
                    mode,
                    "Chế độ hiển thị VietQR không hợp lệ.")
        };
    }

    private static string CreateDefaultFilePath()
    {
        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .LocalApplicationData);

        if (string.IsNullOrWhiteSpace(
                localApplicationData))
        {
            throw new InvalidOperationException(
                "Không xác định được thư mục LocalApplicationData.");
        }

        return Path.Combine(
            localApplicationData,
            ApplicationFolderName,
            SettingsFileName);
    }

    private static JsonSerializerOptions
        CreateJsonOptions()
    {
        var options =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,

                PropertyNameCaseInsensitive =
                    true,

                WriteIndented =
                    true
            };

        options.Converters.Add(
            new JsonStringEnumConverter());

        return options;
    }

    private sealed class
        VietQrDisplayPreferenceFile
    {
        public int Version
        {
            get;
            set;
        }

        public VietQrDisplayMode Mode
        {
            get;
            set;
        }
    }
}