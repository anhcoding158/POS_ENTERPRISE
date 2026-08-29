using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Enums;
using POS.Domain.Entities;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Persistence;

namespace POS.Architecture.Tests;

internal static class PortableDevelopmentDatabase
{
    public static async Task CreateMigratedAsync(
        string databasePath,
        bool seedEmployee = false)
    {
        var fullDatabasePath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullDatabasePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("The portable test database directory is required.");

        var scenarioRoot = new DirectoryInfo(directory).FullName;
        var tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Directory.GetParent(scenarioRoot)?.FullName is not { } parent ||
            !string.Equals(parent, tempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(scenarioRoot).StartsWith(
                "POS-Enterprise-",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The portable test database must be in a project-owned direct TEMP child.");
        }

        Directory.CreateDirectory(scenarioRoot);
        if ((File.GetAttributes(scenarioRoot) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("The portable test database scenario cannot be a reparse point.");

        if (File.Exists(fullDatabasePath))
            throw new InvalidOperationException("The portable test database path must be unique.");

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false
        }.ToString();

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var context = new PosDbContext(options);
        await context.Database.MigrateAsync();

        if (!seedEmployee)
            return;

        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var user = new User(
            "portable-admin",
            new BCryptPasswordHasher().HashPassword(nameof(PortableDevelopmentDatabase)),
            "Portable Administrator",
            Role.Administrator,
            now);
        var employee = new Employee(
            "EMP-PORTABLE",
            "Portable Administrator",
            "+84 900 000 001",
            "portable@example.invalid",
            now);

        employee.AttachAccount(user, now);
        context.Users.Add(user);
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
    }

    public static void DeleteOwnedScenario(string scenarioRoot)
    {
        var fullRoot = new DirectoryInfo(Path.GetFullPath(scenarioRoot)).FullName;
        var tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Directory.GetParent(fullRoot)?.FullName;

        if (!string.Equals(parent, tempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullRoot).StartsWith(
                "POS-Enterprise-",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The test scenario is not an exact project-owned TEMP child.");
        }

        if (Directory.Exists(fullRoot) &&
            (File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "The test scenario cannot be deleted because it is a reparse point.");
        }

        for (var attempt = 0; attempt < 20 && Directory.Exists(fullRoot); attempt++)
        {
            try
            {
                Directory.Delete(fullRoot, recursive: true);
            }
            catch (IOException) when (attempt < 19)
            {
                Thread.Sleep(50);
            }
            catch (IOException)
            {
                Console.Error.WriteLine(
                    $"Owned test scenario cleanup deferred until testhost exit: {fullRoot}");
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 19)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine(
                    $"Owned test scenario cleanup deferred until testhost exit: {fullRoot}");
                return;
            }
        }

        if (Directory.Exists(fullRoot))
        {
            Console.Error.WriteLine(
                $"Owned test scenario cleanup deferred until testhost exit: {fullRoot}");
        }
    }
}
