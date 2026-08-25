using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;

namespace POS.Infrastructure.Persistence;

public sealed record SqliteIntegrityCheckResult(
    bool IsSuccess,
    string Message);

public sealed record SqliteBackupResult(
    bool IsSuccess,
    string? BackupFilePath,
    string Message);

internal enum SqliteBackupArtifactKind
{
    PreMigration,
    PreRestore
}

/// <summary>
/// Kiểm tra toàn vẹn SQLite và tạo bản backup đã được xác minh.
/// </summary>
public sealed class SqliteDatabaseSafetyService
{
    private const string MissingDatabaseMessage =
        "Không tìm thấy database SQLite.";

    public static SqliteIntegrityCheckResult CheckIntegrity(
        string databaseFilePath)
    {
        ValidatePathArgument(
            databaseFilePath,
            nameof(databaseFilePath));

        try
        {
            var fullPath =
                Path.GetFullPath(databaseFilePath);

            if (!File.Exists(fullPath))
            {
                return IntegrityFailure(
                    MissingDatabaseMessage);
            }

            var connectionString =
                new SqliteConnectionStringBuilder
                {
                    DataSource = fullPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Cache = SqliteCacheMode.Private,
                    Pooling = false
                }
                .ToString();

            using var connection =
                new SqliteConnection(
                    connectionString);

            connection.Open();

            using var command =
                connection.CreateCommand();

            command.CommandText =
                "PRAGMA integrity_check;";

            using var reader =
                command.ExecuteReader();

            var resultCount = 0;
            var allResultsAreOk = true;

            while (reader.Read())
            {
                resultCount++;

                if (reader.IsDBNull(0) ||
                    !string.Equals(
                        reader.GetString(0),
                        "ok",
                        StringComparison.OrdinalIgnoreCase))
                {
                    allResultsAreOk = false;
                }
            }

            if (resultCount == 1 &&
                allResultsAreOk)
            {
                return new SqliteIntegrityCheckResult(
                    true,
                    "Database SQLite toàn vẹn.");
            }

            return IntegrityFailure(
                resultCount == 0
                    ? "SQLite không trả về kết quả kiểm tra toàn vẹn."
                    : "Database SQLite không toàn vẹn. " +
                      $"Số kết quả lỗi: {resultCount}.");
        }
        catch (SqliteException)
        {
            return IntegrityFailure(
                "Không thể kiểm tra toàn vẹn database SQLite.");
        }
        catch (IOException)
        {
            return IntegrityFailure(
                "Không thể truy cập file database SQLite.");
        }
        catch (UnauthorizedAccessException)
        {
            return IntegrityFailure(
                "Không có quyền truy cập file database SQLite.");
        }
        catch (ArgumentException)
        {
            return IntegrityFailure(
                "Đường dẫn database SQLite không hợp lệ.");
        }
        catch (NotSupportedException)
        {
            return IntegrityFailure(
                "Đường dẫn database SQLite không được hỗ trợ.");
        }
        catch (Exception)
        {
            return IntegrityFailure(
                "Không thể kiểm tra toàn vẹn database SQLite.");
        }
    }

    public static SqliteBackupResult CreateVerifiedBackup(
        string sourceDatabaseFilePath,
        string backupDirectoryPath,
        DateTimeOffset utcNow)
    {
        return CreateVerifiedBackup(
            sourceDatabaseFilePath,
            backupDirectoryPath,
            utcNow,
            SqliteBackupArtifactKind.PreMigration);
    }

