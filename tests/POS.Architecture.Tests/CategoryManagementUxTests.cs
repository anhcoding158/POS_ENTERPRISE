using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class CategoryManagementUxTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void Category_ui_must_not_contain_search_button()
    {
        var document = LoadXaml();
        var buttons = document.Descendants(Presentation + "Button").ToArray();

        Assert.DoesNotContain(buttons, IsSearchButton);
        Assert.DoesNotContain(
            buttons,
            button => (string?)button.Attribute("Content") == "Tìm kiếm");
    }

    [Fact]
    public void Category_ui_must_not_contain_reload_button()
    {
        var document = LoadXaml();
        var buttons = document.Descendants(Presentation + "Button").ToArray();

        Assert.DoesNotContain(
            buttons,
            button => BindingContains(button, "RefreshCommand"));
        Assert.DoesNotContain(
            buttons,
            button => (string?)button.Attribute("Content") == "Tải lại");
    }

    [Fact]
    public void Category_search_debounce_enter_and_status_must_remain_active()
    {
        var document = LoadXaml();
        var codeBehind = Read(
            "src", "POS.Wpf", "Views", "CategoryManagementWindow.xaml.cs");
        var search = document.Descendants(Presentation + "TextBox")
            .Single(element => (string?)element.Attribute(
                XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) ==
                "CategorySearchBox");
        var status = document.Descendants(Presentation + "ComboBox").Single();

        Assert.Equal("OnSearchTextChanged", (string?)search.Attribute("TextChanged"));
        Assert.Equal("OnSearchKeyDown", (string?)search.Attribute("KeyDown"));
        Assert.Equal(
            "OnStatusFilterChanged",
            (string?)status.Attribute("SelectionChanged"));
        Assert.Contains(
            "TimeSpan.FromMilliseconds(300)",
            codeBehind,
            StringComparison.Ordinal);
        Assert.Contains("Input.Key.Enter", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ExecuteSearch();", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_clear_filter_and_management_actions_must_remain()
    {
        var document = LoadXaml();
        var buttons = document.Descendants(Presentation + "Button").ToArray();

        Assert.Contains(
            buttons,
            button => (string?)button.Attribute("Content") == "Xóa lọc" &&
                      BindingContains(button, "ResetFiltersCommand"));
        Assert.Contains(buttons, button => BindingContains(button, "EditCommand"));
        Assert.Contains(
            buttons,
            button => BindingContains(button, "ToggleActiveCommand"));
        Assert.Contains(buttons, button => BindingContains(button, "AddCommand"));
    }

    [Fact]
    public void Category_reload_command_may_remain_for_internal_refresh()
    {
        var viewModel = Read(
            "src", "POS.Wpf", "ViewModels", "CategoryManagementViewModel.cs");

        Assert.Contains(
            "public AsyncRelayCommand RefreshCommand",
            viewModel,
            StringComparison.Ordinal);
        Assert.Contains("RefreshAsync", viewModel, StringComparison.Ordinal);
        Assert.Contains("await LoadPageAsync(", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_filter_and_action_groups_must_be_visually_separate()
    {
        var document = LoadXaml();
        var rootGrid = document.Descendants(Presentation + "Grid")
            .First(grid =>
                grid.Element(Presentation + "Grid.ColumnDefinitions") is not null &&
                grid.Descendants(Presentation + "TextBox").Any(element =>
                    (string?)element.Attribute(
                        XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) ==
                    "CategorySearchBox") &&
                grid.Descendants(Presentation + "Button").Any(element =>
                    BindingContains(element, "AddCommand")));
        var columns = rootGrid
            .Element(Presentation + "Grid.ColumnDefinitions")!
            .Elements(Presentation + "ColumnDefinition")
            .Select(element => (string?)element.Attribute("Width"))
            .ToArray();

        Assert.Equal(3, columns.Length);
        Assert.Equal("*", columns[0]);
        Assert.Equal("24", columns[1]);
        Assert.Equal("Auto", columns[2]);
        Assert.Contains(
            rootGrid.Elements(Presentation + "StackPanel"),
            panel => (string?)panel.Attribute("Grid.Column") == "2");
    }

    [Fact]
    public void Category_controls_must_have_consistent_height()
    {
        var xaml = Read(
            "src", "POS.Wpf", "Views", "CategoryManagementWindow.xaml");

        Assert.True(Count(xaml, "Height=\"42\"") >= 5);
        Assert.Contains(
            "Property=\"Height\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Category_status_combobox_must_be_vertically_centered()
    {
        var document = LoadXaml();
        var comboStyle = document.Descendants(Presentation + "Style")
            .Single(style =>
                (string?)style.Attribute(
                    XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) ==
                "CategoryFilterComboBoxStyle");
        var setters = comboStyle.Elements(Presentation + "Setter").ToArray();

        Assert.Contains(
            setters,
            setter =>
                (string?)setter.Attribute("Property") ==
                    "VerticalContentAlignment" &&
                (string?)setter.Attribute("Value") == "Center");
        Assert.Contains(
            setters,
            setter =>
                (string?)setter.Attribute("Property") == "Background" &&
                (string?)setter.Attribute("Value") ==
                    "{StaticResource SurfaceBrush}");
    }

    [Fact]
    public void Category_layout_must_fit_1366x768_and_add_no_hex_colors()
    {
        var xaml = Read(
            "src", "POS.Wpf", "Views", "CategoryManagementWindow.xaml");

        Assert.DoesNotContain("MinWidth=\"1367", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MinHeight=\"769", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain(
            RunGitDiff()
                .Split('\n')
                .Where(line =>
                    line.Length > 0 &&
                    line[0] == '+' &&
                    !line.StartsWith("+++", StringComparison.Ordinal)),
            line => System.Text.RegularExpressions.Regex.IsMatch(
                line,
                "#[0-9A-Fa-f]{6,8}"));
    }

    [Fact]
    public void Selection_detail_is_information_only()
    {
        var xaml = Read(
            "src", "POS.Wpf", "Views", "CategoryManagementWindow.xaml");

        Assert.Equal(1, Count(xaml, "Command=\"{Binding EditCommand}\""));
        Assert.Equal(1, Count(xaml, "Command=\"{Binding ToggleActiveCommand}\""));
    }

    [Fact]
    public void Category_editor_and_list_must_not_expose_display_order()
    {
        var editor = Read(
            "src", "POS.Wpf", "Views", "CategoryEditorWindow.xaml");
        var management = Read(
            "src", "POS.Wpf", "Views", "CategoryManagementWindow.xaml");

        Assert.DoesNotContain("Thứ tự hiển thị", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("DisplayOrderText", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"THỨ TỰ\"", management, StringComparison.Ordinal);
        Assert.DoesNotContain("thứ tự hiển thị", management, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Category_window_must_construct_on_STA()
    {
        var completion = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current is null)
                {
                    var application = new POS.Wpf.App();
                    application.InitializeComponent();
                }

                var services = new ServiceCollection().BuildServiceProvider();
                var viewModel = new CategoryManagementViewModel(
                    services.GetRequiredService<IServiceScopeFactory>(),
                    new CategoryDialogFake(),
                    NullLogger<CategoryManagementViewModel>.Instance);
                var window = new CategoryManagementWindow(viewModel);
                window.Close();
                completion.SetResult(null);
            }
            catch (Exception exception)
            {
                completion.SetResult(exception);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.Null(await completion.Task);
        thread.Join();
    }

    private static bool IsSearchButton(XElement button) =>
        BindingContains(button, "SearchCommand");

    private static bool BindingContains(XElement element, string command) =>
        element.Attributes().Any(attribute =>
            attribute.Value.Contains(command, StringComparison.Ordinal));

    private static XDocument LoadXaml() =>
        XDocument.Load(PathOf(
            "src", "POS.Wpf", "Views", "CategoryManagementWindow.xaml"));

    private static int Count(string source, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string PathOf(params string[] parts) =>
        Path.Combine([FindRepositoryRoot(), .. parts]);

    private static string Read(params string[] parts) =>
        File.ReadAllText(PathOf(parts));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "POS.Enterprise.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }

    private static string RunGitDiff()
    {
        var start = new System.Diagnostics.ProcessStartInfo(
            "git",
            "diff --unified=0")
        {
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private sealed class CategoryDialogFake : ICategoryDialogService
    {
        public Task<bool> ShowCreateAsync() => Task.FromResult(false);

        public Task<bool> ShowEditAsync(int categoryId) => Task.FromResult(false);
    }
}
