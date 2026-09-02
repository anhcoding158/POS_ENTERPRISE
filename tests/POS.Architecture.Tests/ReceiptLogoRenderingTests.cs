using POS.Application.Abstractions.StoreSetup;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Printing;
using POS.Application.Services;
using POS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Printing;
using POS.Infrastructure.StoreSetup;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ReceiptLogoRenderingTests
{
    private static readonly byte[]
        OnePixelPng =
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public void Receipt_store_snapshot_must_round_trip_embedded_logo()
    {
        var source =
            CreateReceipt(
                new ReceiptStoreSnapshotDto(
                    "MiniMart",
                    logoBytes: OnePixelPng,
                    logoMimeType: "image/png"));

        var serializer = new ReceiptSnapshotJsonSerializer();
        var json = serializer.Serialize(source);
        var restored = serializer.Deserialize(json);

        Assert.True(restored.Store.HasLogo);
        Assert.Equal("image/png", restored.Store.LogoMimeType);
        Assert.Equal(
            OnePixelPng,
            restored.Store.LogoBytes!.ToArray());
        Assert.Contains("logoBase64", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_snapshot_without_logo_must_render_monogram_fallback()
    {
        RunOnSta(
            () =>
            {
                var serializer = new ReceiptSnapshotJsonSerializer();
                var json =
                    serializer.Serialize(
                        CreateReceipt(
                            new ReceiptStoreSnapshotDto("MiniMart")));

                var legacyJson =
                    json.Replace(
                        ",\"logoBase64\":null,\"logoMimeType\":null",
                        string.Empty,
                        StringComparison.Ordinal);

                var restored = serializer.Deserialize(legacyJson);
                var document = ReceiptDocumentBuilder.Build(restored);
                var brandCell = FindBrandCells(document).Single();

                Assert.Equal(
                    "Receipt.BrandMonogram",
                    brandCell.Tag);
                Assert.Contains(
                    "P\nE",
                    ReadCellText(brandCell),
                    StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Valid_logo_must_replace_monogram_in_renderer()
    {
        RunOnSta(
            () =>
            {
                var document =
                    ReceiptDocumentBuilder.Build(
                        CreateReceipt(
                            new ReceiptStoreSnapshotDto(
                                "MiniMart",
                                logoBytes: OnePixelPng,
                                logoMimeType: "image/png")));

                var brandCell = FindBrandCells(document).Single();
                var inlineImage =
                    Assert.IsType<InlineUIContainer>(
                        Assert.Single(
                            ((Paragraph)brandCell.Blocks.Single()).Inlines));
                var image = Assert.IsType<Image>(inlineImage.Child);

                Assert.Equal("Receipt.BrandLogo", brandCell.Tag);
                Assert.Equal("Receipt.BrandLogoImage", image.Tag);
                Assert.IsType<BitmapImage>(image.Source);
                Assert.Equal(
                    System.Windows.Media.Stretch.Uniform,
                    image.Stretch);
                Assert.Equal(
                    System.Windows.VerticalAlignment.Center,
                    image.VerticalAlignment);
                Assert.Equal(
                    System.Windows.BaselineAlignment.Center,
                    inlineImage.BaselineAlignment);
                Assert.Equal(
                    System.Windows.LineStackingStrategy.BlockLineHeight,
                    ((Paragraph)brandCell.Blocks.Single()).LineStackingStrategy);
                Assert.DoesNotContain(
                    "P\nE",
                    ReadCellText(brandCell),
                    StringComparison.Ordinal);
                Assert.DoesNotContain(
                    "Receipt.BrandMonogram",
                    FindBrandCells(document)
                        .Select(cell => cell.Tag?.ToString())
                        .ToArray());
            });
    }

    [Fact]
    public void Corrupt_logo_must_fall_back_without_failing_render()
    {
        RunOnSta(
            () =>
            {
                var document =
                    ReceiptDocumentBuilder.Build(
                        CreateReceipt(
                            new ReceiptStoreSnapshotDto(
                                "MiniMart",
                                logoBytes: new byte[] { 1, 2, 3, 4 },
                                logoMimeType: "image/png")));

                Assert.Equal(
                    "Receipt.BrandMonogram",
                    FindBrandCells(document).Single().Tag);
            });
    }

    [Fact]
    public void Reprint_must_keep_logo_from_original_snapshot()
    {
        var original =
            CreateReceipt(
                new ReceiptStoreSnapshotDto(
                    "MiniMart",
                    logoBytes: OnePixelPng,
                    logoMimeType: "image/png"));

        var reprint =
            POS.Application.Factories.ReceiptSnapshotFactory.CreateReprint(
                original,
                1);

        Assert.Equal(original.Store.LogoMimeType, reprint.Store.LogoMimeType);
        Assert.Equal(original.Store.LogoBytes, reprint.Store.LogoBytes);
    }

    [Fact]
    public void Snapshot_provider_must_capture_logo_content_at_checkout_boundary()
    {
        var settings =
            new TestStoreSettingsStore(
                new StoreSettingsSnapshot
                {
                    StoreName = "MiniMart",
                    LogoAssetName = "logo-a.png"
                });
        var logoProvider =
            new TestLogoContentProvider(
                new StoreLogoContent(OnePixelPng, "image/png"));

        var provider =
            new ReceiptStoreSnapshotProvider(
                settings,
                logoProvider);

        var snapshot = provider.GetCurrentSnapshot();
        logoProvider.Content = null;
        settings.CurrentValue = settings.CurrentValue with { LogoAssetName = null };

        Assert.True(snapshot.HasLogo);
        Assert.Equal(
            OnePixelPng,
            snapshot.LogoBytes!.ToArray());
        Assert.Equal("image/png", snapshot.LogoMimeType);
    }

    [Fact]
    public void Managed_logo_reader_must_release_source_file_after_embedding()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "pos-receipt-logo-" + Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(root, "fixture.db");

        try
        {
            var paths =
                new StoreSettingsPathProvider(
                    DatabaseRuntimeGuard.IsolatedTestMode,
                    databasePath,
                    root);
            Directory.CreateDirectory(paths.LogoRoot);
            var sourcePath = Path.Combine(paths.LogoRoot, "logo-a.png");
            File.WriteAllBytes(sourcePath, OnePixelPng);

            var service = new ManagedLogoService(paths);
            var content = service.TryRead("logo-a.png");

            File.Delete(sourcePath);

            Assert.NotNull(content);
            Assert.Equal("image/png", content!.MimeType);
            Assert.NotEmpty(content.Bytes);
            Assert.NotEqual(
                Convert.ToBase64String(OnePixelPng),
                Convert.ToBase64String(content.Bytes.ToArray()));
            Assert.False(File.Exists(sourcePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Managed_logo_reader_must_bound_large_valid_logo_before_receipt_embedding()
    {
        RunOnSta(
            () =>
            {
                var root =
                    Path.Combine(
                        Path.GetTempPath(),
                        "pos-receipt-large-logo-" + Guid.NewGuid().ToString("N"));
                var databasePath = Path.Combine(root, "fixture.db");

                try
                {
                    Directory.CreateDirectory(root);
                    var sourcePath = Path.Combine(root, "source.png");
                    WriteDeterministicRasterPng(sourcePath, 700, 500);
                    Assert.True(
                        new FileInfo(sourcePath).Length >
                        StoreLogoContent.MaximumBytes);

                    var paths =
                        new StoreSettingsPathProvider(
                            DatabaseRuntimeGuard.IsolatedTestMode,
                            databasePath,
                            root);
                    var service = new ManagedLogoService(paths);
                    var assetName =
                        service.ImportAsync(sourcePath)
                            .GetAwaiter()
                            .GetResult();

                    var content = service.TryRead(assetName);

                    Assert.NotNull(content);
                    Assert.Equal("image/png", content!.MimeType);
                    Assert.InRange(
                        content.Bytes.Count,
                        1,
                        StoreLogoContent.MaximumBytes);
                }
                finally
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, recursive: true);
                    }
                }
            });
    }

    [Fact]
    public async Task Production_store_settings_and_receipt_pipeline_must_keep_logo_after_reload()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "pos-receipt-logo-e2e-" + Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(root, "fixture.db");
        string? assetName = null;

        try
        {
            using (var firstProvider =
                   BuildReceiptLogoProvider(root, databasePath))
            {
                var paths =
                    firstProvider.GetRequiredService<StoreSettingsPathProvider>();
                var logos =
                    firstProvider.GetRequiredService<ManagedLogoService>();
                var store =
                    firstProvider.GetRequiredService<IStoreSettingsStore>();

                Directory.CreateDirectory(root);
                var sourcePath = Path.Combine(root, "source-logo.png");
                File.WriteAllBytes(sourcePath, OnePixelPng);

                assetName = await logos.ImportAsync(sourcePath);
                File.Delete(sourcePath);

                var managedPath = logos.GetManagedPath(assetName);
                Assert.NotNull(managedPath);
                Assert.True(File.Exists(managedPath));

                var draft =
                    StoreSettingsDefaults.Create(
                        paths.EffectiveDatabaseDirectory,
                        paths.DefaultBackupDirectory) with
                    {
                        StoreName = "MiniMart",
                        Address = "Địa chỉ cửa hàng",
                        Hotline = "0999 888 999",
                        LogoAssetName = assetName
                    };

                var save =
                    await store.SaveAsync(
                        draft,
                        store.Current.Version);

                Assert.True(save.IsSuccess);
                Assert.Equal(assetName, save.Settings!.LogoAssetName);

                var snapshot =
                    firstProvider
                        .GetRequiredService<IReceiptStoreSnapshotProvider>()
                        .GetCurrentSnapshot();

                AssertReceiptContainsLogo(
                    snapshot,
                    firstProvider.GetRequiredService<IReceiptSnapshotSerializer>());
            }

            // A new composition confirms that the receipt path does not rely
            // on the in-memory Store Settings instance from the first provider.
            using (var restartedProvider =
                   BuildReceiptLogoProvider(root, databasePath))
            {
                var store =
                    restartedProvider.GetRequiredService<IStoreSettingsStore>();
                Assert.Equal(assetName, store.Current.LogoAssetName);

                var snapshot =
                    restartedProvider
                        .GetRequiredService<IReceiptStoreSnapshotProvider>()
                        .GetCurrentSnapshot();

                AssertReceiptContainsLogo(
                    snapshot,
                    restartedProvider.GetRequiredService<IReceiptSnapshotSerializer>());
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Production_app_composition_must_inject_configured_receipt_provider_into_checkout()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "pos-receipt-composition-" + Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(root, "fixture.db");
        var previousRuntimeMode =
            Environment.GetEnvironmentVariable(
                DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable);

        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable(
                DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable,
                DatabaseRuntimeGuard.IsolatedTestMode);

            var repositoryRoot =
                Path.GetFullPath(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "..",
                        "..",
                        "..",
                        "..",
                        ".."));
            var configuration =
                new ConfigurationBuilder()
                    .SetBasePath(Path.Combine(repositoryRoot, "src", "POS.Wpf"))
                    .AddJsonFile(
                        "appsettings.json",
                        optional: false,
                        reloadOnChange: false)
                    .AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Infrastructure:DatabasePath"] = databasePath,
                            ["Infrastructure:SeedDefaultAdministrator"] = bool.FalseString
                        })
                    .Build();

            var services = new ServiceCollection();
            services.AddLogging();

            typeof(POS.Wpf.App)
                .GetMethod(
                    "ConfigureApplicationServices",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(null, [services, configuration]);

            using var serviceProvider =
                services.BuildServiceProvider(
                    new ServiceProviderOptions
                    {
                        ValidateOnBuild = true,
                        ValidateScopes = true
                    });
            using var scope = serviceProvider.CreateScope();

            var configuredProvider =
                serviceProvider
                    .GetRequiredService<IReceiptStoreSnapshotProvider>();
            var checkout =
                scope.ServiceProvider
                    .GetRequiredService<CheckoutService>();
            var providerField =
                typeof(CheckoutService).GetField(
                    "_receiptStoreSnapshotProvider",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(providerField);
            Assert.Same(
                configuredProvider,
                providerField!.GetValue(checkout));
            Assert.IsType<AuthorizedCheckoutService>(
                scope.ServiceProvider
                    .GetRequiredService<ICheckoutService>());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable,
                previousRuntimeMode);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ServiceProvider BuildReceiptLogoProvider(
        string root,
        string databasePath)
    {
        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Infrastructure:DatabasePath"] = databasePath,
                        ["Store:Name"] = "MiniMart",
                        ["Store:Address"] = "Địa chỉ cửa hàng",
                        ["Store:Phone"] = "0999 888 999"
                    })
                .Build();

        var previousRuntimeMode =
            Environment.GetEnvironmentVariable(
                DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable);
        Environment.SetEnvironmentVariable(
            DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable,
            DatabaseRuntimeGuard.IsolatedTestMode);

        var services = new ServiceCollection();
        try
        {
            services.AddLogging();
            services.AddInfrastructure(configuration);

            var provider = services.BuildServiceProvider();
            // Force the environment-dependent path registration while the
            // isolated mode is active; later resolutions use this singleton.
            _ = provider.GetRequiredService<StoreSettingsPathProvider>();
            return provider;
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DatabaseRuntimeGuard.RuntimeModeEnvironmentVariable,
                previousRuntimeMode);
        }
    }

    private static void AssertReceiptContainsLogo(
        ReceiptStoreSnapshotDto store,
        IReceiptSnapshotSerializer serializer)
    {
        Assert.True(store.HasLogo);
        Assert.Equal("image/png", store.LogoMimeType);
        Assert.NotEmpty(store.LogoBytes!);

        var roundTripped =
            serializer.Deserialize(
                serializer.Serialize(CreateReceipt(store)));

        Assert.True(roundTripped.Store.HasLogo);
        Assert.Equal(store.LogoBytes, roundTripped.Store.LogoBytes);

        RunOnSta(
            () =>
            {
                var document = ReceiptDocumentBuilder.Build(roundTripped);
                var brandCell = FindBrandCells(document).Single();

                Assert.Equal("Receipt.BrandLogo", brandCell.Tag);
                Assert.IsType<InlineUIContainer>(
                    Assert.Single(
                        ((Paragraph)brandCell.Blocks.Single()).Inlines));
                Assert.DoesNotContain(
                    "Receipt.BrandMonogram",
                    FindBrandCells(document)
                        .Select(cell => cell.Tag?.ToString())
                        .ToArray());
            });
    }

    private static TableCell[] FindBrandCells(
        FlowDocument document)
    {
        var table =
            document.Blocks
                .OfType<Table>()
                .Single(table =>
                    string.Equals(
                        table.Tag?.ToString(),
                        "Receipt.BrandHeader",
                        StringComparison.Ordinal));

        return table.RowGroups
            .SelectMany(group => group.Rows)
            .SelectMany(row => row.Cells)
            .Where(cell =>
                string.Equals(
                    cell.Tag?.ToString(),
                    "Receipt.BrandMonogram",
                    StringComparison.Ordinal) ||
                string.Equals(
                    cell.Tag?.ToString(),
                    "Receipt.BrandLogo",
                    StringComparison.Ordinal))
            .ToArray();
    }

    private static string ReadCellText(TableCell cell) =>
        string.Concat(
            cell.Blocks
                .OfType<Paragraph>()
                .SelectMany(paragraph => paragraph.Inlines)
                .OfType<Run>()
                .Select(run => run.Text));

    private static ReceiptRequest CreateReceipt(
        ReceiptStoreSnapshotDto store)
    {
        var line =
            new ReceiptLineDto(
                1,
                1,
                "SKU-1",
                "Sản phẩm",
                "Cái",
                1,
                10_000,
                0,
                10_000,
                10_000,
                0,
                10_000,
                null,
                []);

        return new ReceiptRequest(
            store,
            ReceiptCopyKind.Original,
            0,
            1,
            "HD-1",
            "Admin POS",
            new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero),
            PaymentMethod.Cash,
            10_000,
            0,
            10_000,
            10_000,
            0,
            [line]);
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(
                failure.ToString());
        }
    }

    private static void WriteDeterministicRasterPng(
        string path,
        int width,
        int height)
    {
        var bitmap =
            new WriteableBitmap(
                width,
                height,
                96,
                96,
                PixelFormats.Bgr32,
                null);
        var pixels = new byte[width * height * 4];
        new Random(20260902).NextBytes(pixels);
        bitmap.WritePixels(
            new System.Windows.Int32Rect(0, 0, width, height),
            pixels,
            width * 4,
            0);

        using var output = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(output);
    }

    private sealed class TestStoreSettingsStore(StoreSettingsSnapshot initial) : IStoreSettingsStore
    {
        public StoreSettingsSnapshot CurrentValue { get; set; } = initial;
        public StoreSettingsSnapshot Current => CurrentValue;

        public Task<StoreSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoreSettingsLoadResult(CurrentValue, [], false));

        public Task<StoreSettingsSaveResult> SaveAsync(
            StoreSettingsSnapshot settings,
            long expectedVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoreSettingsSaveResult(StoreSettingsSaveStatus.Success, settings));
    }

    private sealed class TestLogoContentProvider(StoreLogoContent? content) : IStoreSettingsLogoContentProvider
    {
        public StoreLogoContent? Content { get; set; } = content;

        public StoreLogoContent? TryRead(string? assetName) => Content;
    }
}
