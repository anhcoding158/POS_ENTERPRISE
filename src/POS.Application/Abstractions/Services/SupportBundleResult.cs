using System.IO;

namespace POS.Application.Abstractions.Services;

public enum SupportBundleStatus
{
    Success,
    Cancelled,
    InvalidDestination,
    DestinationUnavailable,
    ArchiveAlreadyExists,
    DatabaseInclusionNotSupported,
    ArchiveCreationFailure,
    UnexpectedFailure
}

public sealed record SupportBundleResult
{
    private SupportBundleResult(SupportBundleStatus status, string? archivePath)
    {
        Status = status;
        ArchivePath = status == SupportBundleStatus.Success ? archivePath : null;
    }

    public SupportBundleStatus Status { get; }
    public string? ArchivePath { get; }
    public bool IsSuccess => Status == SupportBundleStatus.Success;

    public static SupportBundleResult Success(string archivePath) =>
        new(SupportBundleStatus.Success,
            Path.GetFullPath(archivePath ?? throw new ArgumentNullException(nameof(archivePath))));

    public static SupportBundleResult Failure(SupportBundleStatus status)
    {
        if (status == SupportBundleStatus.Success)
            throw new ArgumentOutOfRangeException(nameof(status));
        return new SupportBundleResult(status, null);
    }
}
