namespace POS.Infrastructure.Storage;

public sealed class DatabaseStorageOptions
{
    public const string SectionName = "Infrastructure:DatabaseStorage";
    public const long DefaultWarningFreeBytes = 5_368_709_120;
    public const decimal DefaultWarningFreePercentage = 10m;
    public const long DefaultReservedHeadroomBytes = 536_870_912;
    public const long DefaultBackupEstimateMinimumPaddingBytes = 268_435_456;
    public const decimal DefaultBackupEstimatePaddingPercentage = 10m;

    public long WarningFreeBytes { get; set; } = DefaultWarningFreeBytes;
    public decimal WarningFreePercentage { get; set; } = DefaultWarningFreePercentage;
    public long ReservedHeadroomBytes { get; set; } = DefaultReservedHeadroomBytes;
    public long BackupEstimateMinimumPaddingBytes { get; set; } =
        DefaultBackupEstimateMinimumPaddingBytes;
    public decimal BackupEstimatePaddingPercentage { get; set; } =
        DefaultBackupEstimatePaddingPercentage;

    public void Validate()
    {
        if (WarningFreeBytes <= 0 ||
            WarningFreePercentage is <= 0 or > 100 ||
            ReservedHeadroomBytes <= 0 ||
            ReservedHeadroomBytes > WarningFreeBytes ||
            BackupEstimateMinimumPaddingBytes < 0 ||
            BackupEstimatePaddingPercentage is < 0 or > 100)
        {
            throw new InvalidOperationException(
                "Cấu hình theo dõi dung lượng database không hợp lệ.");
        }
    }
}
