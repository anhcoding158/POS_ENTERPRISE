namespace POS.Wpf.Services;

public interface IStoreSettingsFilePicker
{
    string? PickLogo();
}

public sealed class StoreSettingsFilePicker : IStoreSettingsFilePicker
{
    public string? PickLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Ảnh logo|*.png;*.jpg;*.jpeg;*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
