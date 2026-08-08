namespace POS.Infrastructure.Support;

public sealed class SupportBundleOptions
{
    public const string SectionName = "SupportBundle";
    public const long DefaultMaxExportedLogBytes = 20L * 1024 * 1024;
    public const long MaximumExportedLogBytes = 100L * 1024 * 1024;
    public const int DefaultMaxLogRecordChars = 4096;
    public const int MaximumLogRecordChars = 16 * 1024;

    public long MaxExportedLogBytes { get; set; } = DefaultMaxExportedLogBytes;
    public int MaxLogRecordChars { get; set; } = DefaultMaxLogRecordChars;

    public void Validate()
    {
        if (MaxExportedLogBytes is < 0 or > MaximumExportedLogBytes)
            throw new InvalidOperationException("Support Bundle log budget is invalid.");
        if (MaxLogRecordChars is < 256 or > MaximumLogRecordChars)
            throw new InvalidOperationException("Support Bundle record limit is invalid.");
    }
}
