using POS.Domain.Common;
using POS.Domain.Entities;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductArchivingDomainTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            2026,
            7,
            27,
            8,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Archive_must_deactivate_product_and_record_actor_and_time()
    {
        var product = CreateProduct();
        var archivedAtUtc = CreatedAtUtc.AddHours(1);

        product.Archive(
            archivedByUserId: 7,
            archivedAtUtc);

        Assert.True(product.IsArchived);
        Assert.False(product.IsActive);
        Assert.Equal(archivedAtUtc, product.ArchivedAtUtc);
        Assert.Equal(7, product.ArchivedByUserId);
        Assert.Equal(archivedAtUtc, product.UpdatedAtUtc);
    }

    [Fact]
    public void Archive_must_keep_stock_prices_image_and_identity_unchanged()
    {
        var product = CreateProduct();

        product.Archive(
            archivedByUserId: 7,
            CreatedAtUtc.AddHours(1));

        Assert.Equal(1, product.CategoryId);
        Assert.Equal("ARCHIVE-001", product.Code);
        Assert.Equal("8938500000001", product.Barcode);
        Assert.Equal("Sản phẩm lưu trữ", product.Name);
        Assert.Equal(10_000, product.CostPrice);
        Assert.Equal(15_000, product.SalePrice);
        Assert.Equal(12, product.StockQuantity);
        Assert.Equal(
            @"C:\Products\archive-001.png",
            product.ImagePath);
    }

    [Fact]
    public void Archive_must_reject_invalid_actor_id()
    {
        var product = CreateProduct();

        var exception =
            Assert.Throws<DomainException>(
                () =>
                    product.Archive(
                        archivedByUserId: 0,
                        CreatedAtUtc.AddHours(1)));

        Assert.Equal(
            "PRODUCT.INVALID_ARCHIVED_BY_USER_ID",
            exception.Code);
        Assert.False(product.IsArchived);
    }

    [Fact]
    public void Archive_must_reject_product_already_archived()
    {
        var product = CreateProduct();
        var originalArchivedAtUtc =
            CreatedAtUtc.AddHours(1);

        product.Archive(
            archivedByUserId: 7,
            originalArchivedAtUtc);

        var exception =
            Assert.Throws<DomainException>(
                () =>
                    product.Archive(
                        archivedByUserId: 9,
                        CreatedAtUtc.AddHours(2)));

        Assert.Equal(
            "PRODUCT.ALREADY_ARCHIVED",
            exception.Code);
        Assert.Equal(
            originalArchivedAtUtc,
            product.ArchivedAtUtc);
        Assert.Equal(7, product.ArchivedByUserId);
    }

    [Fact]
    public void Restore_must_clear_archive_metadata_and_keep_product_inactive()
    {
        var product = CreateProduct();
        product.Archive(
            archivedByUserId: 7,
            CreatedAtUtc.AddHours(1));

        var restoredAtUtc =
            CreatedAtUtc.AddHours(2);

        product.Restore(restoredAtUtc);

        Assert.False(product.IsArchived);
        Assert.False(product.IsActive);
        Assert.Null(product.ArchivedAtUtc);
        Assert.Null(product.ArchivedByUserId);
        Assert.Equal(restoredAtUtc, product.UpdatedAtUtc);
    }

    [Fact]
    public void Restore_must_reject_product_not_archived()
    {
        var product = CreateProduct();

        var exception =
            Assert.Throws<DomainException>(
                () =>
                    product.Restore(
                        CreatedAtUtc.AddHours(1)));

        Assert.Equal(
            "PRODUCT.NOT_ARCHIVED",
            exception.Code);
    }

    [Fact]
    public void Activate_must_reject_archived_product()
    {
        var product = CreateProduct();
        product.Archive(
            archivedByUserId: 7,
            CreatedAtUtc.AddHours(1));

        var exception =
            Assert.Throws<DomainException>(
                () =>
                    product.Activate(
                        CreatedAtUtc.AddHours(2)));

        Assert.Equal(
            "PRODUCT.ARCHIVED_CANNOT_ACTIVATE",
            exception.Code);
        Assert.True(product.IsArchived);
        Assert.False(product.IsActive);
    }

    [Fact]
    public void Deactivate_must_not_clear_archive_metadata()
    {
        var product = CreateProduct();
        var archivedAtUtc =
            CreatedAtUtc.AddHours(1);

        product.Archive(
            archivedByUserId: 7,
            archivedAtUtc);

        product.Deactivate(
            CreatedAtUtc.AddHours(2));

        Assert.True(product.IsArchived);
        Assert.Equal(
            archivedAtUtc,
            product.ArchivedAtUtc);
        Assert.Equal(7, product.ArchivedByUserId);
    }

    [Fact]
    public void Archive_and_restore_must_normalize_time_to_utc()
    {
        var product = CreateProduct();
        var localArchiveTime =
            new DateTimeOffset(
                2026,
                7,
                27,
                16,
                0,
                0,
                TimeSpan.FromHours(7));

        product.Archive(
            archivedByUserId: 7,
            localArchiveTime);

        Assert.Equal(
            TimeSpan.Zero,
            product.ArchivedAtUtc?.Offset);
        Assert.Equal(
            localArchiveTime.ToUniversalTime(),
            product.ArchivedAtUtc);

        var localRestoreTime =
            localArchiveTime.AddHours(1);

        product.Restore(localRestoreTime);

        Assert.Equal(
            localRestoreTime.ToUniversalTime(),
            product.UpdatedAtUtc);
        Assert.Equal(
            TimeSpan.Zero,
            product.UpdatedAtUtc.Offset);
    }

    private static Product CreateProduct()
    {
        return new Product(
            categoryId: 1,
            code: "ARCHIVE-001",
            name: "Sản phẩm lưu trữ",
            unitName: "Cái",
            costPrice: 10_000,
            salePrice: 15_000,
            stockQuantity: 12,
            minimumStock: 2,
            trackInventory: true,
            allowNegativeStock: false,
            CreatedAtUtc,
            barcode: "8938500000001",
            description: "Dùng kiểm thử archive.",
            imagePath: @"C:\Products\archive-001.png");
    }
}
