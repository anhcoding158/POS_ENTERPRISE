using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS.Wpf.ViewModels;

/// <summary>
/// Nền tảng cho toàn bộ ViewModel WPF.
/// </summary>
public abstract class ViewModelBase :
    INotifyPropertyChanged
{
    public event PropertyChangedEventHandler?
        PropertyChanged;

    protected bool SetProperty<TValue>(
        ref TValue field,
        TValue value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<TValue>.Default.Equals(
                field,
                value))
        {
            return false;
        }

        field = value;

        OnPropertyChanged(propertyName);

        return true;
    }

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}