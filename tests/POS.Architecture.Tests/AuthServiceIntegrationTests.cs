using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử toàn bộ hành vi đăng nhập của AuthService.
/// </summary>
public sealed class AuthServiceIntegrationTests
{
    private static readonly DateTimeOffset
        LoginAtUtc =
            new(
                2026,
                7,
                21,
                4,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task Login_must_require_username()
    {
        await using var database =
            await AuthTestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var fixture =
            CreateService(
                context);

        var result =
            await fixture.Service.LoginAsync(
                new LoginRequest(
                    username: " ",
                    password: "Admin@2026!"));

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Authentication
                .UsernameRequired,
            result.Error.Code);

        Assert.False(
            fixture.CurrentUser
                .IsAuthenticated);
    }

    [Fact]
    public async Task Login_must_require_password()
    {
        await using var database =
            await AuthTestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var fixture =
            CreateService(
                context);

        var result =
            await fixture.Service.LoginAsync(
                new LoginRequest(
                    username: "admin",
                    password: string.Empty));

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Authentication
                .PasswordRequired,
            result.Error.Code);
    }

    [Fact]
    public async Task Unknown_username_must_return_invalid_credentials()
    {
        await using var database =
            await AuthTestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var fixture =
            CreateService(
                context);

        var result =
            await fixture.Service.LoginAsync(
                new LoginRequest(
                    username: "unknown",
                    password: "WrongPassword"));

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Authentication
                .InvalidCredentials,
            result.Error.Code);

        Assert.False(
            fixture.CurrentUser
                .IsAuthenticated);
    }

    [Fact]
    public async Task Wrong_password_must_increment_failed_attempts()
    {
        await using var database =
            await AuthTestDatabase.CreateAsync();

        var userId =
            await database.SeedUserAsync(
                username: "admin",
                password: "Admin@2026!");

        await using (
            var loginContext =
                database.CreateContext())
        {
            var fixture =
                CreateService(
                    loginContext);

            var result =
                await fixture.Service.LoginAsync(
                    new LoginRequest(
                        username: "ADMIN",
                        password: "WrongPassword"));

            Assert.True(
                result.IsFailure);

            Assert.Equal(
                ErrorCodes.Authentication
                    .InvalidCredentials,
                result.Error.Code);

            Assert.False(
                fixture.CurrentUser
                    .IsAuthenticated);
        }

        await using var verificationContext =
            database.CreateContext();

        var persistedUser =
            await verificationContext.Users
                .SingleAsync(
                    user =>
                        user.Id == userId);

        Assert.Equal(
            1,
            persistedUser
                .FailedLoginAttempts);

        Assert.Null(
            persistedUser
                .LockedUntilUtc);
    }

    [Fact]
    public async Task Fifth_wrong_password_must_lock_account()
    {
        await using var database =
            await AuthTestDatabase.CreateAsync();

        var userId =
            await database.SeedUserAsync(
                username: "admin",
                password: "Admin@2026!",
                initialFailedAttempts: 4);

        await using (
            var loginContext =
                database.CreateContext())
        {
            var fixture =
                CreateService(
                    loginContext);

            var result =
                await fixture.Service.LoginAsync(
                    new LoginRequest(
                        username: "admin",
                        password: "WrongPassword"));

            Assert.True(
                result.IsFailure);

            Assert.Equal(
                ErrorCodes.Authentication
                    .AccountLocked,
                result.Error.Code);
        }

        await using var verificationContext =
            database.CreateContext();

        var persistedUser =
            await verificationContext.Users
                .SingleAsync(
                    user =>
                        user.Id == userId);

        Assert.Equal(
            5,
            persistedUser
                .FailedLoginAttempts);

        Assert.True(
            persistedUser.IsLocked(
                LoginAtUtc));

        Assert.Equal(
            LoginAtUtc.AddMinutes(15),
            persistedUser
                .LockedUntilUtc);
    }

    [Fact]
    public async Task Locked_account_must_reject_correct_password()
    {
        await using var database =
            await AuthTestDatabase.CreateAsync();

        var userId =
            await database.SeedUserAsync(
                username: "admin",
                password: "Admin@2026!",
                initialFailedAttempts: 5);

        await using (
            var loginContext =
                database.CreateContext())
        {
            var fixture =
                CreateService(
                    loginContext);

            var result =
                await fixture.Service.LoginAsync(
                    new LoginRequest(
                        username: "admin",
                        password: "Admin@2026!"));

            Assert.True(
                result.IsFailure);

            Assert.Equal(
                ErrorCodes.Authentication
                    .AccountLocked,
                result.Error.Code);

            Assert.False(
                fixture.CurrentUser
                    .IsAuthenticated);
        }

        await using var verificationContext =
            database.CreateContext();

        var persistedUser =
            await verificationContext.Users
                .SingleAsync(
                    user =>
                        user.Id == userId);

        Assert.Null(
            persistedUser.LastLoginAtUtc);
    }

    [Fact]
    public async Task Inactive_account_must_be_rejected()
    {
        await using var database =
            await AuthTestDatabase.CreateAsync();

        await database.SeedUserAsync(
            username: "cashier",
            password: "Cashier@2026!",
            isActive: false);

        await using var context =
            database.CreateContext();

        var fixture =
            CreateService(
                context);

        var result =
            await fixture.Service.LoginAsync(
                new LoginRequest(
                    username: "cashier",
                    password: "Cashier@2026!"));

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Authentication
                .AccountInactive,
            result.Error.Code);

        Assert.False(
            fixture.CurrentUser
                .IsAuthenticated);
    }

    [Fact]
    public async Task Successful_login_must_update_user_and_session()
    {
        await using var database =
            await AuthTestDatabase.CreateAsync();

        var userId =
            await database.SeedUserAsync(
                username: "Admin.Manager",
                password: "Admin@2026!",
                initialFailedAttempts: 2);

        AuthServiceFixture fixture;

        await using (
            var loginContext =
                database.CreateContext())
        {
            fixture =
                CreateService(
                    loginContext);

            var result =
                await fixture.Service.LoginAsync(
                    new LoginRequest(
                        username:
                            "  ADMIN.manager  ",

                        password:
                            "Admin@2026!"));

            Assert.True(
                result.IsSuccess);

            Assert.Equal(
                userId,
                result.Value.Id);

            Assert.Equal(
                "Admin.Manager",
                result.Value.Username);

            Assert.Equal(
                Role.Administrator,
                result.Value.Role);

            Assert.True(
                fixture.CurrentUser
                    .IsAuthenticated);

            Assert.Equal(
                userId,
                fixture.CurrentUser.UserId);
        }

        await using var verificationContext =
            database.CreateContext();

        var persistedUser =
            await verificationContext.Users
                .SingleAsync(
                    user =>
                        user.Id == userId);

        Assert.Equal(
            0,
            persistedUser
                .FailedLoginAttempts);

        Assert.Null(
            persistedUser
                .LockedUntilUtc);

        Assert.Equal(
            LoginAtUtc,
            persistedUser
                .LastLoginAtUtc);

        var logoutResult =
            fixture.Service.Logout();

        Assert.True(
            logoutResult.IsSuccess);

        Assert.False(
            fixture.CurrentUser
                .IsAuthenticated);
    }

    private static AuthServiceFixture
        CreateService(
            PosDbContext context)
    {
        var repository =
            new UserRepository(
                context);

        var passwordHasher =
            new TestPasswordHasher();

        var unitOfWork =
            new EfUnitOfWork(
                context);

        var currentUser =
            new CurrentUserService();

        var clock =
            new FixedClock(
                LoginAtUtc);

        var service =
            new AuthService(
                repository,
                passwordHasher,
                unitOfWork,
                currentUser,
                clock);

        return new AuthServiceFixture(
            service,
            currentUser);
    }

    private sealed record AuthServiceFixture(
        AuthService Service,
        CurrentUserService CurrentUser);

    private sealed class TestPasswordHasher :
        IPasswordHasher
    {
        public string HashPassword(
            string password)
        {
            if (string.IsNullOrEmpty(
                    password))
            {
                throw new ArgumentException(
                    "Password is required.",
                    nameof(password));
            }

            return $"TEST-HASH::{password}";
        }

        public bool VerifyPassword(
            string password,
            string passwordHash)
        {
            return string.Equals(
                passwordHash,
                $"TEST-HASH::{password}",
                StringComparison.Ordinal);
        }
    }

    private sealed class FixedClock :
        IClock
    {
        public FixedClock(
            DateTimeOffset utcNow)
        {
            UtcNow =
                utcNow.ToUniversalTime();
        }

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class AuthTestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly DbContextOptions<
            PosDbContext>
            _options;

        private AuthTestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<
            AuthTestDatabase>
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
                new AuthTestDatabase(
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

        public async Task<int> SeedUserAsync(
            string username,
            string password,
            bool isActive = true,
            int initialFailedAttempts = 0)
        {
            await using var context =
                CreateContext();

            var passwordHasher =
                new TestPasswordHasher();

            var createdAtUtc =
                LoginAtUtc.AddHours(-1);

            var user =
                new User(
                    username:
                        username,

                    passwordHash:
                        passwordHasher
                            .HashPassword(
                                password),

                    fullName:
                        "Người dùng kiểm thử",

                    role:
                        Role.Administrator,

                    utcNow:
                        createdAtUtc);

            for (var index = 0;
                 index < initialFailedAttempts;
                 index++)
            {
                /*
                 * Lần cuối cùng xảy ra một phút trước LoginAtUtc.
                 * Nếu có đủ 5 lần sai, tài khoản vẫn đang bị khóa.
                 */
                var failedAtUtc =
                    LoginAtUtc.AddMinutes(
                        index -
                        initialFailedAttempts);

                user.RegisterFailedLogin(
                    failedAtUtc,
                    TimeSpan.FromMinutes(15));
            }

            if (!isActive)
            {
                user.Deactivate(
                    LoginAtUtc.AddMinutes(-1));
            }

            context.Users.Add(
                user);

            await context
                .SaveChangesAsync();

            return user.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await _connection
                .DisposeAsync();

            GC.SuppressFinalize(
                this);
        }
    }
}