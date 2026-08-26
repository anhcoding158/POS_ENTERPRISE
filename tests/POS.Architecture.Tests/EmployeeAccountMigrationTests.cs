using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class EmployeeAccountMigrationTests
{
    [Fact]
    public async Task Upgrade_from_pre_staff_schema_preserves_existing_user_and_backfills_employee()
    {
        var root = Path.Combine(Path.GetTempPath(), "POS-R42-Migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "upgrade.db");

        try
        {
            var options = new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite($"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                .Options;

            await using (var before = new PosDbContext(options))
            {
                var migrations = before.Database.GetMigrations().ToArray();
                var current = migrations.Single(name => name.Contains("AddEmployeeAccountManagement", StringComparison.Ordinal));
                var previous = migrations.TakeWhile(name => !string.Equals(name, current, StringComparison.Ordinal)).Last();
                await before.Database.MigrateAsync(previous);

                await using var command = before.Database.GetDbConnection().CreateCommand();
                command.CommandText = """
                    INSERT INTO "Users"
                        ("Id", "Username", "NormalizedUsername", "PasswordHash", "FullName", "Role", "IsActive", "FailedLoginAttempts", "LockedUntilUtc", "LastLoginAtUtc", "ConcurrencyToken", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES
                        (77, 'legacy.admin', 'LEGACY.ADMIN', 'fixture', 'Legacy Admin', 1, 1, 0, NULL, NULL, '77777777-7777-7777-7777-777777777777', 1787750400000, 1787750400000);
                    """;
                await before.Database.OpenConnectionAsync();
                Assert.Equal(1, await command.ExecuteNonQueryAsync());
            }

            await using (var after = new PosDbContext(options))
            {
                await after.Database.MigrateAsync();
            }

            await using var verify = new PosDbContext(options);
            var user = await verify.Users.SingleAsync(item => item.Id == 77);
            var employee = await verify.Employees.SingleAsync(item => item.UserId == 77);

            Assert.Equal(77, user.Id);
            Assert.Equal("EMP-00000000000000000000004D", employee.EmployeeCode);
            Assert.Equal("LEGACY.ADMIN", user.NormalizedUsername);
            Assert.True(employee.IsActive);
            Assert.Equal("Legacy Admin", employee.FullName);
            Assert.True(await verify.Database.CanConnectAsync());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
