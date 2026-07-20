using Microsoft.Data.Sqlite;
using POS.Infrastructure;
using System.IO;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Xác định vị trí database SQLite và tạo connection string.
///
/// Trong môi trường phát triển:
/// - tìm thư mục chứa POS.Enterprise.slnx;
/// - lưu database trong thư mục data của solution.
///
/// Trong bản phát hành:
/// - nếu không còn solution file;
/// - lưu tương đối bên cạnh thư mục ứng dụng.
/// </summary>
public sealed class DatabasePathResolver
{
    private const string SolutionFileName =
        "POS.Enterprise.slnx";

    /// <summary>
    /// Chuyển đường dẫn cấu hình thành đường dẫn tuyệt đối
    /// và bảo đảm thư mục chứa database đã tồn tại.
    /// </summary>
    public string ResolveDatabasePath(
        string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new ArgumentException(
                "Đường dẫn database không được để trống.",
                nameof(configuredPath));
        }

        var trimmedPath = configuredPath.Trim();

        string fullPath;

        if (Path.IsPathRooted(trimmedPath))
        {
            fullPath = Path.GetFullPath(trimmedPath);
        }
        else
        {
            var baseDirectory =
                ResolveApplicationBaseDirectory();

            fullPath = Path.GetFullPath(
                Path.Combine(
                    baseDirectory,
                    trimmedPath));

            EnsureRelativePathDoesNotEscapeBaseDirectory(
                baseDirectory,
                fullPath);
        }

        var databaseDirectory =
            Path.GetDirectoryName(fullPath);

        if (string.IsNullOrWhiteSpace(databaseDirectory))
        {
            throw new InvalidOperationException(
                "Không xác định được thư mục chứa database.");
        }

        Directory.CreateDirectory(databaseDirectory);

        return fullPath;
    }

    /// <summary>
    /// Tạo connection string SQLite bằng builder
    /// để tránh lỗi ký tự đặc biệt và connection-string injection.
    /// </summary>
    public string CreateConnectionString(
        InfrastructureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        var databasePath =
            ResolveDatabasePath(options.DatabasePath);

        var builder =
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,

                Mode =
                    SqliteOpenMode.ReadWriteCreate,

                Cache =
                    SqliteCacheMode.Shared,

                ForeignKeys = true,

                DefaultTimeout =
                    options.DatabaseTimeoutSeconds,

                Pooling = true
            };

        return builder.ToString();
    }

    /// <summary>
    /// Tìm thư mục gốc của solution từ một thư mục bất kỳ.
    /// </summary>
    internal static string? FindSolutionRoot(
        string startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            return null;
        }

        DirectoryInfo? currentDirectory;

        try
        {
            currentDirectory = new DirectoryInfo(
                Path.GetFullPath(startDirectory));
        }
        catch (
            Exception exception)
            when (exception is
                ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return null;
        }

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(
                currentDirectory.FullName,
                SolutionFileName);

            if (File.Exists(solutionPath))
            {
                return currentDirectory.FullName;
            }

            currentDirectory =
                currentDirectory.Parent;
        }

        return null;
    }

    private static string ResolveApplicationBaseDirectory()
    {
        /*
         * Khi chạy dotnet ef hoặc Visual Studio,
         * Environment.CurrentDirectory thường nằm trong solution.
         */
        var solutionRoot =
            FindSolutionRoot(
                Environment.CurrentDirectory);

        if (solutionRoot is not null)
        {
            return solutionRoot;
        }

        /*
         * Khi chạy ứng dụng từ thư mục bin,
         * ta tiếp tục tìm ngược từ AppContext.BaseDirectory.
         */
        solutionRoot =
            FindSolutionRoot(
                AppContext.BaseDirectory);

        if (solutionRoot is not null)
        {
            return solutionRoot;
        }

        /*
 * Bản đóng gói không còn solution file:
 * database được đặt trong LocalApplicationData của
 * tài khoản Windows hiện tại.
 *
 * Không đặt database trong Program Files hoặc cạnh executable
 * vì người dùng thường không có quyền ghi tại đó.
 */
        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "Không xác định được thư mục LocalApplicationData.");
        }

        var applicationDataDirectory =
            Path.Combine(
                localApplicationData,
                "POS Enterprise");

        Directory.CreateDirectory(
            applicationDataDirectory);

        return Path.GetFullPath(
            applicationDataDirectory);
    }

    private static void
        EnsureRelativePathDoesNotEscapeBaseDirectory(
            string baseDirectory,
            string fullPath)
    {
        var normalizedBaseDirectory =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(baseDirectory));

        var relativePath =
            Path.GetRelativePath(
                normalizedBaseDirectory,
                fullPath);

        var escapesBaseDirectory =
            string.Equals(
                relativePath,
                "..",
                StringComparison.Ordinal) ||

            relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) ||

            relativePath.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal);

        if (escapesBaseDirectory)
        {
            throw new InvalidOperationException(
                "Đường dẫn database tương đối không được " +
                "thoát ra ngoài thư mục ứng dụng.");
        }
    }
}