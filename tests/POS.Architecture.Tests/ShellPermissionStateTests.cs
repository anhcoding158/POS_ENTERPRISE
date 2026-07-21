using POS.Application.DTOs.Authentication;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Wpf.Authorization;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm tra trạng thái quyền mà ShellWindow dùng
/// để bật hoặc khóa các chức năng giao diện.
/// </summary>
public sealed class ShellPermissionStateTests
{
    private static readonly DateTimeOffset
        AuthenticatedAtUtc =
            new(
                2026,
                7,
                21,
                9,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public void Administrator_must_have_all_shell_permissions()
    {
        var state =
            CreateState(
                Role.Administrator);

        Assert.True(state.CanViewProductCatalog);
        Assert.True(state.CanManageProducts);
        Assert.True(state.CanManageCategories);
        Assert.True(state.CanAdjustInventory);
        Assert.True(state.CanViewInventoryHistory);
        Assert.True(state.CanUseCheckout);
        Assert.True(state.CanViewReports);
        Assert.True(state.CanManageUsers);
    }

    [Fact]
    public void Manager_must_manage_operations_but_not_users()
    {
        var state =
            CreateState(
                Role.Manager);

        Assert.True(state.CanViewProductCatalog);
        Assert.True(state.CanManageProducts);
        Assert.True(state.CanManageCategories);
        Assert.True(state.CanAdjustInventory);
        Assert.True(state.CanViewInventoryHistory);
        Assert.True(state.CanUseCheckout);
        Assert.True(state.CanViewReports);
        Assert.False(state.CanManageUsers);
    }

    [Fact]
    public void Cashier_must_only_access_catalog_and_checkout()
    {
        var state =
            CreateState(
                Role.Cashier);

        Assert.True(state.CanViewProductCatalog);
        Assert.True(state.CanUseCheckout);

        Assert.False(state.CanManageProducts);
        Assert.False(state.CanManageCategories);
        Assert.False(state.CanAdjustInventory);
        Assert.False(state.CanViewInventoryHistory);
        Assert.False(state.CanViewReports);
        Assert.False(state.CanManageUsers);
    }

    [Fact]
    public void Inventory_staff_must_only_access_catalog_and_inventory()
    {
        var state =
            CreateState(
                Role.InventoryStaff);

        Assert.True(state.CanViewProductCatalog);
        Assert.True(state.CanAdjustInventory);
        Assert.True(state.CanViewInventoryHistory);

        Assert.False(state.CanManageProducts);
        Assert.False(state.CanManageCategories);
        Assert.False(state.CanUseCheckout);
        Assert.False(state.CanViewReports);
        Assert.False(state.CanManageUsers);
    }

    private static ShellPermissionState CreateState(
        Role role)
    {
        var currentUserService =
            new CurrentUserService();

        currentUserService.SetCurrentUser(
            new AuthenticatedUserDto(
                1,
                $"shell.{role}",
                $"Người dùng {role}",
                role,
                AuthenticatedAtUtc));

        var permissionService =
            new PermissionService(
                currentUserService);

        return ShellPermissionState.Create(
            permissionService);
    }
}