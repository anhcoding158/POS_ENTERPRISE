using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Suppliers;
using POS.Application.Services;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SupplierPersistenceIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Fresh_database_supports_supplier_crud_search_filter_and_audit()
    {
        await using var database = await TestDatabase.CreateLatestAsync();
        var currentUser = new CurrentUserService();
        var service = new SupplierService(
            new SupplierRepository(database.Context),
            new SecurityAuditRepository(database.Context),
            new EfUnitOfWork(database.Context),
            new FixedClock(Now),
            currentUser);

        var created = await service.CreateAsync(new(" ncc-001 ", "Công ty A", "TAX01", "An", "0901", "a@example.invalid", "Hà Nội", "Ghi chú"));
        Assert.True(created.IsSuccess, created.AppError.Message);
        Assert.Equal("NCC-001", created.Value.Code.ToUpperInvariant());

        var duplicate = await service.CreateAsync(new("NCC-001", "Tên khác"));
        Assert.True(duplicate.IsFailure);
        Assert.Equal(ErrorCodes.Suppliers.CodeAlreadyExists, duplicate.AppError.Code);

        var page = await service.SearchAsync(new SupplierSearchRequest("ncc", true, 1, 20));
        Assert.True(page.IsSuccess);
        Assert.Single(page.Value.Items);

        var updated = await service.UpdateAsync(new(created.Value.Id, "NCC-001", "Công ty B", null, null, null, null, null, null, created.Value.UpdatedAtUtc));
        Assert.True(updated.IsSuccess, updated.AppError.Message);
        var inactive = await service.SetActiveStateAsync(new(created.Value.Id, false, updated.Value.UpdatedAtUtc));
        Assert.True(inactive.IsSuccess, inactive.AppError.Message);
        Assert.Single((await service.SearchAsync(new SupplierSearchRequest(null, false, 1, 20))).Value.Items);
        Assert.Single((await service.SearchAsync(new SupplierSearchRequest(null, null, 1, 20))).Value.Items);
        var reactivated = await service.SetActiveStateAsync(new(created.Value.Id, true, updated.Value.UpdatedAtUtc));
        Assert.True(reactivated.IsSuccess, reactivated.AppError.Message);
        var auditCountBeforeNoOp = await database.Context.SecurityAuditEvents.CountAsync();
        var noOp = await service.SetActiveStateAsync(new(created.Value.Id, true, updated.Value.UpdatedAtUtc));
        Assert.True(noOp.IsSuccess);
        Assert.Equal(auditCountBeforeNoOp, await database.Context.SecurityAuditEvents.CountAsync());
        var missingVersion = await service.UpdateAsync(new(created.Value.Id, "NCC-001", "Tên khác", null, null, null, null, null, null, default));
        Assert.True(missingVersion.IsFailure);
        Assert.Equal(ErrorCodes.Suppliers.ConcurrencyConflict, missingVersion.AppError.Code);

        var auditActions = await database.Context.SecurityAuditEvents.AsNoTracking().Select(item => item.Action).ToArrayAsync();
        Assert.Contains(SecurityAuditAction.SupplierCreated, auditActions);
        Assert.Contains(SecurityAuditAction.SupplierUpdated, auditActions);
        Assert.Contains(SecurityAuditAction.SupplierDeactivated, auditActions);
        Assert.Contains(SecurityAuditAction.SupplierReactivated, auditActions);
        var audit = await database.Context.SecurityAuditEvents.AsNoTracking().SingleAsync(item => item.Action == SecurityAuditAction.SupplierUpdated);
        Assert.Contains("Nhà cung cấp", audit.BusinessArea);
        Assert.DoesNotContain("0901", audit.BeforeValuesJson + audit.AfterValuesJson);
        Assert.DoesNotContain("a@example.invalid", audit.BeforeValuesJson + audit.AfterValuesJson);
    }

    [Fact]
    public async Task Duplicate_normalized_code_is_rejected_by_database_unique_index()
    {
        await using var database = await TestDatabase.CreateLatestAsync();
        database.Context.Suppliers.Add(new Supplier("NCC01", "A", null, null, null, null, null, null, Now));
        database.Context.Suppliers.Add(new Supplier(" ncc01 ", "B", null, null, null, null, null, null, Now));
        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Audit_action_range_rejects_values_outside_the_forward_migration()
    {
        await using var database = await TestDatabase.CreateLatestAsync();
        Assert.Throws<DomainException>(() =>
            database.Context.SecurityAuditEvents.Add(
                new SecurityAuditEvent(null, null, null, (SecurityAuditAction)25, "Success", Guid.NewGuid(), Now)));
    }

    [Fact]
    public async Task Failed_supplier_save_rolls_back_supplier_and_success_audit()
    {
        await using var database = await TestDatabase.CreateLatestAsync();
        var failingUnitOfWork = new FailingUnitOfWork();
        var service = new SupplierService(
            new SupplierRepository(database.Context),
            new SecurityAuditRepository(database.Context),
            failingUnitOfWork,
            new FixedClock(Now),
            new CurrentUserService());

        var result = await service.CreateAsync(new("NCC-ROLLBACK", "Rollback"));

        Assert.True(result.IsFailure);
        Assert.True(failingUnitOfWork.RolledBack);
        Assert.Empty(await database.Context.Suppliers.AsNoTracking().ToArrayAsync());
        Assert.Empty(await database.Context.SecurityAuditEvents.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task Previous_migration_upgrades_and_preserves_login_failed_action()
    {
        await using var database = await TestDatabase.CreateAtPreviousMigrationAsync();
        database.Context.SecurityAuditEvents.Add(new SecurityAuditEvent(null, null, null, SecurityAuditAction.LoginFailed, "Success", Guid.NewGuid(), Now));
        await database.Context.SaveChangesAsync();
        await database.Context.Database.MigrateAsync();
        Assert.Empty(await database.Context.Database.GetPendingMigrationsAsync());
        Assert.Contains(SecurityAuditAction.LoginFailed, await database.Context.SecurityAuditEvents.Select(item => item.Action).ToArrayAsync());
        database.Context.Suppliers.Add(new Supplier("NCC01", "A", null, null, null, null, null, null, Now));
        await database.Context.SaveChangesAsync();
        Assert.Equal("A", await database.Context.Suppliers.Select(item => item.Name).SingleAsync());
    }

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public bool RolledBack { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new PersistenceConflictException("Forced supplier save failure.");

        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IApplicationTransaction>(new FailingTransaction(this));

        private sealed class FailingTransaction(FailingUnitOfWork owner) : IApplicationTransaction
        {
            public bool IsCompleted { get; private set; }

            public Task CommitAsync(CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("Commit must not be reached.");

            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                owner.RolledBack = true;
                IsCompleted = true;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() =>
                IsCompleted ? ValueTask.CompletedTask : new(RollbackAsync());
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, PosDbContext context) { Connection = connection; Context = context; }
        public SqliteConnection Connection { get; }
        public PosDbContext Context { get; }

        public static async Task<TestDatabase> CreateLatestAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var context = CreateContext(connection);
            await context.Database.MigrateAsync();
            return new(connection, context);
        }

        public static async Task<TestDatabase> CreateAtPreviousMigrationAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var context = CreateContext(connection);
            var migrations = context.Database.GetMigrations().ToArray();
            var index = Array.FindIndex(migrations, migration => migration.Contains("AddSupplierMaster", StringComparison.Ordinal));
            Assert.True(index > 0);
            await context.Database.GetService<IMigrator>().MigrateAsync(migrations[index - 1]);
            return new(connection, context);
        }

        private static PosDbContext CreateContext(SqliteConnection connection) =>
            new(new DbContextOptionsBuilder<PosDbContext>().UseSqlite(connection).AddInterceptors(new AuditableEntityInterceptor()).Options);

        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await Connection.DisposeAsync(); }
    }
}
