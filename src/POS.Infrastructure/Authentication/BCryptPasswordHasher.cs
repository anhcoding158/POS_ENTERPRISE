using System.Text;
using POS.Application.Abstractions.Authentication;

namespace POS.Infrastructure.Authentication;

/// <summary>
/// Mã hóa mật khẩu bằng BCrypt.
///
/// Mật khẩu gốc tuyệt đối không được lưu vào database
/// hoặc ghi vào log.
/// </summary>
public sealed class BCryptPasswordHasher :
    IPasswordHasher
{
    /*
     * Work factor 12 cân bằng giữa:
     * - độ an toàn;
     * - thời gian đăng nhập trên máy POS phổ thông.
     */
    private const int WorkFactor = 12;

    /*
     * BCrypt tiêu chuẩn chỉ xử lý tối đa 72 byte.
     *
     * Giới hạn theo UTF-8 byte thay vì số ký tự để tránh
     * mật khẩu Unicode bị cắt âm thầm.
     */
    private const int MaximumPasswordUtf8Bytes = 72;

    public string HashPassword(
        string password)
    {
        ValidatePasswordForHashing(
            password);

        return BCrypt.Net.BCrypt.HashPassword(
            password,
            WorkFactor);
    }

    public bool VerifyPassword(
        string password,
        string passwordHash)
    {
        if (string.IsNullOrEmpty(password) ||
            string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        if (Encoding.UTF8.GetByteCount(password) >
            MaximumPasswordUtf8Bytes)
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(
                password,
                passwordHash);
        }
        catch (Exception)
        {
            /*
             * Password hash bị lỗi hoặc không đúng định dạng
             * phải thất bại an toàn, không làm ứng dụng crash.
             */
            return false;
        }
    }

    private static void ValidatePasswordForHashing(
        string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException(
                "Mật khẩu không được để trống.",
                nameof(password));
        }

        var utf8ByteCount =
            Encoding.UTF8.GetByteCount(
                password);

        if (utf8ByteCount >
            MaximumPasswordUtf8Bytes)
        {
            throw new ArgumentException(
                $"Mật khẩu vượt quá giới hạn " +
                $"{MaximumPasswordUtf8Bytes} byte UTF-8 của BCrypt.",
                nameof(password));
        }
    }
}