    internal static SqliteBackupResult CreateVerifiedBackup(
        string sourceDatabaseFilePath,
        string backupDirectoryPath,
        DateTimeOffset utcNow,
        SqliteBackupArtifactKind artifactKind)
    {
        ValidatePathArgument(
            sourceDatabaseFilePath,
            nameof(sourceDatabaseFilePath));

        ValidatePathArgument(
            backupDirectoryPath,
            nameof(backupDirectoryPath));

        string? temporaryFilePath = null;
        string? finalFilePath = null;
        var finalFileCreated = false;
        var finalFileVerified = false;

        try
        {
            var sourceFullPath =
                Path.GetFullPath(
                    sourceDatabaseFilePath);

            var backupDirectoryFullPath =
                Path.GetFullPath(
                    backupDirectoryPath);

            if (!File.Exists(sourceFullPath))
            {
                return BackupFailure(
                    MissingDatabaseMessage);
            }

            var sourceIntegrity =
                CheckIntegrity(
                    sourceFullPath);

            if (!sourceIntegrity.IsSuccess)
            {
                return BackupFailure(
                    "Không thể tạo backup vì database nguồn " +
                    "không vượt qua kiểm tra toàn vẹn.");
            }

            var normalizedUtcNow =
                utcNow.ToUniversalTime();

            var prefix = artifactKind switch
            {
                SqliteBackupArtifactKind.PreMigration => "pos-enterprise-pre-migration-",
                SqliteBackupArtifactKind.PreRestore => "pos-enterprise-pre-restore-",
                _ => throw new ArgumentOutOfRangeException(nameof(artifactKind))
            };

            var baseFileName =
                prefix +
                normalizedUtcNow.ToString(
                    "yyyyMMdd-HHmmssfff",
                    CultureInfo.InvariantCulture) +
                ".db";

            var initialFinalFilePath =
                Path.Combine(
                    backupDirectoryFullPath,
                    baseFileName);

            if (PathsAreEqual(
                    sourceFullPath,
                    initialFinalFilePath))
            {
                return BackupFailure(
                    "File backup không được trùng với database nguồn.");
            }

            Directory.CreateDirectory(
                backupDirectoryFullPath);

            finalFilePath =
                GetAvailableBackupFilePath(
                    backupDirectoryFullPath,
                    baseFileName);

            if (PathsAreEqual(
                    sourceFullPath,
                    finalFilePath))
            {
                return BackupFailure(
                    "File backup không được trùng với database nguồn.");
            }

            temporaryFilePath =
                Path.Combine(
                    backupDirectoryFullPath,
                    "." +
                    Path.GetFileName(finalFilePath) +
                    "." +
                    Guid.NewGuid().ToString("N") +
                    ".tmp");

            CreateSqliteBackup(
                sourceFullPath,
                temporaryFilePath);

            var temporaryIntegrity =
                CheckIntegrity(
                    temporaryFilePath);

            if (!temporaryIntegrity.IsSuccess)
            {
                return BackupFailure(
                    "File backup tạm không vượt qua kiểm tra toàn vẹn.");
            }

            NormalizeBackupJournalMode(
                temporaryFilePath);

            var normalizedTemporaryIntegrity =
                CheckIntegrity(
                    temporaryFilePath);

            if (!normalizedTemporaryIntegrity.IsSuccess)
            {
                return BackupFailure(
                    "File backup tạm sau journal normalization " +
                    "không vượt qua kiểm tra toàn vẹn.");
            }

            DeleteBackupSidecars(
                temporaryFilePath);

            File.Move(
                temporaryFilePath,
                finalFilePath,
                overwrite: false);

            temporaryFilePath = null;
            finalFileCreated = true;

            var finalIntegrity =
                CheckIntegrity(
                    finalFilePath);

            var finalFileInfo =
                new FileInfo(
                    finalFilePath);

            if (!finalFileInfo.Exists ||
                finalFileInfo.Length <= 0 ||
                !finalIntegrity.IsSuccess)
            {
                return BackupFailure(
                    "File backup cuối không vượt qua xác minh.");
            }

            DeleteBackupSidecars(
                finalFilePath);

            finalFileVerified = true;

            return new SqliteBackupResult(
                true,
                finalFilePath,
                "Đã tạo và xác minh backup SQLite.");
        }
        catch (SqliteException)
        {
            return BackupFailure(
                "Không thể tạo backup SQLite.");
        }
        catch (IOException)
        {
            return BackupFailure(
                "Không thể ghi file backup SQLite.");
        }
        catch (UnauthorizedAccessException)
        {
            return BackupFailure(
                "Không có quyền tạo file backup SQLite.");
        }
        catch (ArgumentException)
        {
            return BackupFailure(
                "Đường dẫn backup SQLite không hợp lệ.");
        }
        catch (NotSupportedException)
        {
            return BackupFailure(
                "Đường dẫn backup SQLite không được hỗ trợ.");
        }
        catch (Exception)
        {
            return BackupFailure(
                "Không thể tạo backup SQLite.");
        }
        finally
        {
            DeleteBackupArtifactsBestEffort(
                temporaryFilePath);

            if (finalFileCreated &&
                !finalFileVerified)
            {
                DeleteBackupArtifactsBestEffort(
                    finalFilePath);
            }
        }
    }

