using POS.Application.Abstractions.Services;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class RestoreWizardUiTests
{
    private const string Artifact = @"D:\isolated\backup.db";

    [Fact]
    public async Task Picker_cancellation_is_not_a_failure()
    {
        using var vm = Create(picker: new Picker(null));
        await vm.PickArtifactAsync();
        Assert.Equal(RestoreWizardState.SelectArtifact, vm.State);
        Assert.DoesNotContain("lỗi", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_picker_has_exact_db_filter_and_disables_multiselect()
    {
        var source = Source("src", "POS.Wpf", "Services", "RestoreArtifactFilePicker.cs");
        Assert.Contains("POS Enterprise backup (*.db)|*.db", source, StringComparison.Ordinal);
        Assert.Contains("Multiselect = false", source, StringComparison.Ordinal);
        Assert.Contains("window.IsVisible && window.IsActive", source, StringComparison.Ordinal);
        Assert.Contains("?? application?.MainWindow", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Valid_artifact_flows_to_compatibility_then_requires_confirmation()
    {
        using var vm = Create();
        await vm.PickArtifactAsync();
        Assert.Equal(RestoreWizardState.CompatibilityReady, vm.State);
        Assert.Equal("backup.db", vm.SafeFileName);
        await vm.ContinueAsync();
        Assert.Equal(RestoreWizardState.ImpactWarning, vm.State);
        Assert.False(vm.CanContinue);
        vm.ConfirmationAccepted = true;
        Assert.Equal(RestoreWizardState.AwaitingConfirmation, vm.State);
        Assert.True(vm.CanContinue);
    }

    [Fact]
    public async Task Older_legacy_artifact_has_both_required_warnings()
    {
        var inspection = Valid() with
        {
            Status = RestoreArtifactStatus.ValidLegacyUnattested,
            Provenance = RestoreArtifactProvenance.LegacyUnattested,
            SchemaCompatibility = RestoreSchemaCompatibility.OlderCompatible
        };
        using var vm = Create(inspector: new Inspector(inspection));
        await vm.PickArtifactAsync();
        Assert.Contains("không có checksum nguồn gốc", vm.WarningText, StringComparison.Ordinal);
        Assert.Contains("nâng cấp schema", vm.WarningText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RestoreArtifactStatus.InvalidArtifact)]
    [InlineData(RestoreArtifactStatus.ChecksumMismatch)]
    [InlineData(RestoreArtifactStatus.ActiveDatabaseConflict)]
    [InlineData(RestoreArtifactStatus.UnsafeReparsePath)]
    public async Task Invalid_or_unsafe_artifact_never_reaches_confirmation(RestoreArtifactStatus status)
    {
        using var vm = Create(inspector: new Inspector(Valid() with { Status = status }));
        await vm.PickArtifactAsync();
        Assert.Equal(RestoreWizardState.Failure, vm.State);
        Assert.False(vm.CanContinue);
        Assert.False(vm.ShowConfirmation);
    }

    [Fact]
    public async Task Preparation_is_single_flight()
    {
        var pending = new TaskCompletionSource<RestorePreparationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparation = new Preparation((_, _) => pending.Task);
        using var vm = Create(preparation: preparation);
        await ConfirmAsync(vm);
        var first = vm.ContinueAsync();
        var second = vm.ContinueAsync();
        Assert.Equal(1, preparation.CallCount);
        pending.SetResult(FailurePreparation(RestoreExecutionStatus.PreRestoreBackupFailed));
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task Durable_prepared_locks_navigation_and_launch_failure_retries_same_plan()
    {
        var launcher = new Launcher(false, true);
        using var vm = Create(preparation: new Preparation((_, _) => Task.FromResult(Prepared())), launcher: launcher);
        await ConfirmAsync(vm);
        await vm.ContinueAsync();
        Assert.True(vm.HasDurablePreparation);
        Assert.False(vm.CanGoBack);
        Assert.False(vm.CanCancel);
        Assert.False(vm.CanClose);
        Assert.True(vm.CanRetryWorker);
        await vm.RetryWorkerAsync();
        Assert.Equal(2, launcher.CallCount);
        Assert.All(launcher.Operations, id => Assert.Equal(Prepared().OperationId, id));
    }

    [Fact]
    public async Task Worker_start_requests_parent_shutdown_exactly_once()
    {
        var shutdowns = 0;
        using var vm = Create(preparation: new Preparation((_, _) => Task.FromResult(Prepared())),
            launcher: new Launcher(true), shutdown: () => shutdowns++);
        await ConfirmAsync(vm);
        await vm.ContinueAsync();
        await vm.ContinueAsync();
        Assert.Equal(1, shutdowns);
        Assert.Equal(RestoreWizardState.RestoringExternally, vm.State);
    }

    [Fact]
    public void Required_automation_ids_and_owner_modal_shell_entry_are_present()
    {
        var xaml = Source("src", "POS.Wpf", "Views", "RestoreWizardWindow.xaml");
        foreach (var id in new[] { "RestoreArtifactPickerButton", "RestoreArtifactFileName",
                     "RestoreInspectionStatus", "RestoreCompatibilityStatus", "RestoreProvenanceStatus",
                     "RestoreConfirmationCheckBox", "RestoreBackButton", "RestoreCancelButton",
                     "RestoreContinueButton", "RestoreRetryWorkerButton", "RestoreProgressIndicator",
                     "RestoreTerminalStatus" }) Assert.Contains(id, xaml, StringComparison.Ordinal);
        var shell = Source("src", "POS.Wpf", "Views", "ShellWindow.xaml");
        Assert.Contains("RestoreDataNavigationButton", shell, StringComparison.Ordinal);
        Assert.Contains("ManualBackupNavigationButton", shell, StringComparison.Ordinal);
        Assert.Contains("AutomaticBackupStatusSurface", shell, StringComparison.Ordinal);
        var code = Source("src", "POS.Wpf", "Views", "ShellWindow.xaml.cs");
        Assert.Contains("window.Owner = this", code, StringComparison.Ordinal);
        Assert.Contains("window.ShowDialog()", code, StringComparison.Ordinal);
        Assert.Contains("Role.Administrator", code, StringComparison.Ordinal);
    }

    [Fact]
    public void UI_and_viewmodel_do_not_present_raw_token_plan_or_exception()
    {
        var xaml = Source("src", "POS.Wpf", "Views", "RestoreWizardWindow.xaml");
        Assert.DoesNotContain("OneTimeOperationToken", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("OpaquePlanPath", xaml, StringComparison.Ordinal);
        var vm = Source("src", "POS.Wpf", "ViewModels", "RestoreWizardViewModel.cs");
        Assert.DoesNotContain("exception.Message", vm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection string", vm, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ConfirmAsync(RestoreWizardViewModel vm)
    {
        await vm.PickArtifactAsync();
        await vm.ContinueAsync();
        vm.ConfirmationAccepted = true;
    }

    private static RestoreWizardViewModel Create(
        Picker? picker = null, Inspector? inspector = null, Preparation? preparation = null,
        Launcher? launcher = null, Action? shutdown = null) =>
        new(picker ?? new Picker(Artifact), inspector ?? new Inspector(Valid()),
            preparation ?? new Preparation((_, _) => Task.FromResult(FailurePreparation(RestoreExecutionStatus.UnexpectedFailure))),
            launcher ?? new Launcher(false), shutdown ?? (() => { }));

    private static RestoreArtifactInspection Valid() => new(
        RestoreArtifactStatus.Valid, RestoreArtifactKind.Manual,
        RestoreArtifactProvenance.AutomaticStateAttested, RestoreSchemaCompatibility.Current,
        "backup.db", 1024, new string('A', 64), 12, "M12", "M12", "Restore.Valid");

    private static RestorePreparationResult Prepared() => new(
        RestoreExecutionStatus.Success, "Restore.Prepared",
        Guid.Parse("5f962e56-3c80-45ec-ad13-8253344d741b"), RestoreOperationState.Prepared,
        @"D:\isolated\restore-operations\5f962e56-3c80-45ec-ad13-8253344d741b\operation.json",
        Convert.ToBase64String(new byte[32]), "safety.db", 1024, new string('B', 64));

    private static RestorePreparationResult FailurePreparation(RestoreExecutionStatus status) =>
        new(status, "Restore.Failed", Guid.Empty, null, null, null, null, null, null);

    private static string Source(params string[] parts) => File.ReadAllText(
        Path.Combine(new[] { Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")) }
            .Concat(parts).ToArray()));

    private sealed class Picker(string? value) : IRestoreArtifactFilePicker { public string? PickArtifact() => value; }
    private sealed class Inspector(RestoreArtifactInspection value) : IRestoreArtifactInspector
    { public Task<RestoreArtifactInspection> InspectAsync(string? path, CancellationToken token = default) => Task.FromResult(value); }
    private sealed class Preparation(Func<RestorePreparationRequest, CancellationToken, Task<RestorePreparationResult>> run) : IRestorePreparationService
    {
        public int CallCount { get; private set; }
        public Task<RestorePreparationResult> PrepareAsync(RestorePreparationRequest request, CancellationToken token = default)
        { CallCount++; return run(request, token); }
    }
    private sealed class Launcher(params bool[] results) : IRestoreWorkerProcessLauncher
    {
        private readonly Queue<bool> _results = new(results);
        public int CallCount { get; private set; }
        public List<Guid> Operations { get; } = [];
        public Task<bool> StartAsync(RestorePreparationResult prepared, CancellationToken token)
        { CallCount++; Operations.Add(prepared.OperationId); return Task.FromResult(_results.Count > 0 && _results.Dequeue()); }
    }
}
