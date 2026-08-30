using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.ProductImports;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.ProductImports;
using POS.Application.Authorization;
using POS.Application.ProductImports;
using POS.Application.Services;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.ProductImports;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductImportWizardTests
{
    [Fact]
    public async Task Multiple_xlsx_worksheets_require_an_explicit_selection()
    {
        using var fixture = new FixtureScope();
        var path = fixture.WriteWorkbook();
        var parser = new ProductImportPreviewService();

        var selectionRequired = await parser.PreviewAsync(path);

        Assert.Contains(selectionRequired.FileIssues, issue => issue.Code == "WORKSHEET_SELECTION_REQUIRED");
        Assert.Equal(["Products", "Notes"], selectionRequired.WorksheetNames);
        Assert.False(selectionRequired.CanImport);

        var selected = await parser.PreviewAsync(
            path,
            new ProductImportPreviewOptions(WorksheetName: "Products"));

        Assert.True(selected.CanImport);
        Assert.Equal("Products", selected.SelectedWorksheetName);
        Assert.Equal("000123", Assert.Single(selected.PreviewRows).Barcode);
        Assert.Equal(
            ProductImportSchemaCatalog.Fields.Select(field => field.CanonicalKey),
            selected.Headers.Select(header => header.CanonicalFieldKey));
    }

    [Fact]
    public async Task Compact_canonical_headers_auto_map_all_eleven_fields_without_manual_mapping()
    {
        using var fixture = new FixtureScope();
        var result = await new ProductImportPreviewService().PreviewAsync(fixture.WriteCanonicalCsv());

        Assert.True(result.CanImport);
        Assert.Equal(
            ProductImportSchemaCatalog.Fields.Select(field => field.CanonicalKey),
            result.Headers.Select(header => header.CanonicalFieldKey));
        Assert.DoesNotContain(result.FileIssues, issue => issue.Code == "HEADER_UNKNOWN");
        var row = Assert.Single(result.PreviewRows);
        Assert.Equal("000987", row.Barcode);
        Assert.Equal(42000, row.SalePrice);
        Assert.Equal(27500, row.CostPrice);
        Assert.Equal(7, row.InitialStockQuantity);
        Assert.Equal(2, row.MinimumStock);
        Assert.False(row.IsActive);
    }

    [Fact]
    public async Task Explicit_mapping_can_map_unknown_headers_without_silent_drop()
    {
        using var fixture = new FixtureScope();
        var path = fixture.WriteCsv();
        var mapping = ProductImportSchemaCatalog.Fields
            .Select((field, index) => new ProductImportColumnMapping(index, field.CanonicalKey))
            .ToArray();

        var result = await new ProductImportPreviewService().PreviewAsync(
            path,
            new ProductImportPreviewOptions(ColumnMappings: mapping));

        Assert.True(result.CanImport);
        Assert.DoesNotContain(result.FileIssues, issue => issue.Code == "HEADER_UNKNOWN");
        Assert.Equal(ProductImportSchemaCatalog.Fields.Select(field => field.CanonicalKey), result.Headers.Select(header => header.CanonicalFieldKey));
        Assert.Equal("000123", Assert.Single(result.PreviewRows).Barcode);
    }

    [Fact]
    public async Task Wizard_window_constructs_and_arranges_at_supported_sizes()
    {
        await RunOnSta(() =>
        {
            if (global::System.Windows.Application.Current is null)
            {
                var application = new POS.Wpf.App();
                application.InitializeComponent();
            }

            using var provider = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
            using var user = new CurrentUserScope();
            var permissions = new PermissionService(user.Service);
            var viewModel = new ProductImportWizardViewModel(
                provider.GetRequiredService<IServiceScopeFactory>(),
                new ProductImportPreviewService(),
                permissions,
                loggerFactory.CreateLogger<ProductImportWizardViewModel>());
            var window = new ProductImportWizardWindow(viewModel);

            foreach (var size in new[]
            {
                new global::System.Windows.Size(1920, 1080),
                new global::System.Windows.Size(1366, 768),
                new global::System.Windows.Size(1280, 720),
                new global::System.Windows.Size(1000, 640)
            })
            {
                window.Measure(size);
                window.Arrange(new global::System.Windows.Rect(0, 0, size.Width, size.Height));
                window.UpdateLayout();
                Assert.True(window.ActualWidth <= size.Width || window.Width <= size.Width);
                Assert.True(window.ActualHeight <= size.Height || window.Height <= size.Height);
            }

            Assert.Equal("Nhập sản phẩm từ Excel/CSV", window.Title);
            window.Close();
        });
    }

    [Fact]
    public void Wizard_initial_state_has_one_primary_file_action_and_safe_duplicate_choices()
    {
        using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        using var user = new CurrentUserScope();
        var viewModel = new ProductImportWizardViewModel(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new ProductImportPreviewService(),
            new PermissionService(user.Service),
            loggerFactory.CreateLogger<ProductImportWizardViewModel>());

        Assert.Equal(0, viewModel.CurrentStep);
        Assert.True(viewModel.ShowChooseFileAction);
        Assert.False(viewModel.ShowImportAction);
        Assert.False(viewModel.ShowChangeFileAction);
        Assert.False(viewModel.HasPreview);
        Assert.Equal("Chỉ thêm sản phẩm mới", viewModel.DuplicatePolicies[0].DisplayName);
        Assert.Equal("Dừng nếu có sản phẩm trùng", viewModel.DuplicatePolicies[2].DisplayName);
        Assert.Contains("25", viewModel.ImportLimitsHint, StringComparison.Ordinal);
    }

    private static Task RunOnSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private sealed class CurrentUserScope : IDisposable
    {
        public CurrentUserScope()
        {
            Service = new CurrentUserService();
            Service.SetCurrentUser(new AuthenticatedUserDto(
                1,
                "wizard-admin",
                "Wizard Administrator",
                Role.Administrator,
                DateTimeOffset.UtcNow));
        }

        public CurrentUserService Service { get; }
        public void Dispose() => Service.Clear();
    }

    private sealed class FixtureScope : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "POS-Enterprise-R51C-" + Guid.NewGuid().ToString("N"));

        public FixtureScope() => Directory.CreateDirectory(_root);

        public string WriteCsv()
        {
            var path = Path.Combine(_root, "mapped.csv");
            var headers = string.Join(',', Enumerable.Range(1, 11).Select(index => "Nguồn " + index));
            var row = "IMP-WIZ-001,000123,Sản phẩm wizard,Đồ uống,Cái,10000,7000,2,1,Đang bán,Ghi chú";
            File.WriteAllText(path, headers + Environment.NewLine + row, new UTF8Encoding(false));
            return path;
        }

        public string WriteCanonicalCsv()
        {
            var path = Path.Combine(_root, "canonical.csv");
            var headers = "ProductCode,Barcode,Name,Category,UnitName,SalePrice,CostPrice,InitialStock,MinimumStock,IsActive,Description";
            var row = "IMP-CANONICAL-001,000987,Canonical sample,Drinks,Bottle,42000,27500,7,2,Inactive,All eleven fields";
            File.WriteAllText(path, headers + Environment.NewLine + row, new UTF8Encoding(false));
            return path;
        }

        public string WriteWorkbook()
        {
            var path = Path.Combine(_root, "multi.xlsx");
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            WriteEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\" /></Types>");
            WriteEntry(archive, "xl/workbook.xml", "<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Products\" sheetId=\"1\" r:id=\"rId1\" /><sheet name=\"Notes\" sheetId=\"2\" r:id=\"rId2\" /></sheets></workbook>");
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"worksheet\" Target=\"worksheets/sheet1.xml\" /><Relationship Id=\"rId2\" Type=\"worksheet\" Target=\"worksheets/sheet2.xml\" /></Relationships>");
            WriteEntry(archive, "xl/worksheets/sheet1.xml", WorksheetXml());
            WriteEntry(archive, "xl/worksheets/sheet2.xml", WorksheetXml());
            return path;
        }

        private static string WorksheetXml()
        {
            var headers = ProductImportPreviewTestsHeader();
            var row = new[] { "IMP-WIZ-001", "000123", "Sản phẩm wizard", "Đồ uống", "Cái", "10000", "7000", "2", "1", "Đang bán", "" };
            var headerCells = string.Concat(headers.Select((value, index) => InlineCell(1, index, value)));
            var rowCells = string.Concat(row.Select((value, index) => InlineCell(2, index, value)));
            return $"<?xml version=\"1.0\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\">{headerCells}</row><row r=\"2\">{rowCells}</row></sheetData></worksheet>";
        }

        private static string[] ProductImportPreviewTestsHeader() => ["ProductCode", "Barcode", "Name", "Category", "UnitName", "SalePrice", "CostPrice", "InitialStock", "MinimumStock", "IsActive", "Description"];
        private static string InlineCell(int row, int column, string value) => $"<c r=\"{(char)('A' + column)}{row}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(value)}</t></is></c>";

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
