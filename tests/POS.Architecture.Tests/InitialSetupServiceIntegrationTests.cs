using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử thiết lập Administrator lần đầu.
/// </summary>
public sealed class
    InitialSetupServiceIntegrationTests
{
    private static readonly DateTimeOffset
        SetupAtUtc =
            new(
                2026,
                7,
                21,
                6,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task Empty_database_must_require_setup()
    {
        await using var database =
            await SetupTestDatabase
                .CreateAsync();

        await using var context =
            database.CreateContext();

        var fixture =
            CreateService(
                context);

        var result =
            await fixture.Service
                .IsSetupRequiredAsync();

        Assert.True(
            result.IsSuccess);

        Assert.True(
            result.Value);
    }

    [Fact]
    public async Task Creating_admin_must_persist_and_authenticate()
    {
        await using var database =
            await SetupTestDatabase
                .CreateAsync();

        CurrentUserService currentUser;

        await using (
            var context =
                database.CreateContext())
        {
            var fixture =
                CreateService(
                    context);

            currentUser =
                fixture.CurrentUser;

            var result =
                await fixture.Service
                    .CreateInitialAdministratorAsync(
                        ValidRequest());

            Assert.True(
                result.IsSuccess);

            Assert.Equal(
                "admin.owner",
                result.Value.Username);

            Assert.Equal(
                Role.Administrator,
                result.Value.Role);

            Assert.True(
                currentUser.IsAuthenticated);

            Assert.Equal(
                result.Value.Id,
                currentUser.UserId);
        }

        await using var verificationContext =
            database.CreateContext();

        var user =
            await verificationContext.Users
                .SingleAsync();

        Assert.Equal(
            "admin.owner",
            user.Username);

        Assert.Equal(
            "ADMIN.OWNER",
            user.NormalizedUsername);

        Assert.Equal(
            "TEST-HASH::Strong@2026",
            user.PasswordHash);

        Assert.True(
            user.IsActive);

        Assert.Equal(
            Role.Administrator,
            user.Role);
    }

    [Fact]
    public async Task Setup_must_become_complete_after_admin_created()
    {
        await using var database =
            await SetupTestDatabase
                .CreateAsync();

        await using var context =
            database.CreateContext();

        var fixture =
            CreateService(
                context);

        var createResult =
            await fixture.Service
                .CreateInitialAdministratorAsync(
                    ValidRequest());

        Assert.True(
            createResult.IsSuccess);

        var setupResult =
            await fixture.Service
                .IsSetupRequiredAsync();

        Assert.True(
            setupResult.IsSuccess);

        Assert.False(
            setupResult.Value);
    }

    [Fact]
    public async Task Second_initial_admin_must_be_rejected()
    {
        await using var database =
            await SetupTestDatabase
                .CreateAsync();

        await using var context =
            database.CreateContext();

        var fixture =
            CreateService(
                context);

        var firstResult =
            await fixture.Service
                .CreateInitialAdministratorAsync(
                    ValidRequest());

        Assert.True(
            firstResult.IsSuccess);

        var secondResult =
            await fixture.Service
                .CreateInitialAdministratorAsync(
                    new InitialAdministratorRequest(
                        username:
                            "second.admin",

                        fullName:
                            "Quản trị viên thứ hai",

                        password:
                            "Second@2026",

                        confirmPassword:
                            "Second@2026"));

        Assert.True(
            secondResult.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Conflict,
            secondResult.Error.Code);

        Assert.Equal(
            1,
            await context.Users.CountAsync());
    }

    [Fact]
    public async Task Weak_password_must_not_create_user()
    {
        await using var database =
            await SetupTestDatabase
                .CreateAsync();

        await using var context =
            database.CreateContext();

        var fixture =
            CreateService(
                context);

        var result =
            await fixture.Service
                .CreateInitialAdministratorAsync(
                    new InitialAdministratorRequest(
                        username:
                            "admin",

                        fullName:
                            "Quản trị viên",

                        password:
                            "123456",

                        confirmPassword:
                            "123456"));

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Validation,
            result.Error.Code);

        Assert.False(
            await context.Users.AnyAsync());

        Assert.False(
            fixture.CurrentUser
                .IsAuthenticated);
    }

    [Fact]
    public async Task Password_confirmation_must_match()
    {
        await using var database =
            await SetupTestDatabase
                .CreateAsync();

        await using var context =
            database.CreateContext();

        var fixture =
            CreateService(
                context);

        var result =
            await fixture.Service
                .CreateInitialAdministratorAsync(
                    new InitialAdministratorRequest(
                        username:
                            "admin",

                        fullName:
                            "Quản trị viên",

                        password:
                            "Strong@2026",

                        confirmPassword:
                            "Different@2026"));

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Validation,
            result.Error.Code);

        Assert.False(
            await context.Users.AnyAsync());
    }

    private static
        InitialAdministratorRequest
        ValidRequest()
    {
        return new InitialAdministratorRequest(
            username:
                "admin.owner",

            fullName:
                "Chủ cửa hàng",

            password:
                "Strong@2026",

            confirmPassword:
                "Strong@2026");
    }

    private static SetupServiceFixture
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
                SetupAtUtc);

        var service =
            new InitialSetupService(
                repository,
                passwordHasher,
                unitOfWork,
                currentUser,
                clock);

        return new SetupServiceFixture(
            service,
            currentUser);
    }

    private sealed record SetupServiceFixture(
        InitialSetupService Service,
        CurrentUserService CurrentUser);

    private sealed class TestPasswordHasher :
        IPasswordHasher
    {
        public string HashPassword(
            string password)
        {
            return
                $"TEST-HASH::{password}";
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

    private sealed class SetupTestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly DbContextOptions<
            PosDbContext>
            _options;

        private SetupTestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<
            SetupTestDatabase>
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
                new SetupTestDatabase(
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
            await _connection
                .DisposeAsync();

            GC.SuppressFinalize(
                this);
        }
    }
}