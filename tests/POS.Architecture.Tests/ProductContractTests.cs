using POS.Domain.Entities;
using System;
using System.Reflection;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Khóa contract quan trọng của Product.
///
/// Đây chưa thay thế unit test nghiệp vụ chi tiết,
/// nhưng ngăn những lần refactor vô tình đổi kiểu tiền,
/// tồn kho hoặc mở public setter.
/// </summary>
public sealed class ProductContractTests
{
    [Theory]
    [InlineData("CategoryId", typeof(int))]
    [InlineData("Code", typeof(string))]
    [InlineData("Barcode", typeof(string))]
    [InlineData("Name", typeof(string))]
    [InlineData("UnitName", typeof(string))]
    [InlineData("CostPrice", typeof(long))]
    [InlineData("SalePrice", typeof(long))]
    [InlineData("StockQuantity", typeof(int))]
    [InlineData("MinimumStock", typeof(int))]
    [InlineData("TrackInventory", typeof(bool))]
    [InlineData("AllowNegativeStock", typeof(bool))]
    [InlineData("IsActive", typeof(bool))]
    public void Product_property_contract_must_remain_stable(
        string propertyName,
        Type expectedType)
    {
        var property =
            typeof(Product).GetProperty(
                propertyName,
                BindingFlags.Public |
                BindingFlags.Instance);

        Assert.NotNull(property);

        Assert.Equal(
            expectedType,
            property.PropertyType);
    }

    [Theory]
    [InlineData("CategoryId")]
    [InlineData("Code")]
    [InlineData("Barcode")]
    [InlineData("Name")]
    [InlineData("UnitName")]
    [InlineData("CostPrice")]
    [InlineData("SalePrice")]
    [InlineData("StockQuantity")]
    [InlineData("MinimumStock")]
    [InlineData("TrackInventory")]
    [InlineData("AllowNegativeStock")]
    [InlineData("IsActive")]
    public void Product_state_must_not_have_public_setters(
        string propertyName)
    {
        var property =
            typeof(Product).GetProperty(
                propertyName,
                BindingFlags.Public |
                BindingFlags.Instance);

        Assert.NotNull(property);

        var setter =
            property.SetMethod;

        Assert.True(
            setter is null ||
            !setter.IsPublic,
            $"Product.{propertyName} không được có public setter.");
    }

    [Fact]
    public void Product_money_must_use_integer_vnd_representation()
    {
        var costPriceProperty =
            typeof(Product).GetProperty(
                "CostPrice");

        var salePriceProperty =
            typeof(Product).GetProperty(
                "SalePrice");

        Assert.NotNull(
            costPriceProperty);

        Assert.NotNull(
            salePriceProperty);

        Assert.Equal(
            typeof(long),
            costPriceProperty.PropertyType);

        Assert.Equal(
            typeof(long),
            salePriceProperty.PropertyType);

        Assert.NotEqual(
            typeof(double),
            costPriceProperty.PropertyType);

        Assert.NotEqual(
            typeof(double),
            salePriceProperty.PropertyType);
    }
}