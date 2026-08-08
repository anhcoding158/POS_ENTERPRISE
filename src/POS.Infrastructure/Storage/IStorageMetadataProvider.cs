using System.IO;

namespace POS.Infrastructure.Storage;

internal readonly record struct StoragePathMetadata(
    bool Exists,
    bool IsDirectory,
    bool IsReparsePoint,
    long? Length);

internal readonly record struct StorageVolumeMetadata(
    string VolumeRoot,
    long? TotalCapacityBytes,
    long? AvailableFreeBytes);

internal interface IStorageMetadataProvider
{
    StoragePathMetadata GetPathMetadata(string path);
    StorageVolumeMetadata GetVolumeMetadata(string path);
}
