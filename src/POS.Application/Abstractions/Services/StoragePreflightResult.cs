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
    public bool CanProceed => Status is not StoragePreflightStatus.Insufficient;
}
