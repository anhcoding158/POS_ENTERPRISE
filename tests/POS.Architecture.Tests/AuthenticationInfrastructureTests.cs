using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử nền tảng Authentication:
/// - BCrypt;
/// - CurrentUser session;
/// - User EF mapping;
/// - unique username;
/// - repository search.
/// </summary>
public sealed class AuthenticationInfrastructureTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                21,
                1,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public void Bcrypt_must_hash_and_verify_password()
    {
        var hasher =
            new BCryptPasswordHasher();

        const string password =
            "Admin@2026!";

        var passwordHash =
            hasher.HashPassword(
                password);

        Assert.NotEqual(
            password,
            passwordHash);

        Assert.True(
            hasher.VerifyPassword(
                password,
                passwordHash));

        Assert.False(
            hasher.VerifyPassword(
                "WrongPassword",
                passwordHash));
    }

    [Fact]
    public void Bcrypt_must_fail_safely_for_invalid_hash()
    {
        var hasher =
            new BCryptPasswordHasher();

        var result =
            hasher.VerifyPassword(
                "Admin@2026!",
                "not-a-valid-bcrypt-hash");

        Assert.False(result);
    }

    [Fact]
    public void Current_user_service_must_set_and_clear_session()
    {
        var service =
            new CurrentUserService();

        var user =
            new AuthenticatedUserDto(
                id: 7,
                username: "admin",
                fullName: "Quản trị viên",
                role: Role.Administrator,
                authenticatedAtUtc:
                    CreatedAtUtc);

        service.SetCurrentUser(
            user);

        Assert.True(
            service.IsAuthenticated);

        Assert.Equal(
            7,
            service.UserId);

        Assert.Equal(
            "admin",
            service.Username);

        Assert.Equal(
            "Quản trị viên",
            service.FullName);

        Assert.True(
            service.IsInRole(
                Role.Administrator));

        Assert.False(
            service.IsInRole(
                Role.Cashier));

        service.Clear();

        Assert.False(
            service.IsAuthenticated);

        Assert.Null(
            service.CurrentUser);
    }

    [Fact]
    public async Task Repository_must_find_normalized_username()
    {
        await using var database =
            await AuthenticationTestDatabase
                .CreateAsync();

        var hasher =
            new BCryptPasswordHasher();

        await using (
            var seedContext =
                database.CreateContext())
        {
            var user =
                new User(
                    username:
                        "Admin.Manager",

                    passwordHash:
                        hasher.HashPassword(
                            "Admin@2026!"),

                    fullName:
                        "Quản trị viên",

                    role:
                        Role.Administrator,

                    utcNow:
                        CreatedAtUtc);

            seedContext.Users.Add(
                user);

            await seedContext
                .SaveChangesAsync();
        }

        await using var context =
            database.CreateContext();

        var repository =
            new UserRepository(
                context);

        var found =
            await repository
                .GetByNormalizedUsernameAsync(
                    "  admin.manager  ");

        Assert.NotNull(found);

        Assert.Equal(
            "Admin.Manager",
            found.Username);

        Assert.Equal(
            "ADMIN.MANAGER",
            found.NormalizedUsername);
    }

    [Fact]
    public async Task Database_must_reject_duplicate_normalized_username()
    {
        await using var database =
            await AuthenticationTestDatabase
                .CreateAsync();

        var hasher =
            new BCryptPasswordHasher();

        await using var context =
            database.CreateContext();

        var repository =
            new UserRepository(
                context);

        var unitOfWork =
            new EfUnitOfWork(
                context);

        var firstUser =
            new User(
                username:
                    "Admin",

                passwordHash:
                    hasher.HashPassword(
                        "First@2026!"),

                fullName:
                    "Admin thứ nhất",

                role:
                    Role.Administrator,

                utcNow:
                    CreatedAtUtc);

        await repository.AddAsync(
            firstUser);

        await unitOfWork
            .SaveChangesAsync();

        var duplicateUser =
            new User(
                username:
                    "ADMIN",

                passwordHash:
                    hasher.HashPassword(
                        "Second@2026!"),

                fullName:
                    "Admin thứ hai",

                role:
                    Role.Administrator,

                utcNow:
                    CreatedAtUtc.AddMinutes(1));

        await repository.AddAsync(
            duplicateUser);

        var exception =
            await Assert.ThrowsAsync<
                PersistenceConflictException>(
                () =>
                    unitOfWork
                        .SaveChangesAsync());

        Assert.Equal(
            PersistenceConflictKind
                .UniqueConstraint,
            exception.Kind);

        Assert.Equal(
            PersistenceConflictTargets
                .UserNormalizedUsername,
            exception.Target);
    }

    [Fact]
    public async Task User_search_must_filter_and_paginate()
    {
        await using var database =
            await AuthenticationTestDatabase
                .CreateAsync();

        var hasher =
            new BCryptPasswordHasher();

        await using (
            var seedContext =
                database.CreateContext())
        {
            var passwordHash =
                hasher.HashPassword(
                    "Admin@2026!");

            var admin =
                new User(
                    "admin",
                    passwordHash,
                    "Nguyễn Quản Trị",
                    Role.Administrator,
                    CreatedAtUtc);

            var manager =
                new User(
                    "manager",
                    passwordHash,
                    "Trần Quản Lý",
                    Role.Manager,
                    CreatedAtUtc);

            var cashier =
                new User(
                    "cashier",
                    passwordHash,
                    "Lê Thu Ngân",
                    Role.Cashier,
                    CreatedAtUtc);

            cashier.Deactivate(
                CreatedAtUtc.AddMinutes(1));

            seedContext.Users.AddRange(
                admin,
                manager,
                cashier);

            await seedContext
                .SaveChangesAsync();
        }

        await using var context =
            database.CreateContext();

        var repository =
            new UserRepository(
                context);

        var activePage =
            await repository.SearchAsync(
                searchTerm: null,
                role: null,
                isActive: true,
                pageNumber: 1,
                pageSize: 10);

        Assert.Equal(
            2,
            activePage.TotalCount);

        Assert.All(
            activePage.Items,
            user =>
                Assert.True(
                    user.IsActive));

        var managerPage =
            await repository.SearchAsync(
                searchTerm:
                    "Quản Lý",

                role:
                    Role.Manager,

                isActive:
                    true,

                pageNumber:
                    1,

                pageSize:
                    10);

    var foundManager =
        Assert.Single(
            managerPage.Items);

        Assert.Equal(
            "manager",
            foundManager.Username);
    }

    [Fact]
    public void User_domain_must_lock_after_failed_login_limit()
    {
        var hasher =
            new BCryptPasswordHasher();

        var user =
            new User(
                username:
                    "admin",

                passwordHash:
                    hasher.HashPassword(
                        "Admin@2026!"),

                fullName:
                    "Quản trị viên",

                role:
                    Role.Administrator,

                utcNow:
                    CreatedAtUtc);

        var lockDuration =
            TimeSpan.FromMinutes(15);

        for (var attempt = 1;
             attempt <= 5;
             attempt++)
        {
            user.RegisterFailedLogin(
                CreatedAtUtc.AddMinutes(
                    attempt),
                lockDuration);
        }

        Assert.Equal(
            5,
            user.FailedLoginAttempts);

        Assert.True(
            user.IsLocked(
                CreatedAtUtc.AddMinutes(6)));

        Assert.Equal(
            CreatedAtUtc.AddMinutes(20),
            user.LockedUntilUtc);
    }

    private sealed class
        AuthenticationTestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly DbContextOptions<
            PosDbContext>
            _options;

        private AuthenticationTestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<
            AuthenticationTestDatabase>
            CreateAsync()
        {
            var connection =
                new SqliteConnection(
                    "Data Source=:memory:");

            await connection.OpenAsync();

            var options =
                new DbContextOptionsBuilder<
                    PosDbContext>()
                    .UseSqlite(
                        connection)
                    .EnableDetailedErrors()
                    .Options;

            var database =
                new AuthenticationTestDatabase(
                    connection,
                    options);

            await using var context =
                database.CreateContext();

            await context.Database
                .EnsureCreatedAsync();

            return database;
        }

        public PosDbContext CreateContext()
        {
            return new PosDbContext(
                _options);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();

            GC.SuppressFinalize(this);
        }
    }
}