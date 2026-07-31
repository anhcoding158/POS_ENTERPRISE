using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử ma trận quyền và trạng thái phiên.
/// </summary>
public sealed class PermissionServiceTests
{
    private static readonly DateTimeOffset
        AuthenticatedAtUtc =
            new(
                2026,
                7,
                21,
                7,
                0,
                0,
                TimeSpan.Zero);

    private static readonly SystemCapability[]
        AllPermissions =
            Enum.GetValues<
                SystemCapability>();

    [Fact]
    public void Administrator_must_have_every_permission()
    {
        var service =
            CreateAuthenticatedService(
                Role.Administrator);

        Assert.All(
            AllPermissions,
            permission =>
                Assert.True(
                    service.HasPermission(
                        permission)));
    }

    [Fact]
    public void Manager_must_have_operational_permissions_but_not_user_management()
    {
        var service =
            CreateAuthenticatedService(
                Role.Manager);

        var expected =
            new HashSet<SystemCapability>
            {
                SystemCapability.ViewProductCatalog,
                SystemCapability.ManageProducts,
                SystemCapability.ManageCategories,
                SystemCapability.ViewInventoryHistory,
                SystemCapability.AdjustInventory,
                SystemCapability.UseCheckout,
            SystemCapability.ViewReports,
            SystemCapability.ProcessReturns,
            SystemCapability.ApplySalesDiscount
            };

        AssertPermissionSet(
            service,
            expected);
    }

    [Fact]
    public void Cashier_must_only_access_catalog_and_checkout()
    {
        var service =
            CreateAuthenticatedService(
                Role.Cashier);

        var expected =
            new HashSet<SystemCapability>
            {
                SystemCapability.ViewProductCatalog,
                SystemCapability.UseCheckout
            };

        AssertPermissionSet(
            service,
            expected);
    }

    [Fact]
    public void Inventory_staff_must_only_access_inventory_work()
    {
        var service =
            CreateAuthenticatedService(
                Role.InventoryStaff);

        var expected =
            new HashSet<SystemCapability>
            {
                SystemCapability.ViewProductCatalog,
                SystemCapability.ViewInventoryHistory,
                SystemCapability.AdjustInventory
            };

        AssertPermissionSet(
            service,
            expected);
    }

    [Fact]
    public void Unauthenticated_session_must_return_unauthorized()
    {
        var currentUser =
            new CurrentUserService();

        var service =
            new PermissionService(
                currentUser);

        var result =
            service.Authorize(
                SystemCapability.ManageProducts);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Unauthorized,
            result.AppError.Code);

        Assert.False(
            service.HasPermission(
                SystemCapability.ManageProducts));
    }

    [Fact]
    public void Allowed_permission_must_return_success()
    {
        var service =
            CreateAuthenticatedService(
                Role.InventoryStaff);

        var result =
            service.Authorize(
                SystemCapability.AdjustInventory);

        Assert.True(
            result.IsSuccess);
    }

    [Fact]
    public void Denied_permission_must_return_forbidden()
    {
        var service =
            CreateAuthenticatedService(
                Role.Cashier);

        var result =
            service.Authorize(
                SystemCapability.AdjustInventory);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.General.Forbidden,
            result.AppError.Code);

        Assert.Contains(
            "điều chỉnh tồn kho",
            result.AppError.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_permission_must_be_rejected()
    {
        var service =
            CreateAuthenticatedService(
                Role.Administrator);

        var invalidPermission =
            (SystemCapability)999;

        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    service.HasPermission(
                        invalidPermission));

        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    service.Authorize(
                        invalidPermission));
    }

    [Fact]
    public void Invalid_role_must_be_rejected()
    {
        var invalidRole =
            (Role)999;

        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    RolePermissionPolicy
                        .HasPermission(
                            invalidRole,
                            SystemCapability
                                .ViewProductCatalog));
    }

    private static PermissionService
        CreateAuthenticatedService(
            Role role)
    {
        var currentUser =
            new CurrentUserService();

        currentUser.SetCurrentUser(
            new AuthenticatedUserDto(
                id:
                    1,

                username:
                    $"test.{role}",

                fullName:
                    $"Người dùng {role}",

                role:
                    role,

                authenticatedAtUtc:
                    AuthenticatedAtUtc));

        return new PermissionService(
            currentUser);
    }

    private static void AssertPermissionSet(
        PermissionService service,
        HashSet<SystemCapability>
            expectedPermissions)
    {
        foreach (var permission in
                 AllPermissions)
        {
            Assert.Equal(
                expectedPermissions.Contains(
                    permission),

                service.HasPermission(
                    permission));
        }
    }
}
