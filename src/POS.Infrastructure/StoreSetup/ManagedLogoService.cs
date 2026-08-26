using System.IO;
using System.Windows.Media.Imaging;
using POS.Application.Abstractions.StoreSetup;

namespace POS.Infrastructure.StoreSetup;

public sealed class ManagedLogoService : IStoreSettingsLogoService
{
    private const long MaximumBytes = 2 * 1024 * 1024;
    private const int MaximumDimension = 1600;
    private readonly StoreSettingsPathProvider _paths;

    public ManagedLogoService(StoreSettingsPathProvider paths) => _paths = paths ?? throw new ArgumentNullException(nameof(paths));

    public async Task<string> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !Path.IsPathFullyQualified(sourcePath) || sourcePath.StartsWith("\\\\", StringComparison.Ordinal)) throw new InvalidDataException("Logo phải là tệp cục bộ.");
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source) || (File.GetAttributes(source) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw new InvalidDataException("Tệp logo không hợp lệ.");
        var info = new FileInfo(source);
        if (info.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Kích thước logo không được hỗ trợ.");
        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".bmp") throw new InvalidDataException("Định dạng logo không được hỗ trợ.");
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 32 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var bytes = new byte[8];
        if (await input.ReadAsync(bytes, cancellationToken) < 8 || !HasSignature(bytes, extension)) throw new InvalidDataException("Nội dung logo không khớp định dạng tệp.");
        input.Position = 0;
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.FirstOrDefault() ?? throw new InvalidDataException("Không đọc được logo.");
        if (frame.PixelWidth > MaximumDimension || frame.PixelHeight > MaximumDimension) throw new InvalidDataException("Kích thước ảnh logo quá lớn.");
        Directory.CreateDirectory(_paths.LogoRoot);
        EnsureSafeLogoRoot();
        var assetName = $"logo-{Guid.NewGuid():N}{extension}";
        var temp = Path.Combine(_paths.LogoRoot, $".{assetName}.tmp");
        var target = Path.Combine(_paths.LogoRoot, assetName);
        try
        {
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            { input.Position = 0; await input.CopyToAsync(output, cancellationToken); await output.FlushAsync(cancellationToken); output.Flush(true); }
            cancellationToken.ThrowIfCancellationRequested(); File.Move(temp, target, false); return assetName;
        }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
    }

    public Task RemoveAsync(string? assetName, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); if (TryGet(assetName, out var path)) File.Delete(path!); return Task.CompletedTask; }
    public string? GetManagedPath(string? assetName) => TryGet(assetName, out var path) ? path : null;
    private bool TryGet(string? name, out string? path)
    { path = null; if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name) || name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar) || name.Contains("..", StringComparison.Ordinal)) return false; var candidate = Path.GetFullPath(Path.Combine(_paths.LogoRoot, name)); if (!string.Equals(Path.GetDirectoryName(candidate), _paths.LogoRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate)) return false; if ((File.GetAttributes(candidate) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) return false; path = candidate; return true; }
    private void EnsureSafeLogoRoot() { if (Directory.Exists(_paths.LogoRoot) && (File.GetAttributes(_paths.LogoRoot) & FileAttributes.ReparsePoint) != 0) throw new IOException("Kho logo không an toàn."); }
    private static bool HasSignature(byte[] b, string ext) => ext == ".png" ? b.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) : ext is ".jpg" or ".jpeg" ? b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF : b[0] == 0x42 && b[1] == 0x4D;
}
