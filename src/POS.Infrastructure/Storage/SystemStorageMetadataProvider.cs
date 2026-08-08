using System.IO;

namespace POS.Infrastructure.Storage;

internal sealed class SystemStorageMetadataProvider : IStorageMetadataProvider
{
    public StoragePathMetadata GetPathMetadata(string path)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return new StoragePathMetadata(false, false, false, null);
        }
        catch (DirectoryNotFoundException)
        {
            return new StoragePathMetadata(false, false, false, null);
        }

        var isDirectory = (attributes & FileAttributes.Directory) != 0;
        var isReparse = (attributes & FileAttributes.ReparsePoint) != 0;
        long? length = null;
        if (!isDirectory && !isReparse)
        {
            length = new FileInfo(path).Length;
        }

        return new StoragePathMetadata(true, isDirectory, isReparse, length);
    }

    public StorageVolumeMetadata GetVolumeMetadata(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new IOException("Volume root is unavailable.");
        }

        var drive = new DriveInfo(root);
        return new StorageVolumeMetadata(
            Path.GetFullPath(root),
            drive.IsReady ? drive.TotalSize : null,
            drive.IsReady ? drive.AvailableFreeSpace : null);
    }
}
