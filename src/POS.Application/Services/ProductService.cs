using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Products;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

/// <summary>
/// Triển khai các use case quản lý sản phẩm.
///
/// Service chịu trách nhiệm:
/// - kiểm tra dữ liệu đầu vào;
/// - kiểm tra danh mục;
/// - kiểm tra trùng mã và barcode;
/// - gọi Domain để áp dụng quy tắc nghiệp vụ;
/// - tạo OpeningBalance khi có tồn đầu kỳ;
/// - lưu Product và InventoryMovement trong cùng transaction;
/// - ánh xạ entity sang DTO.
/// </summary>
public sealed class ProductService :
    IProductService
{
    private const string UnknownCategoryName =
        "Không xác định";

    private const string OpeningBalanceReason =
        "Tồn đầu kỳ khi tạo sản phẩm.";

    private readonly IProductRepository
        _productRepository;

    private readonly ICategoryRepository
        _categoryRepository;

    private readonly IInventoryMovementRepository
        _inventoryMovementRepository;

    private readonly IUnitOfWork
        _unitOfWork;

    private readonly IClock
        _clock;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IInventoryMovementRepository inventoryMovementRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _productRepository =
            productRepository ??
            throw new ArgumentNullException(
                nameof(productRepository));

        _categoryRepository =
            categoryRepository ??
            throw new ArgumentNullException(
                nameof(categoryRepository));

        _inventoryMovementRepository =
            inventoryMovementRepository ??
            throw new ArgumentNullException(
                nameof(inventoryMovementRepository));

        _unitOfWork =
            unitOfWork ??
            throw new ArgumentNullException(
                nameof(unitOfWork));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));
    }

    public async Task<
        Result<PagedResult<ProductListItemDto>>>
        SearchAsync(
            ProductSearchRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        var productPage =
            await _productRepository.SearchAsync(
                request.SearchTerm,
                request.CategoryId,
                request.IsActive,
                request.IsLowStock,
                request.PageNumber,
                request.PageSize,
                cancellationToken);

        var categoryNames =
            await ResolveCategoryNamesAsync(
                productPage.Items,
                cancellationToken);

        var items =
            productPage.Items
                .Select(
                    product =>
                        MapToListItem(
                            product,
                            categoryNames[
                                product.CategoryId]))
                .ToArray();

        var resultPage =
            new PagedResult<ProductListItemDto>(
                items,
                productPage.PageNumber,
                productPage.PageSize,
                productPage.TotalCount);

        return Result.Success(
            resultPage);
    }

    public async Task<Result<ProductDetailsDto>>
        GetByIdAsync(
            int productId,
            CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return ValidationFailure<
                ProductDetailsDto>(
                    "Mã sản phẩm phải lớn hơn 0.");
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var product =
            await _productRepository.GetByIdAsync(
                productId,
                cancellationToken);

        if (product is null)
        {
            return ProductNotFound<
                ProductDetailsDto>();
        }

        var categoryName =
            await ResolveCategoryNameAsync(
                product,
                cancellationToken);

        return Result.Success(
            MapToDetails(
                product,
                categoryName));
    }

    public async Task<Result<ProductDetailsDto>>
        CreateAsync(
            CreateProductRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        /*
         * Sản phẩm không theo dõi kho không được mang
         * tồn đầu kỳ ẩn.
         *
         * WPF hiện đã chuẩn hóa giá trị này về 0,
         * nhưng Application vẫn phải tự bảo vệ vì service
         * có thể được gọi từ API hoặc module khác sau này.
         */
        if (!request.TrackInventory &&
            request.InitialStockQuantity != 0)
        {
            return ValidationFailure<
                ProductDetailsDto>(
                    "Sản phẩm không theo dõi kho " +
                    "phải có tồn đầu kỳ bằng 0.");
        }

        var occurredAtUtc =
            _clock.UtcNow;

        Product validatedSnapshot;

        /*
         * Entity tạm dùng để Domain kiểm tra toàn bộ request,
         * bao gồm cả tồn đầu kỳ:
         *
         * - giới hạn tồn;
         * - chính sách tồn âm;
         * - giá tiền;
         * - mã, tên và đơn vị;
         * - cấu hình theo dõi kho.
         */
        try
        {
            validatedSnapshot =
                new Product(
                    request.CategoryId,
                    request.Code,
                    request.Name,
                    request.UnitName,
                    request.CostPrice,
                    request.SalePrice,
                    request.InitialStockQuantity,
                    request.MinimumStock,
                    request.TrackInventory,
                    request.AllowNegativeStock,
                    occurredAtUtc,
                    request.Barcode,
                    request.Description,
                    request.ImagePath);
        }
        catch (DomainException exception)
        {
            return DomainFailure<
                ProductDetailsDto>(
                    exception);
        }

        var categoryResult =
            await GetActiveCategoryAsync(
                validatedSnapshot.CategoryId,
                cancellationToken);

        if (categoryResult.IsFailure)
        {
            return Result.Failure<
                ProductDetailsDto>(
                    categoryResult.Error);
        }

        /*
         * Pre-check giúp phản hồi giao diện nhanh.
         *
         * Unique index trong SQLite vẫn là nguồn sự thật
         * khi hai thao tác tạo chạy đồng thời.
         */
        if (await _productRepository.CodeExistsAsync(
                validatedSnapshot.Code,
                cancellationToken:
                    cancellationToken))
        {
            return Result.Failure<
                ProductDetailsDto>(
                    new Error(
                        ErrorCodes.Products
                            .CodeAlreadyExists,
                        $"Mã sản phẩm " +
                        $"'{validatedSnapshot.Code}' " +
                        "đã tồn tại."));
        }

        if (validatedSnapshot.Barcode is not null &&
            await _productRepository
                .BarcodeExistsAsync(
                    validatedSnapshot.Barcode,
                    cancellationToken:
                        cancellationToken))
        {
            return Result.Failure<
                ProductDetailsDto>(
                    new Error(
                        ErrorCodes.Products
                            .BarcodeAlreadyExists,
                        $"Mã vạch " +
                        $"'{validatedSnapshot.Barcode}' " +
                        "đã tồn tại."));
        }

        /*
         * Product được tạo thật với tồn bằng 0.
         *
         * Tồn đầu kỳ sẽ được áp dụng sau khi Product đã
         * được lưu lần đầu và nhận ProductId.
         */
        Product product;

        try
        {
            product =
                new Product(
                    validatedSnapshot.CategoryId,
                    validatedSnapshot.Code,
                    validatedSnapshot.Name,
                    validatedSnapshot.UnitName,
                    validatedSnapshot.CostPrice,
                    validatedSnapshot.SalePrice,
                    stockQuantity: 0,
                    validatedSnapshot.MinimumStock,
                    validatedSnapshot.TrackInventory,
                    validatedSnapshot.AllowNegativeStock,
                    occurredAtUtc,
                    validatedSnapshot.Barcode,
                    validatedSnapshot.Description,
                    validatedSnapshot.ImagePath);
        }
        catch (DomainException exception)
        {
            return DomainFailure<
                ProductDetailsDto>(
                    exception);
        }

        /*
         * Transaction bao trùm toàn bộ quá trình:
         *
         * 1. thêm Product với tồn 0;
         * 2. SaveChanges để nhận ProductId;
         * 3. cập nhật tồn đầu kỳ qua Domain;
         * 4. thêm InventoryMovement;
         * 5. SaveChanges;
         * 6. commit.
         *
         * Nếu bất kỳ bước nào thất bại, khi transaction
         * được dispose mà chưa commit, toàn bộ thay đổi
         * sẽ tự rollback.
         */
        await using var transaction =
            await _unitOfWork
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            await _productRepository.AddAsync(
                product,
                cancellationToken);

            /*
             * Lần lưu thứ nhất là cần thiết để SQLite
             * sinh khóa chính ProductId.
             */
            var productSaveResult =
                await SaveChangesSafelyAsync(
                    cancellationToken);

            if (productSaveResult.IsFailure)
            {
                return Result.Failure<
                    ProductDetailsDto>(
                        productSaveResult.Error);
            }

            /*
             * Không tạo movement có delta bằng 0 vì:
             *
             * - không mang ý nghĩa nghiệp vụ;
             * - database chỉ cho delta 0 với Stocktake;
             * - tránh làm lịch sử kho bị nhiễu.
             */
            if (validatedSnapshot.TrackInventory &&
                request.InitialStockQuantity != 0)
            {
                product.ReconcileStock(
                    request.InitialStockQuantity,
                    occurredAtUtc);

                var openingBalance =
                    new InventoryMovement(
                        product.Id,
                        InventoryMovementType
                            .OpeningBalance,
                        quantityDelta:
                            request.InitialStockQuantity,
                        quantityBefore:
                            0,
                        quantityAfter:
                            request.InitialStockQuantity,
                        reason:
                            OpeningBalanceReason,
                        occurredAtUtc:
                            occurredAtUtc,
                        referenceType:
                            null,
                        referenceId:
                            null,
                        performedByUserId:
                            null);

                await _inventoryMovementRepository
                    .AddAsync(
                        openingBalance,
                        cancellationToken);

                /*
                 * Lần lưu thứ hai cập nhật tồn Product
                 * và thêm OpeningBalance cùng lúc.
                 */
                var openingBalanceSaveResult =
                    await SaveChangesSafelyAsync(
                        cancellationToken);

                if (openingBalanceSaveResult.IsFailure)
                {
                    return Result.Failure<
                        ProductDetailsDto>(
                            openingBalanceSaveResult.Error);
                }
            }

            await transaction.CommitAsync(
                cancellationToken);

            return Result.Success(
                MapToDetails(
                    product,
                    categoryResult.Value.Name));
        }
        catch (DomainException exception)
        {
            /*
             * Transaction chưa commit sẽ tự rollback
             * khi thoát khỏi await using.
             */
            return DomainFailure<
                ProductDetailsDto>(
                    exception);
        }
    }

    public async Task<Result<ProductDetailsDto>>
        UpdateAsync(
            UpdateProductRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.ProductId <= 0)
        {
            return ValidationFailure<
                ProductDetailsDto>(
                    "Mã sản phẩm phải lớn hơn 0.");
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var product =
            await _productRepository.GetByIdAsync(
                request.ProductId,
                cancellationToken);

        if (product is null)
        {
            return ProductNotFound<
                ProductDetailsDto>();
        }

        /*
         * Tạo entity tạm để Domain kiểm tra toàn bộ request
         * trước khi thay đổi entity thật đang được EF theo dõi.
         *
         * StockQuantity lấy từ Product hiện tại.
         * UpdateProductRequest không có trường thay đổi tồn kho.
         */
        Product validatedSnapshot;

        try
        {
            validatedSnapshot =
                new Product(
                    request.CategoryId,
                    request.Code,
                    request.Name,
                    request.UnitName,
                    request.CostPrice,
                    request.SalePrice,
                    product.StockQuantity,
                    request.MinimumStock,
                    request.TrackInventory,
                    request.AllowNegativeStock,
                    _clock.UtcNow,
                    request.Barcode,
                    request.Description,
                    request.ImagePath);
        }
        catch (DomainException exception)
        {
            return DomainFailure<
                ProductDetailsDto>(
                    exception);
        }

        var categoryResult =
            await GetActiveCategoryAsync(
                validatedSnapshot.CategoryId,
                cancellationToken);

        if (categoryResult.IsFailure)
        {
            return Result.Failure<
                ProductDetailsDto>(
                    categoryResult.Error);
        }

        if (await _productRepository.CodeExistsAsync(
                validatedSnapshot.Code,
                product.Id,
                cancellationToken))
        {
            return Result.Failure<
                ProductDetailsDto>(
                    new Error(
                        ErrorCodes.Products
                            .CodeAlreadyExists,
                        $"Mã sản phẩm " +
                        $"'{validatedSnapshot.Code}' " +
                        "đã tồn tại."));
        }

        if (validatedSnapshot.Barcode is not null &&
            await _productRepository
                .BarcodeExistsAsync(
                    validatedSnapshot.Barcode,
                    product.Id,
                    cancellationToken))
        {
            return Result.Failure<
                ProductDetailsDto>(
                    new Error(
                        ErrorCodes.Products
                            .BarcodeAlreadyExists,
                        $"Mã vạch " +
                        $"'{validatedSnapshot.Barcode}' " +
                        "đã tồn tại."));
        }

        var utcNow =
            _clock.UtcNow;

        try
        {
            product.UpdateDetails(
                validatedSnapshot.CategoryId,
                validatedSnapshot.Code,
                validatedSnapshot.Barcode,
                validatedSnapshot.Name,
                validatedSnapshot.Description,
                validatedSnapshot.UnitName,
                validatedSnapshot.ImagePath,
                utcNow);

            product.ChangePrices(
                validatedSnapshot.CostPrice,
                validatedSnapshot.SalePrice,
                utcNow);

            /*
             * Chỉ cập nhật cấu hình kho.
             *
             * Product.ConfigureInventory dùng lại
             * StockQuantity hiện tại của Product.
             */
            product.ConfigureInventory(
                validatedSnapshot.MinimumStock,
                validatedSnapshot.TrackInventory,
                validatedSnapshot.AllowNegativeStock,
                utcNow);

            if (request.IsActive)
            {
                product.Activate(
                    utcNow);
            }
            else
            {
                product.Deactivate(
                    utcNow);
            }
        }
        catch (DomainException exception)
        {
            return DomainFailure<
                ProductDetailsDto>(
                    exception);
        }

        var saveResult =
            await SaveChangesSafelyAsync(
                cancellationToken);

        if (saveResult.IsFailure)
        {
            return Result.Failure<
                ProductDetailsDto>(
                    saveResult.Error);
        }

        return Result.Success(
            MapToDetails(
                product,
                categoryResult.Value.Name));
    }

    public async Task<Result>
        SetActiveStateAsync(
            int productId,
            bool isActive,
            CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.General.Validation,
                    "Mã sản phẩm phải lớn hơn 0."));
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var product =
            await _productRepository.GetByIdAsync(
                productId,
                cancellationToken);

        if (product is null)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.Products.NotFound,
                    "Không tìm thấy sản phẩm."));
        }

        if (product.IsActive == isActive)
        {
            return Result.Success();
        }

        var utcNow =
            _clock.UtcNow;

        if (isActive)
        {
            var categoryResult =
                await GetActiveCategoryAsync(
                    product.CategoryId,
                    cancellationToken);

            if (categoryResult.IsFailure)
            {
                return Result.Failure(
                    categoryResult.Error);
            }

            product.Activate(
                utcNow);
        }
        else
        {
            product.Deactivate(
                utcNow);
        }

        return await SaveChangesSafelyAsync(
            cancellationToken);
    }

    private async Task<Result<Category>>
        GetActiveCategoryAsync(
            int categoryId,
            CancellationToken cancellationToken)
    {
        if (categoryId <= 0)
        {
            return Result.Failure<Category>(
                new Error(
                    ErrorCodes.General.Validation,
                    "Mã danh mục phải lớn hơn 0."));
        }

        var category =
            await _categoryRepository.GetByIdAsync(
                categoryId,
                cancellationToken);

        if (category is null)
        {
            return Result.Failure<Category>(
                new Error(
                    ErrorCodes.Products
                        .CategoryNotFound,
                    "Không tìm thấy danh mục sản phẩm."));
        }

        if (!category.IsActive)
        {
            return Result.Failure<Category>(
                new Error(
                    ErrorCodes.Products
                        .CategoryInactive,
                    "Danh mục sản phẩm đang ngừng hoạt động."));
        }

        return Result.Success(
            category);
    }

    private async Task<
        IReadOnlyDictionary<int, string>>
        ResolveCategoryNamesAsync(
            IReadOnlyCollection<Product> products,
            CancellationToken cancellationToken)
    {
        var categoryNames =
            new Dictionary<int, string>();

        foreach (var product in products)
        {
            if (product.Category is not null)
            {
                categoryNames[
                    product.CategoryId] =
                    product.Category.Name;
            }
        }

        var missingCategoryIds =
            products
                .Select(
                    product =>
                        product.CategoryId)
                .Where(
                    categoryId =>
                        !categoryNames
                            .ContainsKey(
                                categoryId))
                .Distinct()
                .ToArray();

        foreach (var categoryId
                 in missingCategoryIds)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var category =
                await _categoryRepository
                    .GetByIdAsync(
                        categoryId,
                        cancellationToken);

            categoryNames[categoryId] =
                category?.Name ??
                UnknownCategoryName;
        }

        return categoryNames;
    }

    private async Task<string>
        ResolveCategoryNameAsync(
            Product product,
            CancellationToken cancellationToken)
    {
        if (product.Category is not null)
        {
            return product.Category.Name;
        }

        var category =
            await _categoryRepository.GetByIdAsync(
                product.CategoryId,
                cancellationToken);

        return category?.Name ??
               UnknownCategoryName;
    }

    private async Task<Result>
        SaveChangesSafelyAsync(
            CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return Result.Success();
        }
        catch (
            PersistenceConflictException exception)
        {
            return Result.Failure(
                MapPersistenceConflict(
                    exception));
        }
    }

    private static Error MapPersistenceConflict(
        PersistenceConflictException exception)
    {
        if (exception.Kind ==
                PersistenceConflictKind
                    .UniqueConstraint &&
            string.Equals(
                exception.Target,
                PersistenceConflictTargets
                    .ProductCode,
                StringComparison.Ordinal))
        {
            return new Error(
                ErrorCodes.Products
                    .CodeAlreadyExists,
                "Mã sản phẩm đã tồn tại. " +
                "Vui lòng sử dụng mã khác.");
        }

        if (exception.Kind ==
                PersistenceConflictKind
                    .UniqueConstraint &&
            string.Equals(
                exception.Target,
                PersistenceConflictTargets
                    .ProductBarcode,
                StringComparison.Ordinal))
        {
            return new Error(
                ErrorCodes.Products
                    .BarcodeAlreadyExists,
                "Mã vạch đã được sử dụng " +
                "bởi sản phẩm khác.");
        }

        if (exception.Kind ==
            PersistenceConflictKind.Concurrency)
        {
            return new Error(
                ErrorCodes.Products
                    .ConcurrencyConflict,
                "Sản phẩm đã được người dùng " +
                "hoặc cửa sổ khác thay đổi. " +
                "Hãy tải lại dữ liệu rồi thực hiện lại.");
        }

        return new Error(
            ErrorCodes.Products
                .PersistenceConflict,
            "Không thể lưu sản phẩm do dữ liệu " +
            "đang xung đột. Hãy tải lại và thử lại.");
    }

    private static ProductListItemDto
        MapToListItem(
            Product product,
            string categoryName)
    {
        return new ProductListItemDto(
            product.Id,
            product.CategoryId,
            categoryName,
            product.Code,
            product.Barcode,
            product.Name,
            product.UnitName,
            product.CostPrice,
            product.SalePrice,
            product.ProfitPerUnit,
            product.StockQuantity,
            product.MinimumStock,
            product.TrackInventory,
            product.AllowNegativeStock,
            product.IsLowStock,
            product.IsOutOfStock,
            product.IsActive);
    }

    private static ProductDetailsDto
        MapToDetails(
            Product product,
            string categoryName)
    {
        return new ProductDetailsDto(
            product.Id,
            product.CategoryId,
            categoryName,
            product.Code,
            product.Barcode,
            product.Name,
            product.Description,
            product.UnitName,
            product.ImagePath,
            product.CostPrice,
            product.SalePrice,
            product.ProfitPerUnit,
            product.StockQuantity,
            product.MinimumStock,
            product.TrackInventory,
            product.AllowNegativeStock,
            product.IsLowStock,
            product.IsOutOfStock,
            product.IsActive,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);
    }

    private static Result<TValue>
        ProductNotFound<TValue>()
    {
        return Result.Failure<TValue>(
            new Error(
                ErrorCodes.Products.NotFound,
                "Không tìm thấy sản phẩm."));
    }

    private static Result<TValue>
        ValidationFailure<TValue>(
            string message)
    {
        return Result.Failure<TValue>(
            new Error(
                ErrorCodes.General.Validation,
                message));
    }

    private static Result<TValue>
        DomainFailure<TValue>(
            DomainException exception)
    {
        return Result.Failure<TValue>(
            new Error(
                exception.Code,
                exception.Message));
    }
}