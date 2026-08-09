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
    public static string ResolveDatabasePath(
        string configuredPath)
    {
        var fullPath = ResolveDatabasePathWithoutCreatingDirectory(
            configuredPath);

        var databaseDirectory =
            Path.GetDirectoryName(fullPath)!;

        Directory.CreateDirectory(databaseDirectory);

        return fullPath;
    }

    /// <summary>
    /// Canonicalizes and validates the configured database path without
    /// creating a directory or file. Metadata-only diagnostics use this API.
    /// </summary>
    public static string ResolveDatabasePathWithoutCreatingDirectory(
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

        return fullPath;
    }

    /// <summary>
    /// Resolve the same absolute path used by the runtime and derive its
    /// process-ownership identity without opening SQLite.
    /// </summary>
    public static DatabaseIdentity ResolveDatabaseIdentity(
        string configuredPath)
    {
        return DatabaseIdentity.FromResolvedPath(
            ResolveDatabasePath(configuredPath));
    }

    /// <summary>
    /// Tạo connection string SQLite bằng builder
    /// để tránh lỗi ký tự đặc biệt và connection-string injection.
    /// </summary>
    public static string CreateConnectionString(
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
        return ResolveApplicationBaseDirectory(
            Environment.CurrentDirectory,
            AppContext.BaseDirectory);
    }

    internal static string ResolveApplicationBaseDirectory(
        string currentDirectory,
        string applicationBaseDirectory,
        string? processPath = null,
        string? entryAssemblyName = null)
    {
        /*
         * Chỉ nhận solution-root khi executable nằm trong output build của
         * source/test và không ở dưới nhánh publish. Published output dù được
         * đặt dưới repository vẫn phải dùng LocalAppData để không mở nhầm
         * database phát triển.
         */
        var solutionRoot =
            FindSolutionRoot(applicationBaseDirectory);

        if (solutionRoot is not null &&
            IsDevelopmentOutput(applicationBaseDirectory))
        {
            return solutionRoot;
        }

        /*
         * Nếu application base nằm trong repository nhưng không phải output
         * phát triển của POS.Wpf thì đó là publish/artifact. Không được dùng
         * solution-root chỉ vì artifact được giải nén bên trong checkout.
         */
        if (solutionRoot is not null)
        {
            return ResolveLocalApplicationDataDirectory();
        }

        solutionRoot = FindSolutionRoot(currentDirectory);
        if (solutionRoot is not null &&
            IsRepositoryToolingProcess(
                processPath,
                entryAssemblyName))
        {
            return solutionRoot;
        }

        return ResolveLocalApplicationDataDirectory();
    }

    private static string ResolveLocalApplicationDataDirectory()
    {

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

        return Path.GetFullPath(
            applicationDataDirectory);
    }

    /// <summary>
    /// Identifies a source/test build output without using the current
    /// directory, executable name or environment name.
    /// </summary>
    public static bool IsDevelopmentOutput(
        string applicationBaseDirectory)
    {
        var solutionRoot =
            FindSolutionRoot(applicationBaseDirectory);

        return solutionRoot is not null &&
            IsRepositoryDevelopmentOutput(
                solutionRoot,
                applicationBaseDirectory) &&
            File.Exists(Path.Combine(
                Path.GetFullPath(applicationBaseDirectory),
                "appsettings.Development.json"));
    }

    private static bool IsRepositoryDevelopmentOutput(
        string solutionRoot,
        string applicationBaseDirectory)
    {
        var relativePath = Path.GetRelativePath(
            Path.GetFullPath(solutionRoot),
            Path.GetFullPath(applicationBaseDirectory));

        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Length >= 3 &&
            (string.Equals(
                segments[0],
                "src",
                StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                segments[0],
                "tests",
                StringComparison.OrdinalIgnoreCase)) &&
            segments.Any(segment =>
                string.Equals(
                    segment,
                    "bin",
                    StringComparison.OrdinalIgnoreCase)) &&
            !segments.Any(segment =>
                string.Equals(
                    segment,
                    "publish",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRepositoryToolingProcess(
        string? processPath,
        string? entryAssemblyName)
    {
        return string.Equals(
            entryAssemblyName,
            "dotnet-ef",
            StringComparison.OrdinalIgnoreCase);
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
