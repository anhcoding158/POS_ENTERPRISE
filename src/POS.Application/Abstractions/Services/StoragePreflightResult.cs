namespace POS.Application.Abstractions.Services;

public enum StoragePreflightStatus
{
    Allowed,
    AllowedWithWarning,
    Insufficient,
    MetricsUnavailable
}

public sealed record StoragePreflightRequest
{
    public StoragePreflightRequest(long requiredAdditionalBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requiredAdditionalBytes);
        RequiredAdditionalBytes = requiredAdditionalBytes;
    }

    public long RequiredAdditionalBytes { get; }
}

public sealed record StoragePreflightResult(
    StoragePreflightStatus Status,
    long RequiredAdditionalBytes,
    long ReservedHeadroomBytes,
    long? RequiredFreeBytes,
    long? AvailableFreeBytes)
{
    public bool CanProceed => Status is
        StoragePreflightStatus.Allowed or
        StoragePreflightStatus.AllowedWithWarning or
        StoragePreflightStatus.MetricsUnavailable;
}

public sealed class StoragePreflightException : Exception
{
    public StoragePreflightException(StoragePreflightResult result)
        : base("Không đủ dung lượng an toàn để thực hiện thao tác database.")
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Status is not StoragePreflightStatus.Insufficient)
        {
            throw new ArgumentException(
                "Chỉ kết quả Insufficient mới có thể tạo storage preflight exception.",
                nameof(result));
        }

        Result = result;
    }

    public StoragePreflightResult Result { get; }
}
