using POS.Application.Common;
using POS.Domain.Common;
using POS.Infrastructure;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Khóa chiều phụ thuộc Clean Architecture.
///
/// Những test này ngăn một lần sửa trong tương lai vô tình
/// đưa EF Core/WPF vào Domain hoặc Infrastructure vào Application.
/// </summary>
public sealed class ArchitectureDependencyTests
{
    [Fact]
    public void Domain_must_not_reference_outer_layers()
    {
        var references =
            GetReferencedAssemblyNames(
                typeof(Entity).Assembly);

        Assert.DoesNotContain(
            "POS.Application",
            references);

        Assert.DoesNotContain(
            "POS.Infrastructure",
            references);

        Assert.DoesNotContain(
            "POS.Wpf",
            references);

        Assert.DoesNotContain(
            "Microsoft.EntityFrameworkCore",
            references);

        Assert.DoesNotContain(
            "PresentationFramework",
            references);
    }

    [Fact]
    public void Application_must_not_reference_infrastructure_or_wpf()
    {
        var references =
            GetReferencedAssemblyNames(
                typeof(Error).Assembly);

        Assert.Contains(
            "POS.Domain",
            references);

        Assert.DoesNotContain(
            "POS.Infrastructure",
            references);

        Assert.DoesNotContain(
            "POS.Wpf",
            references);

        Assert.DoesNotContain(
            "Microsoft.EntityFrameworkCore",
            references);

        Assert.DoesNotContain(
            "PresentationFramework",
            references);
    }

    [Fact]
    public void Infrastructure_may_reference_application_and_domain()
    {
        var references =
            GetReferencedAssemblyNames(
                typeof(InfrastructureOptions)
                    .Assembly);

        Assert.Contains(
            "POS.Application",
            references);

        Assert.Contains(
            "POS.Domain",
            references);
    }

    [Fact]
    public void Domain_types_must_not_have_entity_framework_attributes()
    {
        var domainAssembly =
            typeof(Entity).Assembly;

        var entityFrameworkAttributes =
            domainAssembly
                .DefinedTypes
                .SelectMany(
                    type =>
                        type.CustomAttributes)
                .Where(
                    attribute =>
                        attribute.AttributeType
                            .Namespace?
                            .StartsWith(
                                "Microsoft.EntityFrameworkCore",
                                StringComparison.Ordinal) ==
                        true)
                .Select(
                    attribute =>
                        attribute.AttributeType.FullName)
                .Where(
                    name =>
                        name is not null)
                .ToArray();

        Assert.Empty(
            entityFrameworkAttributes);
    }

    private static IReadOnlySet<string>
        GetReferencedAssemblyNames(
            Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(
                reference =>
                    reference.Name)
            .Where(
                name =>
                    !string.IsNullOrWhiteSpace(name))
            .Select(
                name =>
                    name!)
            .ToHashSet(
                StringComparer.Ordinal);
    }
}