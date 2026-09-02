using System.IO;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using POS.Application.Common;
using System.Windows.Media.Imaging;
using POS.Application.Abstractions.StoreSetup;

namespace POS.Infrastructure.StoreSetup;

public sealed class ManagedLogoService :
    IStoreSettingsLogoService,
    IStoreSettingsLogoContentProvider
{
    private const long MaximumBytes = 2 * 1024 * 1024;
    private const int MaximumDimension = 1600;
    private readonly StoreSettingsPathProvider _paths;
    private readonly ILogger<ManagedLogoService>? _logger;

    public ManagedLogoService(
        StoreSettingsPathProvider paths,
        ILogger<ManagedLogoService>? logger = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger;
    }

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

    public async Task<bool> IsSameContentAsync(
        string sourcePath,
        string? assetName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) ||
            !Path.IsPathFullyQualified(sourcePath) ||
            sourcePath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return false;
        }

        string source;
        string? managed;
        try
        {
            source = Path.GetFullPath(sourcePath);
            if (!File.Exists(source) ||
                (File.GetAttributes(source) &
                    (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                !TryGet(assetName, out managed))
            {
                return false;
            }
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            return false;
        }

        var sourceInfo = new FileInfo(source);
        var managedInfo = new FileInfo(managed!);
        if (sourceInfo.Length != managedInfo.Length ||
            sourceInfo.Length > MaximumBytes)
        {
            return false;
        }

        await using var sourceStream =
            new FileStream(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                32 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var managedStream =
            new FileStream(
                managed!,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                32 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

        var sourceBuffer = new byte[32 * 1024];
        var managedBuffer = new byte[32 * 1024];
        while (true)
        {
            var sourceRead =
                await sourceStream.ReadAsync(
                    sourceBuffer.AsMemory(),
                    cancellationToken);
            var managedRead =
                await managedStream.ReadAsync(
                    managedBuffer.AsMemory(),
                    cancellationToken);

            if (sourceRead != managedRead)
            {
                return false;
            }

            if (sourceRead == 0)
            {
                return true;
            }

            if (!sourceBuffer.AsSpan(0, sourceRead).SequenceEqual(
                    managedBuffer.AsSpan(0, managedRead)))
            {
                return false;
            }
        }
    }

    public StoreLogoContent? TryRead(string? assetName)
    {
        string? path;

        try
        {
            if (!TryGet(assetName, out path))
            {
                LogReadResult(assetName is not null, false, 0, "ManagedAssetNotFound");
                return null;
            }

            using var input =
                new FileStream(
                    path!,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    32 * 1024,
                    FileOptions.SequentialScan);

            var decoder =
                BitmapDecoder.Create(
                    input,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

            var frame =
                decoder.Frames.FirstOrDefault();

            if (frame is null ||
                frame.PixelWidth <= 0 ||
                frame.PixelHeight <= 0 ||
                frame.PixelWidth > MaximumDimension ||
                frame.PixelHeight > MaximumDimension)
            {
                LogReadResult(true, false, 0, "ImageDimensionsInvalid");
                return null;
            }

            if (frame.CanFreeze)
            {
                frame.Freeze();
            }

            var normalized = EncodeBoundedPng(frame);
            if (normalized is null)
            {
                LogReadResult(true, false, 0, "NormalizedPngExceedsLimit");
                return null;
            }

            LogReadResult(true, true, normalized.Length, "None");
            return new StoreLogoContent(
                normalized,
                "image/png");
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException or
            ArgumentException or
            InvalidOperationException or
            FileFormatException)
        {
            Trace.TraceWarning(
                "Không thể nhúng logo cửa hàng vào receipt snapshot: {0}",
                exception.GetType().Name);
            LogReadResult(
                assetName is not null,
                false,
                0,
                "ImageDecodeOrEncodeFailed");
            return null;
        }
    }

    private static byte[]? EncodeBoundedPng(BitmapSource source)
    {
        BitmapSource candidate = source;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var output = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(candidate));
            encoder.Save(output);

            if (output.Length is > 0 and <= StoreLogoContent.MaximumBytes)
            {
                return output.ToArray();
            }

            var scale = 0.75;
            var transformed = new TransformedBitmap(
                candidate,
                new System.Windows.Media.ScaleTransform(scale, scale));
            if (transformed.CanFreeze)
            {
                transformed.Freeze();
            }

            candidate = transformed;
        }

        return null;
    }

    private void LogReadResult(
        bool configuredAssetNamePresent,
        bool resolved,
        int byteCount,
        string reason)
    {
        if (_logger is null)
        {
            return;
        }

        PosLog.Information(
            _logger,
            "ManagedLogo.Read: " +
            "ConfiguredLogoAssetNamePresent={ConfiguredLogoAssetNamePresent}; " +
            "ManagedLogoResolved={ManagedLogoResolved}; " +
            "EmbeddedLogoByteCount={EmbeddedLogoByteCount}; " +
            "FallbackReason={FallbackReason}",
            configuredAssetNamePresent,
            resolved,
            byteCount,
            reason);
    }

    private bool TryGet(string? name, out string? path)
    { path = null; if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name) || name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar) || name.Contains("..", StringComparison.Ordinal)) return false; var candidate = Path.GetFullPath(Path.Combine(_paths.LogoRoot, name)); if (!string.Equals(Path.GetDirectoryName(candidate), _paths.LogoRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate)) return false; if ((File.GetAttributes(candidate) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) return false; path = candidate; return true; }
    private void EnsureSafeLogoRoot() { if (Directory.Exists(_paths.LogoRoot) && (File.GetAttributes(_paths.LogoRoot) & FileAttributes.ReparsePoint) != 0) throw new IOException("Kho logo không an toàn."); }
    private static bool HasSignature(byte[] b, string ext) => ext == ".png" ? b.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) : ext is ".jpg" or ".jpeg" ? b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF : b[0] == 0x42 && b[1] == 0x4D;
}
