using POS.Wpf;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class RestoreWorkerStartupTests
{
    private static readonly string Plan = @"D:\isolated\restore-operations\2db57d25-8242-42a5-971e-fd92ad57689a\operation.json";
    private static readonly string Operation = "2db57d25-8242-42a5-971e-fd92ad57689a";
    private static readonly string Token = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
    private static readonly string[] NormalArguments = ["--safe-normal-option"];

    [Fact]
    public void Exact_worker_arguments_are_accepted()
    {
        var parsed = App.ParseRestoreWorkerArguments(Valid());
        Assert.True(parsed.IsWorkerMode);
        Assert.NotNull(parsed.Request);
        Assert.Equal(Guid.Parse(Operation), parsed.Request.OperationId);
        Assert.Equal(Plan, parsed.Request.PlanPath);
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void Invalid_worker_arguments_fail_closed(string[] arguments)
    {
        var parsed = App.ParseRestoreWorkerArguments(arguments);
        Assert.True(parsed.IsWorkerMode);
        Assert.Null(parsed.Request);
    }

    [Fact]
    public void Normal_arguments_do_not_select_private_worker_mode()
    {
        var parsed = App.ParseRestoreWorkerArguments(NormalArguments);
        Assert.False(parsed.IsWorkerMode);
        Assert.Null(parsed.Request);
    }

    [Fact]
    public void Worker_selection_precedes_host_single_instance_and_database_startup()
    {
        var source = Source("src", "POS.Wpf", "App.xaml.cs");
        var parse = source.IndexOf("ParseRestoreWorkerArguments(e.Args)", StringComparison.Ordinal);
        Assert.True(parse >= 0);
        foreach (var later in new[] { "Host.CreateApplicationBuilder", "WindowsSingleInstanceCoordinator(",
                     "_host.StartAsync()", "InitializeDatabaseAsync(" })
            Assert.True(source.IndexOf(later, parse, StringComparison.Ordinal) > parse, later);
    }

    [Fact]
    public void Worker_and_restart_launch_use_exact_executable_argumentlist_and_no_shell()
    {
        var launcher = Source("src", "POS.Wpf", "ViewModels", "RestoreWizardViewModel.cs");
        Assert.Contains("Environment.ProcessPath", launcher, StringComparison.Ordinal);
        Assert.Contains("UseShellExecute = false", launcher, StringComparison.Ordinal);
        Assert.Contains("ArgumentList.Add(\"--restore-worker\")", launcher, StringComparison.Ordinal);
        Assert.Contains("ArgumentList.Add(\"--plan\")", launcher, StringComparison.Ordinal);
        Assert.Contains("ArgumentList.Add(\"--operation\")", launcher, StringComparison.Ordinal);
        Assert.Contains("ArgumentList.Add(\"--token\")", launcher, StringComparison.Ordinal);
        Assert.DoesNotContain("powershell", launcher, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cmd.exe", launcher, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_discovery_is_exact_boundary_nonrecursive_and_before_database_initializer()
    {
        var store = Source("src", "POS.Infrastructure", "Support", "RestoreOperationStore.cs");
        Assert.Contains("SearchOption.TopDirectoryOnly", store, StringComparison.Ordinal);
        Assert.Contains("EnsureRegularDirectory(_operationsRoot)", store, StringComparison.Ordinal);
        Assert.Contains("Restore.Startup.AmbiguousOperations", store, StringComparison.Ordinal);
        var app = Source("src", "POS.Wpf", "App.xaml.cs");
        Assert.True(app.IndexOf("RecoverRestoreBeforeDatabaseStartupAsync(", StringComparison.Ordinal) <
                    app.IndexOf("InitializeDatabaseAsync(", StringComparison.Ordinal));
        Assert.Contains("RestoreOperationState.RollbackFailed", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Terminal_results_are_safe_presented_once_and_rollback_failed_is_never_acknowledged()
    {
        var app = Source("src", "POS.Wpf", "App.xaml.cs");
        Assert.Contains("Khôi phục dữ liệu thành công.", app, StringComparison.Ordinal);
        Assert.Contains("Dữ liệu ban đầu đã được phục hồi an toàn.", app, StringComparison.Ordinal);
        Assert.Contains("Không thể khôi phục dữ liệu ban đầu.", app, StringComparison.Ordinal);
        var store = Source("src", "POS.Infrastructure", "Support", "RestoreOperationStore.cs");
        Assert.Contains("result.acknowledged", store, StringComparison.Ordinal);
        Assert.Contains("is not (RestoreOperationState.Verified or RestoreOperationState.RolledBack)",
            store, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> InvalidArguments()
    {
        yield return new object[] { new[] { "--restore-worker" } };
        yield return new object[] { new[] { "--restore-worker", "--plan", Plan, "--operation", Operation, "--token", "short" } };
        yield return new object[] { new[] { "--restore-worker", "--plan", Plan, "--operation", "bad", "--token", Token } };
        yield return new object[] { new[] { "--restore-worker", "--plan", Plan, "--plan", Plan, "--token", Token } };
        yield return new object[] { new[] { "--restore-worker", "--plan", Plan, "--operation", Operation, "--unknown", Token } };
        yield return new object[] { new[] { "--restore-worker", "--operation", Operation, "--token", Token, "--plan", "relative.db" } };
    }

    private static string[] Valid() =>
        new[] { "--restore-worker", "--plan", Plan, "--operation", Operation, "--token", Token };

    private static string Source(params string[] parts) => File.ReadAllText(
        Path.Combine(new[] { Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")) }
            .Concat(parts).ToArray()));
}
