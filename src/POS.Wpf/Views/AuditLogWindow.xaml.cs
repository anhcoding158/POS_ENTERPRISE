using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class AuditLogWindow : global::System.Windows.Window
{
    public AuditLogWindow(AuditLogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
