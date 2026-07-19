namespace POS.Application.Common;

/// <summary>
/// Kết quả của một thao tác không cần trả dữ liệu.
/// </summary>
public class Result
{
    protected internal Result(
        bool isSuccess,
        Error error)
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
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure =>
        !IsSuccess;

    public Error Error { get; }

    public static Result Success()
    {
        return new Result(
            true,
            Error.None);
    }

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result(
            false,
            error);
    }

    public static Result<TValue> Success<TValue>(
        TValue value)
    {
        return Result<TValue>.Success(value);
    }

    public static Result<TValue> Failure<TValue>(
        Error error)
    {
        return Result<TValue>.Failure(error);
    }

    /// <summary>
    /// Chuyển Result thành một giá trị khác tùy theo
    /// trạng thái thành công hoặc thất bại.
    /// </summary>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess
            ? onSuccess()
            : onFailure(Error);
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
            : Failure<TValue>(Error);
    }
}