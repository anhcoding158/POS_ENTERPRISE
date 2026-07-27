using Microsoft.Data.Sqlite;
using POS.Infrastructure.Persistence;
using System.Globalization;
using System.IO;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SqliteDatabaseSafetyServiceTests
{
    private static readonly DateTimeOffset BackupUtcNow =
        new(
            2026,
            7,
            27,
            8,
            30,
            45,
            123,
            TimeSpan.Zero);

    [Fact]
    public void Integrity_check_must_succeed_for_valid_database()
    {
        var testDirectory =
            CreateTestDirectory();

        try
        {
            var databasePath =
                Path.Combine(
                    testDirectory,
                    "valid.db");

            CreateDatabaseWithRow(
                databasePath,
                "valid-row");

            var service =
                new SqliteDatabaseSafetyService();

            var result =
                service.CheckIntegrity(
                    databasePath);

            Assert.True(
                result.IsSuccess,
                result.Message);
        }
        finally
        {
            DeleteTestDirectory(
                testDirectory);
        }
    }

    [Fact]
    public void Integrity_check_must_fail_when_database_is_missing()
    {
        var testDirectory =
            CreateTestDirectory();

        try
        {
            var missingPath =
                Path.Combine(
                    testDirectory,
                    "missing.db");

            var service =
                new SqliteDatabaseSafetyService();

            var result =
                service.CheckIntegrity(
                    missingPath);

            Assert.False(
                result.IsSuccess);

            Assert.False(
                File.Exists(missingPath));
        }
        finally
        {
            DeleteTestDirectory(
                testDirectory);
        }
    }

    [Fact]
    public void
        Integrity_check_must_fail_for_corrupt_file_without_throwing()
    {
        var testDirectory =
            CreateTestDirectory();

        try
        {
            var corruptPath =
                Path.Combine(
                    testDirectory,
                    "corrupt.db");

            File.WriteAllBytes(
                corruptPath,
                [0x13, 0x37, 0x42, 0x00, 0x7F]);

            var service =
                new SqliteDatabaseSafetyService();

            var exception =
                Record.Exception(
                    () =>
                    {
                        var result =
                            service.CheckIntegrity(
                                corruptPath);

                        Assert.False(
                            result.IsSuccess);
                    });

            Assert.Null(
                exception);
        }
        finally
        {
            DeleteTestDirectory(
                testDirectory);
        }
    }

    [Fact]
    public void Backup_must_create_verified_database_copy()
    {
        var testDirectory =
            CreateTestDirectory();

        try
        {
            var sourcePath =
                Path.Combine(
                    testDirectory,
                    "source.db");

            var backupDirectory =
                Path.Combine(
                    testDirectory,
                    "backups");

            CreateDatabaseWithRow(
                sourcePath,
                "copied-row");

            var service =
                new SqliteDatabaseSafetyService();

            var result =
                service.CreateVerifiedBackup(
                    sourcePath,
                    backupDirectory,
                    BackupUtcNow);

            Assert.True(
                result.IsSuccess,
                result.Message);

            Assert.NotNull(
                result.BackupFilePath);

            Assert.True(
                File.Exists(
                    result.BackupFilePath));

            Assert.True(
                new FileInfo(
                    result.BackupFilePath).Length > 0);

            Assert.Equal(
                "copied-row",
                ReadStoredValue(
                    result.BackupFilePath));

            var integrity =
                service.CheckIntegrity(
                    result.BackupFilePath);

            Assert.True(
                integrity.IsSuccess,
                integrity.Message);
        }
        finally
        {
            DeleteTestDirectory(
                testDirectory);
        }
    }

    [Fact]
    public void
        Verified_backup_from_wal_source_must_include_committed_wal_data()
    {
        var testDirectory =
            CreateTestDirectory();

        try
        {
            var sourcePath =
                Path.Combine(
                    testDirectory,
                    "wal-source.db");

            var backupDirectory =
                Path.Combine(
                    testDirectory,
                    "backups");

            using var sourceConnection =
                OpenReadWriteConnection(
                    sourcePath);

            ExecuteNonQuery(
                sourceConnection,
                "PRAGMA journal_mode=WAL;");

            ExecuteNonQuery(
                sourceConnection,
                "PRAGMA wal_autocheckpoint=0;");

            ExecuteNonQuery(
                sourceConnection,
                """
                CREATE TABLE SafetyRows
                (
                    Id INTEGER PRIMARY KEY,
                    Value TEXT NOT NULL
                );
                """);

            using (var transaction =
                   sourceConnection.BeginTransaction())
            {
                using var command =
                    sourceConnection.CreateCommand();

                command.Transaction =
                    transaction;

                command.CommandText =
                    """
                    INSERT INTO SafetyRows (Value)
                    VALUES ('committed-wal-row');
                    """;

                command.ExecuteNonQuery();
                transaction.Commit();
            }

            var service =
                new SqliteDatabaseSafetyService();

            var result =
                service.CreateVerifiedBackup(
                    sourcePath,
                    backupDirectory,
                    BackupUtcNow);

            Assert.True(
                result.IsSuccess,
                result.Message);

            Assert.NotNull(
                result.BackupFilePath);

            Assert.True(
                File.Exists(sourcePath + "-wal"));

            Assert.True(
                File.Exists(sourcePath + "-shm"));

            Assert.Equal(
                "committed-wal-row",
                ReadStoredValue(
                    result.BackupFilePath));

            Assert.False(
                File.Exists(
                    result.BackupFilePath + "-wal"));

            Assert.False(
                File.Exists(
                    result.BackupFilePath + "-shm"));
        }
        finally
        {
            DeleteTestDirectory(
                testDirectory);
        }
    }

    [Fact]
    public void Collision_retry_must_not_delete_existing_valid_backup()
    {
        var testDirectory =
            CreateTestDirectory();

        try
        {
            var sourcePath =
                Path.Combine(
                    testDirectory,
                    "source.db");

            var backupDirectory =
                Path.Combine(
                    testDirectory,
                    "backups");

            CreateDatabaseWithRow(
                sourcePath,
                "collision-row");

            var service =
                new SqliteDatabaseSafetyService();

            var first =
                service.CreateVerifiedBackup(
                    sourcePath,
                    backupDirectory,
                    BackupUtcNow);

            var second =
                service.CreateVerifiedBackup(
                    sourcePath,
                    backupDirectory,
                    BackupUtcNow);

            Assert.True(
                first.IsSuccess,
                first.Message);

            Assert.True(
                second.IsSuccess,
                second.Message);

            Assert.NotNull(
                first.BackupFilePath);

            Assert.NotNull(
                second.BackupFilePath);

            Assert.NotEqual(
                first.BackupFilePath,
                second.BackupFilePath);

            Assert.True(
                File.Exists(
                    first.BackupFilePath));

            Assert.True(
                File.Exists(
                    second.BackupFilePath));
        }
        finally
        {
            DeleteTestDirectory(
                testDirectory);
        }
    }

    [Fact]
    public void Verified_backup_must_not_leave_temporary_wal_or_shm()
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var sourcePath =
                Path.Combine(testDirectory, "source.db");
            var backupDirectory =
                Path.Combine(testDirectory, "backups");

            CreateDatabaseWithRow(sourcePath, "temporary-cleanup");

            var result =
                new SqliteDatabaseSafetyService()
                    .CreateVerifiedBackup(
                        sourcePath,
                        backupDirectory,
                        BackupUtcNow);

            Assert.True(result.IsSuccess, result.Message);
            Assert.DoesNotContain(
                Directory.GetFiles(backupDirectory),
                path =>
                    Path.GetFileName(path).StartsWith(
                        '.'));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Verified_backup_must_not_leave_final_wal_or_shm()
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var sourcePath =
                Path.Combine(testDirectory, "source.db");
            var backupDirectory =
                Path.Combine(testDirectory, "backups");

            CreateDatabaseWithRow(sourcePath, "final-cleanup");

            var result =
                new SqliteDatabaseSafetyService()
                    .CreateVerifiedBackup(
                        sourcePath,
                        backupDirectory,
                        BackupUtcNow);

            Assert.True(result.IsSuccess, result.Message);
            Assert.NotNull(result.BackupFilePath);
            Assert.False(File.Exists(result.BackupFilePath + "-wal"));
            Assert.False(File.Exists(result.BackupFilePath + "-shm"));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Backup_cleanup_must_not_delete_source_wal_or_shm()
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var sourcePath =
                Path.Combine(testDirectory, "source.db");
            var backupDirectory =
                Path.Combine(testDirectory, "backups");

            using var sourceConnection =
                OpenReadWriteConnection(sourcePath);

            ExecuteNonQuery(sourceConnection, "PRAGMA journal_mode=WAL;");
            ExecuteNonQuery(sourceConnection, "PRAGMA wal_autocheckpoint=0;");
            ExecuteNonQuery(
                sourceConnection,
                "CREATE TABLE SafetyRows " +
                "(Id INTEGER PRIMARY KEY, Value TEXT NOT NULL);");
            ExecuteNonQuery(
                sourceConnection,
                "INSERT INTO SafetyRows (Value) VALUES ('source-sidecars');");

            var result =
                new SqliteDatabaseSafetyService()
                    .CreateVerifiedBackup(
                        sourcePath,
                        backupDirectory,
                        BackupUtcNow);

            Assert.True(result.IsSuccess, result.Message);
            Assert.True(File.Exists(sourcePath + "-wal"));
            Assert.True(File.Exists(sourcePath + "-shm"));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Final_backup_must_open_without_sidecars()
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var result =
                CreateVerifiedTestBackup(
                    testDirectory,
                    "opens-without-sidecars");

            Assert.NotNull(result.BackupFilePath);
            Assert.Equal(
                "opens-without-sidecars",
                ReadStoredValue(result.BackupFilePath));
            Assert.False(File.Exists(result.BackupFilePath + "-wal"));
            Assert.False(File.Exists(result.BackupFilePath + "-shm"));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Final_backup_must_pass_integrity_without_sidecars()
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var result =
                CreateVerifiedTestBackup(
                    testDirectory,
                    "integrity-without-sidecars");

            Assert.NotNull(result.BackupFilePath);

            var integrity =
                new SqliteDatabaseSafetyService()
                    .CheckIntegrity(result.BackupFilePath);

            Assert.True(integrity.IsSuccess, integrity.Message);
            Assert.False(File.Exists(result.BackupFilePath + "-wal"));
            Assert.False(File.Exists(result.BackupFilePath + "-shm"));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Failed_backup_must_cleanup_temporary_database_and_sidecars()
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var sourcePath =
                Path.Combine(testDirectory, "corrupt.db");
            var backupDirectory =
                Path.Combine(testDirectory, "backups");

            File.WriteAllBytes(sourcePath, [0x00, 0x01, 0x02, 0x03]);

            var result =
                new SqliteDatabaseSafetyService()
                    .CreateVerifiedBackup(
                        sourcePath,
                        backupDirectory,
                        BackupUtcNow);

            Assert.False(result.IsSuccess);
            Assert.True(
                !Directory.Exists(backupDirectory) ||
                Directory.GetFiles(backupDirectory).Length == 0);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Cleanup_must_only_target_exact_backup_paths()
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var backupDirectory =
                Path.Combine(testDirectory, "backups");
            Directory.CreateDirectory(backupDirectory);

            var nearbyFile =
                Path.Combine(
                    backupDirectory,
                    "pos-enterprise-pre-migration-nearby.db-wal");
            File.WriteAllText(nearbyFile, "must-survive");

            var result =
                CreateVerifiedTestBackup(
                    testDirectory,
                    "exact-paths");

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("must-survive", File.ReadAllText(nearbyFile));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Repeated_cleanup_must_be_idempotent()
    {
        var testDirectory = CreateTestDirectory();

        try
        {
            var first =
                CreateVerifiedTestBackup(testDirectory, "repeated");
            var second =
                new SqliteDatabaseSafetyService()
                    .CreateVerifiedBackup(
                        Path.Combine(testDirectory, "source.db"),
                        Path.Combine(testDirectory, "backups"),
                        BackupUtcNow);

            Assert.True(first.IsSuccess, first.Message);
            Assert.True(second.IsSuccess, second.Message);
            Assert.NotEqual(first.BackupFilePath, second.BackupFilePath);
            Assert.True(File.Exists(first.BackupFilePath));
            Assert.True(File.Exists(second.BackupFilePath));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Backup_must_not_be_created_when_source_is_corrupt()
    {
        var testDirectory =
            CreateTestDirectory();

        try
        {
            var sourcePath =
                Path.Combine(
                    testDirectory,
                    "corrupt.db");

            var backupDirectory =
                Path.Combine(
                    testDirectory,
                    "backups");

            File.WriteAllBytes(
                sourcePath,
                [0x00, 0x01, 0x02, 0x03]);

            var service =
                new SqliteDatabaseSafetyService();

            var result =
                service.CreateVerifiedBackup(
                    sourcePath,
                    backupDirectory,
                    BackupUtcNow);

            Assert.False(
                result.IsSuccess);

            Assert.False(
                Directory.Exists(
                    backupDirectory));
        }
        finally
        {
            DeleteTestDirectory(
                testDirectory);
        }
    }

    [Fact]
    public void
        Backup_must_reject_source_and_destination_as_same_file()
    {
        var testDirectory =
            CreateTestDirectory();

        try
        {
            var expectedFileName =
                "pos-enterprise-pre-migration-" +
                BackupUtcNow.ToString(
                    "yyyyMMdd-HHmmssfff",
                    CultureInfo.InvariantCulture) +
                ".db";

            var sourcePath =
                Path.Combine(
                    testDirectory,
                    expectedFileName);

            CreateDatabaseWithRow(
                sourcePath,
                "source-must-survive");

            var sourceLength =
                new FileInfo(
                    sourcePath).Length;

            var service =
                new SqliteDatabaseSafetyService();

            var result =
                service.CreateVerifiedBackup(
                    sourcePath,
                    testDirectory,
                    BackupUtcNow);

            Assert.False(
                result.IsSuccess);

            Assert.True(
                File.Exists(sourcePath));

            Assert.Equal(
                sourceLength,
                new FileInfo(
                    sourcePath).Length);

            Assert.Equal(
                "source-must-survive",
                ReadStoredValue(
                    sourcePath));
        }
        finally
        {
            DeleteTestDirectory(
                testDirectory);
        }
    }

    private static void CreateDatabaseWithRow(
        string databasePath,
        string value)
    {
        using var connection =
            OpenReadWriteConnection(
                databasePath);

        ExecuteNonQuery(
            connection,
            """
            CREATE TABLE SafetyRows
            (
                Id INTEGER PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """);

        using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO SafetyRows (Value)
            VALUES ($value);
            """;

        command.Parameters.AddWithValue(
            "$value",
            value);

        command.ExecuteNonQuery();
    }

    private static SqliteBackupResult CreateVerifiedTestBackup(
        string testDirectory,
        string value)
    {
        var sourcePath =
            Path.Combine(testDirectory, "source.db");
        var backupDirectory =
            Path.Combine(testDirectory, "backups");

        CreateDatabaseWithRow(sourcePath, value);

        var result =
            new SqliteDatabaseSafetyService()
                .CreateVerifiedBackup(
                    sourcePath,
                    backupDirectory,
                    BackupUtcNow);

        Assert.True(result.IsSuccess, result.Message);

        return result;
    }

    private static SqliteConnection OpenReadWriteConnection(
        string databasePath)
    {
        var connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            }
            .ToString();

        var connection =
            new SqliteConnection(
                connectionString);

        connection.Open();

        return connection;
    }

    private static string ReadStoredValue(
        string databasePath)
    {
        var connectionString =
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
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
            """
            SELECT Value
            FROM SafetyRows
            ORDER BY Id
            LIMIT 1;
            """;

        return Assert.IsType<string>(
            command.ExecuteScalar());
    }

    private static void ExecuteNonQuery(
        SqliteConnection connection,
        string commandText)
    {
        using var command =
            connection.CreateCommand();

        command.CommandText =
            commandText;

        command.ExecuteNonQuery();
    }

    private static string CreateTestDirectory()
    {
        var testDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "POS-Enterprise-Sqlite-Safety-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            testDirectory);

        return testDirectory;
    }

    private static void DeleteTestDirectory(
        string testDirectory)
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(
                testDirectory,
                recursive: true);
        }
    }
}
