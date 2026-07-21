using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation của IOrderRepository.
///
/// Khi tải một Order nghiệp vụ, repository luôn tải đủ:
/// Order → Items → Modifiers.
///
/// Các truy vấn danh sách sử dụng no-tracking để không giữ
/// aggregate lâu hơn vòng đời của use case.
/// </summary>
public sealed class OrderRepository :
    IOrderRepository
{
    private const string
        LikeEscapeCharacter = "\\";

    private const int
        MaximumPageSize = 200;

    private readonly PosDbContext
        _dbContext;

    public OrderRepository(
        PosDbContext dbContext)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    public Task<Order?> GetByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            return Task.FromResult<Order?>(
                null);
        }

        return CreateAggregateQuery(
                asNoTracking:
                    false)
            .SingleOrDefaultAsync(
                order =>
                    order.Id == orderId,
                cancellationToken);
    }

    public Task<Order?> GetByCodeAsync(
        string orderCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode =
            NormalizeOrderCode(
                orderCode);

        if (normalizedCode.Length == 0)
        {
            return Task.FromResult<Order?>(
                null);
        }

        return CreateAggregateQuery(
                asNoTracking:
                    false)
            .SingleOrDefaultAsync(
                order =>
                    order.OrderCode ==
                    normalizedCode,
                cancellationToken);
    }

    public async Task<PagedResult<Order>>
        SearchAsync(
            string? searchTerm,
            OrderStatus? status,
            int? customerId,
            int? cashierUserId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var normalizedFromUtc =
            NormalizeOptionalUtc(
                fromUtc,
                nameof(fromUtc));

        var normalizedToUtc =
            NormalizeOptionalUtc(
                toUtc,
                nameof(toUtc));

        ValidateSearch(
            status,
            customerId,
            cashierUserId,
            normalizedFromUtc,
            normalizedToUtc,
            pageNumber,
            pageSize);

        /*
         * Tính skip bằng checked trước khi truy vấn database.
         *
         * Không để pageNumber cực lớn làm overflow rồi
         * tạo ra vị trí phân trang âm hoặc sai.
         */
        var skip =
            CalculateSkip(
                pageNumber,
                pageSize);

        cancellationToken
            .ThrowIfCancellationRequested();

        IQueryable<Order> query =
            _dbContext.Orders
                .AsNoTracking();

        if (status.HasValue)
        {
            query =
                query.Where(
                    order =>
                        order.Status ==
                        status.Value);
        }

        if (customerId.HasValue)
        {
            query =
                query.Where(
                    order =>
                        order.CustomerId ==
                        customerId.Value);
        }

        if (cashierUserId.HasValue)
        {
            query =
                query.Where(
                    order =>
                        order.CashierUserId ==
                        cashierUserId.Value);
        }

        if (normalizedFromUtc.HasValue)
        {
            query =
                query.Where(
                    order =>
                        order.CreatedAtUtc >=
                        normalizedFromUtc.Value);
        }

        if (normalizedToUtc.HasValue)
        {
            query =
                query.Where(
                    order =>
                        order.CreatedAtUtc <=
                        normalizedToUtc.Value);
        }

        var normalizedSearchTerm =
            NormalizeSearchTerm(
                searchTerm);

        if (normalizedSearchTerm is not null)
        {
            var escapedSearchTerm =
                EscapeLikePattern(
                    normalizedSearchTerm);

            var pattern =
                $"%{escapedSearchTerm}%";

            /*
             * Customer chưa được đưa vào persistence model
             * trong 8A, nên hiện tại tìm theo:
             * - mã đơn;
             * - mã giảm giá snapshot;
             * - tên thu ngân.
             */
            query =
                query.Where(
                    order =>
                        EF.Functions.Like(
                            order.OrderCode,
                            pattern,
                            LikeEscapeCharacter)

                        ||

                        (
                            order.DiscountCode != null &&
                            EF.Functions.Like(
                                order.DiscountCode,
                                pattern,
                                LikeEscapeCharacter)
                        )

                        ||

                        (
                            order.CashierUser != null &&
                            EF.Functions.Like(
                                order.CashierUser.FullName,
                                pattern,
                                LikeEscapeCharacter)
                        ));
        }

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var pageOrderIds =
            await query
                .OrderByDescending(
                    order =>
                        order.CreatedAtUtc)
                .ThenByDescending(
                    order =>
                        order.Id)
                .Select(
                    order =>
                        order.Id)
                .Skip(
                    skip)
                .Take(
                    pageSize)
                .ToArrayAsync(
                    cancellationToken);

        if (pageOrderIds.Length == 0)
        {
            /*
             * Trang có thể vượt quá trang cuối nhưng
             * TotalCount vẫn phải phản ánh toàn bộ kết quả.
             *
             * Không dùng PagedResult.Empty vì method đó
             * luôn đặt TotalCount bằng 0.
             */
            return new PagedResult<Order>(
                Array.Empty<Order>(),
                pageNumber,
                pageSize,
                totalCount);
        }

        /*
         * Tải aggregate bằng truy vấn riêng.
         *
         * Phân trang trực tiếp trên collection Include có thể
         * nhân số dòng SQL và làm sai kết quả phân trang.
         */
        var loadedOrders =
            await CreateAggregateQuery(
                    asNoTracking:
                        true)
                .Where(
                    order =>
                        pageOrderIds.Contains(
                            order.Id))
                .ToArrayAsync(
                    cancellationToken);

        var orderById =
            loadedOrders.ToDictionary(
                order =>
                    order.Id);

        /*
         * SQL IN không đảm bảo giữ thứ tự của pageOrderIds.
         * Sắp xếp lại trong bộ nhớ theo đúng thứ tự trang.
         */
        var orderedItems =
            pageOrderIds
                .Where(
                    orderById.ContainsKey)
                .Select(
                    orderId =>
                        orderById[orderId])
                .ToArray();

        return new PagedResult<Order>(
            orderedItems,
            pageNumber,
            pageSize,
            totalCount);
    }

    public Task<bool> CodeExistsAsync(
        string orderCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode =
            NormalizeOrderCode(
                orderCode);

        if (normalizedCode.Length == 0)
        {
            return Task.FromResult(
                false);
        }

        return _dbContext.Orders
            .AsNoTracking()
            .AnyAsync(
                order =>
                    order.OrderCode ==
                    normalizedCode,
                cancellationToken);
    }

    public async Task AddAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            order);

        await _dbContext.Orders
            .AddAsync(
                order,
                cancellationToken);
    }

    private IQueryable<Order>
        CreateAggregateQuery(
            bool asNoTracking)
    {
        IQueryable<Order> query =
            _dbContext.Orders
                .AsSplitQuery()
                .Include(
                    order =>
                        order.CashierUser)
                .Include(
                    order =>
                        order.Items)
                    .ThenInclude(
                        item =>
                            item.Modifiers);

        if (asNoTracking)
        {
            query =
                query.AsNoTrackingWithIdentityResolution();
        }

        return query;
    }

    private static void ValidateSearch(
        OrderStatus? status,
        int? customerId,
        int? cashierUserId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int pageNumber,
        int pageSize)
    {
        if (status.HasValue &&
            !Enum.IsDefined(
                status.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Trạng thái đơn hàng không hợp lệ.");
        }

        if (customerId.HasValue &&
            customerId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(customerId),
                customerId,
                "Mã khách hàng phải lớn hơn 0.");
        }

        if (cashierUserId.HasValue &&
            cashierUserId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cashierUserId),
                cashierUserId,
                "Mã thu ngân phải lớn hơn 0.");
        }

        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value >
            toUtc.Value)
        {
            throw new ArgumentException(
                "Thời gian bắt đầu không được lớn hơn " +
                "thời gian kết thúc.");
        }

        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                pageNumber,
                "Số trang phải lớn hơn 0.");
        }

        if (pageSize <= 0 ||
            pageSize > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Kích thước trang phải từ 1 đến " +
                $"{MaximumPageSize}.");
        }
    }

    private static int CalculateSkip(
        int pageNumber,
        int pageSize)
    {
        try
        {
            return checked(
                (pageNumber - 1) *
                pageSize);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                pageNumber,
                "Vị trí phân trang vượt quá giới hạn.");
        }
    }

    private static DateTimeOffset?
        NormalizeOptionalUtc(
            DateTimeOffset? value,
            string parameterName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value == default)
        {
            throw new ArgumentException(
                "Thời điểm tìm kiếm không hợp lệ.",
                parameterName);
        }

        return value.Value
            .ToUniversalTime();
    }

    private static string NormalizeOrderCode(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? string.Empty
            : value
                .Trim()
                .ToUpperInvariant();
    }

    private static string?
        NormalizeSearchTerm(
            string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }

    private static string EscapeLikePattern(
        string value)
    {
        return value
            .Replace(
                LikeEscapeCharacter,
                LikeEscapeCharacter +
                LikeEscapeCharacter,
                StringComparison.Ordinal)
            .Replace(
                "%",
                LikeEscapeCharacter + "%",
                StringComparison.Ordinal)
            .Replace(
                "_",
                LikeEscapeCharacter + "_",
                StringComparison.Ordinal);
    }
}