namespace POS.Wpf.Views;

public partial class SuccessDialogWindow : global::System.Windows.Window
{
    public SuccessDialogWindow(SuccessDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        InitializeComponent();
        DataContext = SuccessDialogViewModel.From(request);
        Title = request.Title;
    }
}
