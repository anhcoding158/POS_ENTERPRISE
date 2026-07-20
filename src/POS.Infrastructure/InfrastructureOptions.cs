using POS.Domain.Constants;

namespace POS.Infrastructure;

/// <summary>
/// Cấu hình kỹ thuật của tầng Infrastructure.
///
/// Các thuộc tính được ánh xạ trực tiếp từ section:
///
/// "Infrastructure"
///
/// trong appsettings.json.
/// </summary>
public sealed class InfrastructureOptions
{
    public const string SectionName = "Infrastructure";

    private const int MinimumAdminPasswordLength = 8;

    /// <summary>
    /// Đường dẫn tương đối hoặc tuyệt đối tới database SQLite.
    ///
    /// Ví dụ:
    /// data/pos-enterprise.db
    /// </summary>
    public string DatabasePath { get; set; } =
        "data/pos-enterprise.db";

    /// <summary>
    /// Thời gian timeout mặc định của lệnh SQLite,
    /// tính theo giây.
    /// </summary>
    public int DatabaseTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Tự động chạy migration khi ứng dụng khởi động.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;

    /// <summary>
    /// Cho phép tạo tài khoản quản trị đầu tiên
    /// khi database chưa có người dùng.
    /// </summary>
    /// 
    /// <summary>
    /// Cho phép tạo danh mục và sản phẩm minh họa.
    ///
    /// Giá trị mặc định phải là false để bản phát hành
    /// không tự đưa dữ liệu demo vào database của cửa hàng.
    /// </summary>
    public bool SeedDemoProductCatalog { get; set; }
    public bool SeedDefaultAdministrator { get; set; }

    public string DefaultAdminUsername { get; set; } =
        "admin";

    /// <summary>
    /// Mật khẩu bootstrap dạng rõ.
    ///
    /// Giá trị này chỉ được dùng để tạo password hash lần đầu.
    /// Không bao giờ lưu trực tiếp vào database.
    ///
    /// Sau giai đoạn demo, cấu hình này sẽ được chuyển khỏi
    /// appsettings.json sang cơ chế secret an toàn hơn.
    /// </summary>
    public string DefaultAdminPassword { get; set; } =
        string.Empty;

    public string DefaultAdminFullName { get; set; } =
        "Quản trị viên hệ thống";

    /// <summary>
    /// Kiểm tra cấu hình trước khi mở database.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            throw new InvalidOperationException(
                "Infrastructure:DatabasePath không được để trống.");
        }

        if (DatabaseTimeoutSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException(
                "Infrastructure:DatabaseTimeoutSeconds " +
                "phải nằm trong khoảng từ 1 đến 300 giây.");
        }

        if (!SeedDefaultAdministrator)
        {
            return;
        }

        var username = DefaultAdminUsername?.Trim()
            ?? string.Empty;

        if (username.Length <
                BusinessRules.Users.UsernameMinLength ||
            username.Length >
                BusinessRules.Users.UsernameMaxLength)
        {
            throw new InvalidOperationException(
                $"Infrastructure:DefaultAdminUsername phải có từ " +
                $"{BusinessRules.Users.UsernameMinLength} đến " +
                $"{BusinessRules.Users.UsernameMaxLength} ký tự.");
        }

        if (string.IsNullOrWhiteSpace(
                DefaultAdminPassword) ||
            DefaultAdminPassword.Length <
                MinimumAdminPasswordLength)
        {
            throw new InvalidOperationException(
                $"Infrastructure:DefaultAdminPassword phải có " +
                $"ít nhất {MinimumAdminPasswordLength} ký tự.");
        }

        var fullName = DefaultAdminFullName?.Trim()
            ?? string.Empty;

        if (fullName.Length == 0 ||
            fullName.Length >
                BusinessRules.Users.FullNameMaxLength)
        {
            throw new InvalidOperationException(
                "Infrastructure:DefaultAdminFullName không hợp lệ.");
        }
    }
}