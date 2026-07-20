using System;
using System.IO;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm tra các quy tắc source-code không thể hiện
/// đầy đủ bằng ProjectReference.
/// </summary>
public sealed class ProjectStructureTests
{
    private static readonly string[]
        ProductVerticalSliceFiles =
        [
            "src/POS.Application/Services/ProductService.cs",

            "src/POS.Infrastructure/Persistence/" +
            "Repositories/ProductRepository.cs",

            "src/POS.Infrastructure/Persistence/" +
            "Repositories/CategoryRepository.cs",

            "src/POS.Infrastructure/Persistence/" +
            "PosDbContext.cs",

            "src/POS.Infrastructure/Persistence/" +
            "DatabaseInitializer.cs",

            "src/POS.Wpf/ViewModels/" +
            "ShellViewModel.cs",

            "src/POS.Wpf/ViewModels/" +
            "ProductEditorViewModel.cs",

            "src/POS.Wpf/Views/" +
            "ProductEditorWindow.xaml",

            "src/POS.Wpf/Views/" +
            "ShellWindow.xaml"
        ];

    [Fact]
    public void Product_vertical_slice_files_must_exist()
    {
        foreach (var relativePath in
                 ProductVerticalSliceFiles)
        {
            var fullPath =
                RepositoryLocator.GetPath(
                    SplitPath(relativePath));

            Assert.True(
                File.Exists(fullPath),
                $"Thiếu file Product slice: {relativePath}");
        }
    }

    [Fact]
    public void Product_vertical_slice_must_not_contain_scaffold_placeholder()
    {
        const string placeholder =
            "TODO: Paste the reviewed implementation";

        foreach (var relativePath in
                 ProductVerticalSliceFiles)
        {
            var fullPath =
                RepositoryLocator.GetPath(
                    SplitPath(relativePath));

            Assert.True(
                File.Exists(fullPath),
                $"Thiếu file Product slice: {relativePath}");

            var content =
                File.ReadAllText(fullPath);

            Assert.DoesNotContain(
                placeholder,
                content,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Domain_source_must_not_depend_on_ef_core_or_wpf()
    {
        var domainDirectory =
            RepositoryLocator.GetPath(
                "src",
                "POS.Domain");

        AssertForbiddenTextIsAbsent(
            domainDirectory,
            "*.cs",
            [
                "using Microsoft.EntityFrameworkCore",
                "using System.Windows",
                "using POS.Infrastructure",
                "using POS.Wpf"
            ]);
    }

    [Fact]
    public void Application_source_must_not_depend_on_infrastructure_or_wpf()
    {
        var applicationDirectory =
            RepositoryLocator.GetPath(
                "src",
                "POS.Application");

        AssertForbiddenTextIsAbsent(
            applicationDirectory,
            "*.cs",
            [
                "using Microsoft.EntityFrameworkCore",
                "using Microsoft.Data.Sqlite",
                "using System.Windows",
                "using POS.Infrastructure",
                "using POS.Wpf"
            ]);
    }

    [Fact]
    public void Repository_contracts_must_not_expose_iqueryable()
    {
        var contractsDirectory =
            RepositoryLocator.GetPath(
                "src",
                "POS.Application",
                "Abstractions",
                "Persistence");

        var contractFiles =
            Directory.GetFiles(
                contractsDirectory,
                "*.cs",
                SearchOption.TopDirectoryOnly);

        Assert.NotEmpty(
            contractFiles);

        foreach (var contractFile in
                 contractFiles)
        {
            var content =
                File.ReadAllText(
                    contractFile);

            Assert.DoesNotContain(
                "IQueryable<",
                content,
                StringComparison.Ordinal);
        }
    }

    private static void AssertForbiddenTextIsAbsent(
        string directory,
        string searchPattern,
        IReadOnlyCollection<string> forbiddenTexts)
    {
        var files =
            Directory.GetFiles(
                directory,
                searchPattern,
                SearchOption.AllDirectories);

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var content =
                File.ReadAllText(file);

            foreach (var forbiddenText in
                     forbiddenTexts)
            {
                Assert.DoesNotContain(
                    forbiddenText,
                    content,
                    StringComparison.Ordinal);
            }
        }
    }

    private static string[] SplitPath(
        string relativePath)
    {
        return relativePath.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
    }
}