    private static void CreateSqliteBackup(
        string sourceFilePath,
        string destinationFilePath)
    {
        var sourceConnectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = sourceFilePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            }
            .ToString();

        var destinationConnectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = destinationFilePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            }
            .ToString();

        using var sourceConnection =
            new SqliteConnection(
                sourceConnectionString);

        using var destinationConnection =
            new SqliteConnection(
                destinationConnectionString);

        sourceConnection.Open();
        destinationConnection.Open();

        sourceConnection.BackupDatabase(
            destinationConnection);
    }

    private static void NormalizeBackupJournalMode(
        string backupFilePath)
    {
        var connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = backupFilePath,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            }
            .ToString();

        using var connection =
            new SqliteConnection(
                connectionString);

        connection.Open();

        using var command =
            connection.CreateCommand();

        command.CommandText =
            "PRAGMA journal_mode=DELETE;";

        var journalMode =
            Convert.ToString(
                command.ExecuteScalar(),
                CultureInfo.InvariantCulture);

        if (!string.Equals(
                journalMode,
                "delete",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Không thể chuyển backup SQLite sang journal_mode DELETE.");
        }
    }

    private static string GetAvailableBackupFilePath(
        string backupDirectoryPath,
        string baseFileName)
    {
        var candidate =
            Path.Combine(
                backupDirectoryPath,
                baseFileName);

        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var fileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(
                baseFileName);

        var extension =
            Path.GetExtension(
                baseFileName);

        do
        {
            candidate =
                Path.Combine(
                    backupDirectoryPath,
                    fileNameWithoutExtension +
                    "-" +
                    Guid.NewGuid()
                        .ToString("N")[..8] +
                    extension);
        }
        while (File.Exists(candidate));

        return candidate;
    }

    private static bool PathsAreEqual(
        string firstPath,
        string secondPath)
    {
        return string.Equals(
            Path.GetFullPath(firstPath),
            Path.GetFullPath(secondPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidatePathArgument(
        string path,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(
            path,
            parameterName);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Đường dẫn không được để trống.",
                parameterName);
        }
    }

    private static SqliteIntegrityCheckResult
        IntegrityFailure(
            string message)
    {
        return new SqliteIntegrityCheckResult(
            false,
            message);
    }

    private static SqliteBackupResult BackupFailure(
        string message)
    {
        return new SqliteBackupResult(
            false,
            null,
            message);
    }

    private static void DeleteFileBestEffort(
        string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Cleanup best-effort không được che lấp kết quả chính.
        }
    }

    private static void DeleteBackupSidecars(
        string backupFilePath)
    {
        DeleteFile(
            backupFilePath + "-wal");

        DeleteFile(
            backupFilePath + "-shm");
    }

    private static void DeleteFile(
        string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private static void DeleteBackupArtifactsBestEffort(
        string? backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(
                backupFilePath))
        {
            return;
        }

        DeleteFileBestEffort(
            backupFilePath);

        DeleteFileBestEffort(
            backupFilePath + "-wal");

        DeleteFileBestEffort(
            backupFilePath + "-shm");
    }
}
