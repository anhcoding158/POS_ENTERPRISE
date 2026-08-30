using System.Windows;
using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class BulkProductWindow : Window
{
    public BulkProductWindow(BulkProductViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += result => { DialogResult = result; Close(); };
    }
}
