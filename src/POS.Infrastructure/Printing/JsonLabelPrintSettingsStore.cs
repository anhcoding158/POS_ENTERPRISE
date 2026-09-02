using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using POS.Application.Abstractions.Printing;
using POS.Application.DTOs.Printing;
using POS.Infrastructure.StoreSetup;

namespace POS.Infrastructure.Printing;

public sealed class JsonLabelPrintSettingsStore : ILabelPrintSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly StoreSettingsPathProvider _paths;
    private readonly object _gate = new();
    private LabelPrintSettings _current = new();

    public JsonLabelPrintSettingsStore(StoreSettingsPathProvider paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        LoadExisting();
    }

    public LabelPrintSettings Current
    {
        get { lock (_gate) return _current; }
    }

    public void Save(LabelPrintSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var tempPath = Path.Combine(_paths.Root, $".{StoreSettingsPathProvider.LabelSettingsFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(_paths.Root);
            var directory = new DirectoryInfo(_paths.Root);
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) return;
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
            if (File.Exists(_paths.LabelSettingsPath))
                File.Replace(tempPath, _paths.LabelSettingsPath, null, true);
            else
                File.Move(tempPath, _paths.LabelSettingsPath, false);
            lock (_gate) _current = settings;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private void LoadExisting()
    {
        try
        {
            if (!File.Exists(_paths.LabelSettingsPath)) return;
            var value = JsonSerializer.Deserialize<LabelPrintSettings>(File.ReadAllText(_paths.LabelSettingsPath), JsonOptions);
            if (value is not null) _current = value;
        }
        catch (IOException) { }
        catch (JsonException) { }
        catch (UnauthorizedAccessException) { }
    }
}
