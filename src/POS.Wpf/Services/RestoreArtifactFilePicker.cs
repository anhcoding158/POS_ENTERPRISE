using Microsoft.Win32;

namespace POS.Wpf.Services;

public sealed class RestoreArtifactFilePicker : IRestoreArtifactFilePicker
{
    internal const string ArtifactFilter = "POS Enterprise backup (*.db)|*.db";

    public string? PickArtifact()
    {
        var dialog = new OpenFileDialog
        {
            Filter = ArtifactFilter,
            Multiselect = false,
            CheckFileExists = true,
            CheckPathExists = true,
            AddExtension = true,
            DefaultExt = ".db",
            DereferenceLinks = false,
            Title = "Chọn bản sao lưu POS Enterprise"
        };

        var application = global::System.Windows.Application.Current;
        var owner = application?.Windows
            .OfType<global::System.Windows.Window>()
            .FirstOrDefault(window => window.IsVisible && window.IsActive)
            ?? application?.MainWindow;
        var accepted = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return accepted == true ? dialog.FileName : null;
    }
}
