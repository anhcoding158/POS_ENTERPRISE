namespace POS.Application.Common;

/// <summary>
/// Kết quả của một thao tác có trả dữ liệu.
/// </summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(
        TValue? value,
        bool isSuccess,
        AppError error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Dữ liệu của kết quả thành công.
    ///
    /// Đọc Value khi Result thất bại sẽ phát sinh
    /// InvalidOperationException để phát hiện lỗi lập trình.
    /// </summary>
    public TValue Value
    {
        get
        {
            if (IsFailure)
            {
                throw new InvalidOperationException(
                    $"Không thể đọc Value của Result thất bại. " +
                    $"Lỗi: {AppError.Code}");
            }

            return _value!;
        }
    }

    /// <summary>
    /// Trả dữ liệu hoặc default mà không phát sinh exception.
    /// </summary>
    public TValue? ValueOrDefault =>
        _value;

    /// <summary>
    /// Chuyển dữ liệu thành công sang kiểu khác.
    /// Nếu Result thất bại, giữ nguyên lỗi.
    /// </summary>
    public Result<TOutput> Map<TOutput>(
        Func<TValue, TOutput> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        if (IsFailure)
        {
            return Result.Failure<TOutput>(AppError);
        }

        var output = mapper(Value);

        return Result.Success(output);
    }

    /// <summary>
    /// Nối với một thao tác khác có thể thành công
    /// hoặc thất bại.
    /// </summary>
    public Result<TOutput> Bind<TOutput>(
        Func<TValue, Result<TOutput>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        return IsFailure
            ? Result.Failure<TOutput>(AppError)
            : binder(Value);
    }

    /// <summary>
    /// Nối với một thao tác không trả dữ liệu.
    /// </summary>
    public Result Bind(
        Func<TValue, Result> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        return IsFailure
            ? Result.Failure(AppError)
            : binder(Value);
    }

    /// <summary>
    /// Chuyển kết quả thành một giá trị khác tùy theo
    /// thành công hoặc thất bại.
    /// </summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<AppError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess
            ? onSuccess(Value)
            : onFailure(AppError);
    }

    /// <summary>
    /// Kiểm tra thêm điều kiện trên kết quả thành công.
    /// </summary>
    public Result<TValue> Ensure(
        Func<TValue, bool> predicate,
        AppError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (IsFailure)
        {
            return this;
        }

        return predicate(Value)
            ? this
            : Result.Failure<TValue>(error);
    }

    /// <summary>
    /// Thực hiện hành động phụ khi thành công,
    /// không làm thay đổi Result.
    /// </summary>
    public Result<TValue> Tap(
        Action<TValue> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess)
        {
            action(Value);
        }

        return this;
    }
}
