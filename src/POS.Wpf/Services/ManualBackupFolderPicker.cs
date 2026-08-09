using System.IO;
using Microsoft.Win32;

namespace POS.Wpf.Services;

public sealed class ManualBackupFolderPicker : IManualBackupFolderPicker
{
    public string? PickDestination()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục lưu backup dữ liệu",
            Multiselect = false
        };

        var owner = global::System.Windows.Application.Current?.MainWindow;
        var accepted = owner is null
            ? dialog.ShowDialog()
            : dialog.ShowDialog(owner);

        return accepted == true && Directory.Exists(dialog.FolderName)
            ? Path.GetFullPath(dialog.FolderName)
            : null;
    }
}
