namespace POS.Application.Common;

/// <summary>
/// Kết quả của một thao tác không cần trả dữ liệu.
/// </summary>
public class Result
{
    protected internal Result(
        bool isSuccess,
        AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (isSuccess && !error.IsNone)
        {
            throw new ArgumentException(
                "Kết quả thành công không được chứa lỗi.",
                nameof(error));
        }

        if (!isSuccess && error.IsNone)
        {
            throw new ArgumentException(
                "Kết quả thất bại phải chứa lỗi.",
                nameof(error));
        }

        IsSuccess = isSuccess;
        AppError = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure =>
        !IsSuccess;

    public AppError AppError { get; }

    public static Result Success()
    {
        return new Result(
            true,
            AppError.None);
    }

    public static Result Failure(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result(
            false,
            error);
    }

    public static Result<TValue> Success<TValue>(
        TValue value)
    {
        return value is null
            ? Failure<TValue>(AppError.NullValue)
            : new Result<TValue>(value, true, AppError.None);
    }

    public static Result<TValue> Failure<TValue>(
        AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<TValue>(default, false, error);
    }

    /// <summary>
    /// Chuyển Result thành một giá trị khác tùy theo
    /// trạng thái thành công hoặc thất bại.
    /// </summary>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<AppError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess
            ? onSuccess()
            : onFailure(AppError);
    }

    /// <summary>
    /// Thực hiện bước tiếp theo nếu Result hiện tại thành công.
    /// </summary>
    public Result Bind(
        Func<Result> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return IsSuccess
            ? next()
            : this;
    }

    /// <summary>
    /// Thực hiện bước tiếp theo có trả dữ liệu
    /// nếu Result hiện tại thành công.
    /// </summary>
    public Result<TValue> Bind<TValue>(
        Func<Result<TValue>> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return IsSuccess
            ? next()
            : Failure<TValue>(AppError);
    }
}
