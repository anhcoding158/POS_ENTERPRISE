using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// Một nút lọc danh mục trên màn hình bán hàng.
/// </summary>
public sealed class SalesCategoryFilterViewModel :
    ViewModelBase
{
    private bool _isSelected;

    public SalesCategoryFilterViewModel(
        int? categoryId,
        string name,
        bool isSelected,
        Func<
            SalesCategoryFilterViewModel,
            Task> selectAsync)
    {
        if (string.IsNullOrWhiteSpace(
                name))
        {
            throw new ArgumentException(
                "Tên danh mục không được để trống.",
                nameof(name));
        }

        ArgumentNullException.ThrowIfNull(
            selectAsync);

        CategoryId = categoryId;
        Name = name.Trim();
        _isSelected = isSelected;

        SelectCommand =
            new AsyncRelayCommand(
                () =>
                    selectAsync(this));
    }

    public int? CategoryId { get; }

    public string Name { get; }

    public AsyncRelayCommand
        SelectCommand
    {
        get;
    }

    public bool IsSelected
    {
        get => _isSelected;

        set => SetProperty(
            ref _isSelected,
            value);
    }
}