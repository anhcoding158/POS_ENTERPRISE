using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class EmployeeAccountSuccessWindow : global::System.Windows.Window
{
    public EmployeeAccountSuccessWindow(EmployeeAccountSuccessEventArgs details)
    {
        ArgumentNullException.ThrowIfNull(details);
        InitializeComponent();
        DataContext = SuccessDialogViewModel.FromEmployee(details);
        Title = details.Title;
    }
}
