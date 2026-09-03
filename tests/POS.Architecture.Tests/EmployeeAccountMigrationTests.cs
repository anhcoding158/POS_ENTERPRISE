using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class EmployeeAccountMigrationTests
{
    [Fact]
    public async Task Employee_security_hardening_upgrades_from_bulk_audit_schema_without_data_loss()
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
                var previous = migrations.Single(name => name.Contains("AddBulkProductAuditActions", StringComparison.Ordinal));
                await before.Database.MigrateAsync(previous);

                await using var command = before.Database.GetDbConnection().CreateCommand();
                command.CommandText = """
                    INSERT INTO "Users"
                        ("Id", "Username", "NormalizedUsername", "PasswordHash", "FullName", "Role", "IsActive", "FailedLoginAttempts", "LockedUntilUtc", "LastLoginAtUtc", "ConcurrencyToken", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES
                        (77, 'legacy.admin', 'LEGACY.ADMIN', 'fixture', 'Legacy Admin', 1, 1, 0, NULL, NULL, '77777777-7777-7777-7777-777777777777', 1787750400000, 1787750400000);

                    INSERT INTO "Employees"
                        ("Id", "EmployeeCode", "NormalizedEmployeeCode", "FullName", "PhoneNumber", "EmailAddress", "IsActive", "UserId", "ConcurrencyToken", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES
                        (77, 'EMP-LEGACY-77', 'EMP-LEGACY-77', 'Legacy Admin', NULL, NULL, 1, 77, '77777777-7777-7777-7777-777777777778', 1787750400000, 1787750400000);

                    INSERT INTO "SecurityAuditEvents"
                        ("Id", "ActorUserId", "TargetEmployeeId", "TargetUserId", "Action", "Result", "OperationId", "ActorDisplayNameSnapshot", "TargetDisplayNameSnapshot", "BusinessArea", "TargetType", "TerminalId", "BeforeValuesJson", "AfterValuesJson", "ConcurrencyToken", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES
                        (77, 77, 77, 77, 15, 'Success', '77777777-7777-7777-7777-777777777779', 'Legacy Admin', 'Legacy Admin', 'Sản phẩm', 'Batch', 'TERM-LEGACY', '[]', '[]', '77777777-7777-7777-7777-777777777780', 1787750400000, 1787750400000);
                    """;
                await before.Database.OpenConnectionAsync();
                Assert.Equal(3, await command.ExecuteNonQueryAsync());
            }

            await using (var after = new PosDbContext(options))
            {
                await after.Database.MigrateAsync();
            }

            await using (var secondStart = new PosDbContext(options))
            {
                await secondStart.Database.MigrateAsync();
            }

            await using var verify = new PosDbContext(options);
            var user = await verify.Users.SingleAsync(item => item.Id == 77);
            var employee = await verify.Employees.SingleAsync(item => item.UserId == 77);
            var historicalAudit = await verify.SecurityAuditEvents.SingleAsync(item => item.Id == 77);

            Assert.Equal(77, user.Id);
            Assert.Equal("EMP-LEGACY-77", employee.EmployeeCode);
            Assert.Equal("LEGACY.ADMIN", user.NormalizedUsername);
            Assert.Equal("fixture", user.PasswordHash);
            Assert.True(employee.IsActive);
            Assert.Equal("Legacy Admin", employee.FullName);

            Assert.Equal(SecurityAuditAction.BulkProductOperation, historicalAudit.Action);

            await verify.Database.OpenConnectionAsync();
            await using var schemaCommand = verify.Database.GetDbConnection().CreateCommand();
            schemaCommand.CommandText = "PRAGMA table_info(Users);";
            await using var schemaReader = await schemaCommand.ExecuteReaderAsync();
            var lastFailedColumnFound = false;
            while (await schemaReader.ReadAsync())
            {
                if (!string.Equals(schemaReader.GetString(1), "LastFailedLoginAtUtc", StringComparison.Ordinal))
                    continue;

                lastFailedColumnFound = true;
                Assert.Equal("INTEGER", schemaReader.GetString(2));
                Assert.Equal(0, schemaReader.GetInt32(3));
                Assert.True(schemaReader.IsDBNull(4));
            }
            Assert.True(lastFailedColumnFound);

            verify.SecurityAuditEvents.Add(new SecurityAuditEvent(
                actorUserId: user.Id,
                targetEmployeeId: employee.Id,
                targetUserId: user.Id,
                action: SecurityAuditAction.LoginFailed,
                result: "Failed",
                operationId: Guid.NewGuid(),
                utcNow: DateTimeOffset.FromUnixTimeMilliseconds(1787750400000),
                actorDisplayNameSnapshot: user.FullName,
                targetDisplayNameSnapshot: user.FullName,
                businessArea: "Nhân viên và tài khoản",
                targetType: "Tài khoản",
                terminalId: "TERM-TEST",
                changes: null));
            await verify.SaveChangesAsync();
            Assert.Contains(await verify.SecurityAuditEvents.ToArrayAsync(), item => item.Action == SecurityAuditAction.LoginFailed);
            Assert.True(await verify.Database.CanConnectAsync());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
