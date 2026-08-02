using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Platform;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SingleInstanceInfrastructureTests
{
    [Fact]
    public void Equivalent_windows_paths_create_the_same_identity()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "POS-R21B-Identity",
                "Store");

        var firstPath =
            Path.Combine(
                root,
                "pos-enterprise.db");

        var equivalentPath =
            Path.Combine(
                root,
                "nested",
                "..",
                "POS-ENTERPRISE.DB")
            .Replace(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
            Path.AltDirectorySeparatorChar;

        var firstIdentity =
            DatabaseIdentity.FromResolvedPath(
                firstPath);

        var equivalentIdentity =
            DatabaseIdentity.FromResolvedPath(
                equivalentPath);

        Assert.Equal(
            firstIdentity.CanonicalDatabasePath,
            equivalentIdentity.CanonicalDatabasePath);

        Assert.Equal(
            firstIdentity.Hash,
            equivalentIdentity.Hash);

        Assert.Equal(
            firstIdentity.MutexName,
            equivalentIdentity.MutexName);

        Assert.Equal(
            firstIdentity.PipeName,
            equivalentIdentity.PipeName);
    }

    [Fact]
    public void Different_database_paths_create_different_identities()
    {
        var firstIdentity =
            CreateIdentity();

        var secondIdentity =
            DatabaseIdentity.FromResolvedPath(
                Path.Combine(
                    Path.GetTempPath(),
                    "POS-R21B-Identity",
                    $"other-{Guid.NewGuid():N}.db"));

        Assert.NotEqual(
            firstIdentity.Hash,
            secondIdentity.Hash);

        Assert.NotEqual(
            firstIdentity.MutexName,
            secondIdentity.MutexName);

        Assert.NotEqual(
            firstIdentity.PipeName,
            secondIdentity.PipeName);
    }

    [Fact]
    public void Object_names_use_hashes_and_never_expose_the_raw_path()
    {
        var identity =
            CreateIdentity();

        Assert.StartsWith(
            "Local\\POS.Enterprise.SingleInstance.",
            identity.MutexName,
            StringComparison.Ordinal);

        Assert.StartsWith(
            "POS.Enterprise.Activation.",
            identity.PipeName,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Local\\",
            identity.PipeName,
            StringComparison.Ordinal);

        Assert.Matches(
            "^[0-9A-F]{64}$",
            identity.Hash);

        Assert.DoesNotContain(
            identity.CanonicalDatabasePath,
            identity.MutexName,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            identity.CanonicalDatabasePath,
            identity.PipeName,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Activation_pipe_acl_allows_only_the_current_user_sid()
    {
        var currentUser =
            WindowsIdentity.GetCurrent().User;

        Assert.NotNull(currentUser);

        var security =
            WindowsSingleInstanceCoordinator
                .CreateActivationPipeSecurity(
                    currentUser!);

        var rules =
            security
                .GetAccessRules(
                    includeExplicit:
                        true,
                    includeInherited:
                        true,
                    targetType:
                        typeof(SecurityIdentifier))
                .Cast<PipeAccessRule>()
                .ToArray();

        var allowRules =
            rules
                .Where(rule =>
                    rule.AccessControlType ==
                    AccessControlType.Allow)
                .ToArray();

        Assert.True(
            security.AreAccessRulesProtected);

        Assert.Equal(
            currentUser,
            security.GetOwner(
                typeof(SecurityIdentifier)));

        Assert.NotEmpty(allowRules);
        Assert.All(
            allowRules,
            rule =>
                Assert.Equal(
                    currentUser,
                    rule.IdentityReference));

        Assert.DoesNotContain(
            allowRules,
            rule =>
                rule.IdentityReference ==
                new SecurityIdentifier(
                    WellKnownSidType.AuthenticatedUserSid,
                    null));

        Assert.DoesNotContain(
            allowRules,
            rule =>
                rule.IdentityReference ==
                new SecurityIdentifier(
                    WellKnownSidType.WorldSid,
                    null));

        Assert.DoesNotContain(
            allowRules,
            rule =>
                rule.IdentityReference ==
                new SecurityIdentifier(
                    WellKnownSidType.BuiltinUsersSid,
                    null));
    }

    [Fact]
    public async Task Same_identity_contender_cannot_acquire_the_mutex()
    {
        var identity =
            CreateIdentity();

        using var releaseOwner =
            new ManualResetEventSlim(false);

        var ownerReady =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var ownerThread =
            new Thread(
                () =>
                {
                    try
                    {
                        using var owner =
                            new WindowsSingleInstanceCoordinator(
                                identity);

                        ownerReady.SetResult(
                            owner.TryAcquire());

                        releaseOwner.Wait();
                    }
                    catch (Exception exception)
                    {
                        ownerReady.SetException(
                            exception);
                    }
                });

        ownerThread.Start();

        try
        {
            Assert.True(
                await ownerReady.Task.WaitAsync(
                    TimeSpan.FromSeconds(2)));

            using var contender =
                new WindowsSingleInstanceCoordinator(
                    identity);

            Assert.False(
                contender.TryAcquire());
        }
        finally
        {
            releaseOwner.Set();
            await Task.Run(ownerThread.Join);
        }
    }

    [Fact]
    public void Different_identity_can_acquire_independently()
    {
        using var first =
            new WindowsSingleInstanceCoordinator(
                CreateIdentity());

        using var second =
            new WindowsSingleInstanceCoordinator(
                CreateIdentity());

        Assert.True(
            first.TryAcquire());

        Assert.True(
            second.TryAcquire());
    }

    [Fact]
    public void Normal_dispose_releases_ownership_for_the_next_owner()
    {
        var identity =
            CreateIdentity();

        var first =
            new WindowsSingleInstanceCoordinator(
                identity);

        Assert.True(
            first.TryAcquire());

        first.Dispose();

        using var second =
            new WindowsSingleInstanceCoordinator(
                identity);

        Assert.True(
            second.TryAcquire());
    }

    [Fact]
    public void Abandoned_mutex_is_taken_over_as_successful_ownership()
    {
        var identity =
            CreateIdentity();

        using var ready =
            new ManualResetEventSlim(false);

        Exception? threadException =
            null;

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        using var mutex =
                            new Mutex(
                                initiallyOwned: false,
                                name: identity.MutexName,
                                createdNew: out _);

                        mutex.WaitOne();
                        ready.Set();
                    }
                    catch (Exception exception)
                    {
                        threadException =
                            exception;
                        ready.Set();
                    }
                });

        thread.Start();

        Assert.True(
            ready.Wait(TimeSpan.FromSeconds(2)));

        thread.Join();

        Assert.Null(threadException);

        using var takeover =
            new WindowsSingleInstanceCoordinator(
                identity);

        Assert.True(
            takeover.TryAcquire());
    }

    [Fact]
    public async Task Activation_request_is_received_by_the_owner_listener()
    {
        var identity =
            CreateIdentity();

        var received =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var listenerError =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        using var releaseOwner =
            new ManualResetEventSlim(false);

        var ownerReady =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var ownerThread =
            StartOwnerListenerThread(
                identity,
                ownerReady,
                received,
                listenerError,
                releaseOwner);

        ownerThread.Start();

        try
        {
            Assert.True(
                await ownerReady.Task.WaitAsync(
                    TimeSpan.FromSeconds(2)));

            using var requestCancellation =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(5));

            var requested =
                await WindowsSingleInstanceCoordinator
                    .RequestActivationAsync(
                        identity,
                        requestCancellation.Token);

            Assert.False(
                listenerError.Task.IsCompleted,
                listenerError.Task.IsCompleted
                    ? listenerError.Task.Exception?.ToString()
                      ?? "Listener fault"
                    : string.Empty);

            Assert.True(requested);
            Assert.True(
                await received.Task.WaitAsync(
                    TimeSpan.FromSeconds(2)));
        }
        finally
        {
            releaseOwner.Set();
            await Task.Run(ownerThread.Join);
        }
    }

    [Fact]
    public async Task Malformed_payload_is_ignored_and_listener_continues()
    {
        var identity =
            CreateIdentity();

        var received =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var listenerError =
            new TaskCompletionSource<Exception>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        using var releaseOwner =
            new ManualResetEventSlim(false);

        var ownerReady =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var ownerThread =
            StartOwnerListenerThread(
                identity,
                ownerReady,
                received,
                listenerError,
                releaseOwner);

        ownerThread.Start();

        try
        {
            Assert.True(
                await ownerReady.Task.WaitAsync(
                    TimeSpan.FromSeconds(2)));

            using (var malformedClient =
                   new NamedPipeClientStream(
                       ".",
                   identity.PipeName,
                   PipeDirection.Out,
                   PipeOptions.Asynchronous))
            {
                await malformedClient
                    .ConnectAsync(2000);

                await malformedClient
                    .WriteAsync(
                        Encoding.UTF8.GetBytes(
                            "INVALID!"));

                await malformedClient
                    .FlushAsync();
            }

            Assert.False(
                received.Task.IsCompleted);

            Assert.False(
                listenerError.Task.IsCompleted,
                listenerError.Task.IsCompleted
                    ? listenerError.Task.Exception?.ToString()
                      ?? "Listener fault"
                    : string.Empty);

            using var requestCancellation =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(5));

            Assert.True(
                await WindowsSingleInstanceCoordinator
                    .RequestActivationAsync(
                        identity,
                        requestCancellation.Token));

            Assert.True(
                await received.Task.WaitAsync(
                    TimeSpan.FromSeconds(2)));
        }
        finally
        {
            releaseOwner.Set();
            await Task.Run(ownerThread.Join);
        }
    }

    [Fact]
    public async Task Listener_dispose_stops_accepting_activation_requests()
    {
        var identity =
            CreateIdentity();

        using var stopOwner =
            new ManualResetEventSlim(false);

        var ownerReady =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var ownerStopped =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var ownerThread =
            new Thread(
                () =>
                {
                    try
                    {
                        using var owner =
                            new WindowsSingleInstanceCoordinator(
                                identity);

                        Assert.True(
                            owner.TryAcquire());

                        owner.StartActivationListener(
                            () => Task.CompletedTask);

                        ownerReady.SetResult(true);
                        stopOwner.Wait();

                        owner.StopActivationListenerAsync()
                            .GetAwaiter()
                            .GetResult();

                        ownerStopped.SetResult(true);
                    }
                    catch (Exception exception)
                    {
                        ownerReady.TrySetException(
                            exception);
                        ownerStopped.TrySetException(
                            exception);
                    }
                });

        ownerThread.Start();

        Assert.True(
            await ownerReady.Task.WaitAsync(
                TimeSpan.FromSeconds(2)));

        stopOwner.Set();

        try
        {
            Assert.True(
                await ownerStopped.Task.WaitAsync(
                    TimeSpan.FromSeconds(2)));

            using var cancellation =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(3));

            Assert.False(
                await WindowsSingleInstanceCoordinator
                    .RequestActivationAsync(
                        identity,
                        cancellation.Token));
        }
        finally
        {
            await Task.Run(ownerThread.Join);
        }
    }

    private static Thread StartOwnerListenerThread(
        DatabaseIdentity identity,
        TaskCompletionSource<bool> ownerReady,
        TaskCompletionSource<bool> received,
        TaskCompletionSource<Exception> listenerError,
        ManualResetEventSlim releaseOwner)
    {
        return new Thread(
            () =>
            {
                try
                {
                    using var owner =
                        new WindowsSingleInstanceCoordinator(
                            identity,
                            exception =>
                                listenerError.TrySetResult(
                                    exception));

                    Assert.True(
                        owner.TryAcquire());

                    owner.StartActivationListener(
                        () =>
                        {
                            received.TrySetResult(true);

                            return Task.CompletedTask;
                        });

                    /*
                     * StartActivationListener creates the first pipe server
                     * before returning. Signal readiness only after that
                     * contract has been established so the client never
                     * races the test harness.
                     */
                    ownerReady.SetResult(true);

                    releaseOwner.Wait();
                    owner.StopActivationListenerAsync()
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception exception)
                {
                    ownerReady.TrySetException(
                        exception);
                }
            });
    }

    private static DatabaseIdentity CreateIdentity()
    {
        return DatabaseIdentity.FromResolvedPath(
            Path.Combine(
                Path.GetTempPath(),
                "POS-R21B-Tests",
                $"database-{Guid.NewGuid():N}.db"));
    }
}
