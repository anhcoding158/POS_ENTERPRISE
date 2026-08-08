using System.IO;

namespace POS.Infrastructure.Logging;

public sealed class SafeFileLoggerOptions
{
    public const string SectionName = "Logging:SafeFile";
    public const long DefaultMaxFileSizeBytes = 5L * 1024 * 1024;
    public const int DefaultMaxSegmentCount = 10;
    public const long DefaultMaxDirectorySizeBytes = 50L * 1024 * 1024;
    public const int DefaultMaxAgeDays = 14;

    public string? LogDirectory { get; set; }
    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;
    public int MaxSegmentCount { get; set; } = DefaultMaxSegmentCount;
    public long MaxDirectorySizeBytes { get; set; } = DefaultMaxDirectorySizeBytes;
    public int MaxAgeDays { get; set; } = DefaultMaxAgeDays;

    public void Validate()
    {
        if (MaxFileSizeBytes < 256 || MaxSegmentCount <= 0 ||
            MaxDirectorySizeBytes <= 0 || MaxAgeDays <= 0)
        {
            throw new InvalidOperationException(
                "Safe-file logging limits are invalid; segments must be at least 256 bytes.");
        }

        if (MaxFileSizeBytes > MaxDirectorySizeBytes)
        {
            throw new InvalidOperationException(
                "A log segment cannot exceed the directory quota.");
        }
    }

    internal string ResolveLogDirectory()
    {
        if (!string.IsNullOrWhiteSpace(LogDirectory))
        {
            return Path.GetFullPath(LogDirectory);
        }

        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData))
        {
            throw new InvalidOperationException("LocalApplicationData is unavailable.");
        }

        return Path.GetFullPath(Path.Combine(applicationData, "POS Enterprise", "logs"));
    }
}
