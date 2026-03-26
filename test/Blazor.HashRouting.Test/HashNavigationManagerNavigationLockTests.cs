using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;

namespace Blazor.HashRouting.Test
{
    public sealed class HashNavigationManagerNavigationLockTests
    {
        private readonly RecordingJSRuntime _jSRuntime;
        private readonly HashNavigationManager _target;

        public HashNavigationManagerNavigationLockTests()
        {
            _jSRuntime = new RecordingJSRuntime();
            _target = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                _jSRuntime,
                NullLogger<HashNavigationManager>.Instance,
                new HashRoutingOptions());
        }

        [Fact]
        public async Task GIVEN_ManagerNotInitialized_WHEN_InitializeAsync_THEN_ImportsModuleAndInitializesJsBridge()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            _jSRuntime.ImportCalls.Should().ContainSingle();
            _jSRuntime.Module.Calls.Should().Contain(call => call.Identifier == "initialize");
        }

        [Fact]
        public async Task GIVEN_ManagerAlreadyInitialized_WHEN_InitializeAsyncCalledAgain_THEN_DoesNotReinitialize()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            _jSRuntime.ImportCalls.Should().ContainSingle();
            _jSRuntime.Module.Calls.Count(call => call.Identifier == "initialize").Should().Be(1);
        }

        [Fact]
        public async Task GIVEN_NoLocationChangingHandlers_WHEN_NotifyLocationChangingFromJs_THEN_ReturnsTrue()
        {
            var result = await _target.NotifyLocationChangingFromJs("http://localhost/settings", null, true);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task GIVEN_LocationChangingHandlerPreventsNavigation_WHEN_NotifyLocationChangingFromJs_THEN_ReturnsFalse()
        {
            using var registration = _target.RegisterLocationChangingHandler(context =>
            {
                context.PreventNavigation();
                return ValueTask.CompletedTask;
            });

            var result = await _target.NotifyLocationChangingFromJs("http://localhost/settings", null, true);

            result.Should().BeFalse();
        }

        [Fact]
        public void GIVEN_LocationChangedNotification_WHEN_NotifyLocationChangedFromJs_THEN_UpdatesUriAndRaisesEvent()
        {
            string? observedUri = null;
            _target.LocationChanged += (_, args) =>
            {
                observedUri = args.Location;
            };

            _target.NotifyLocationChangedFromJs("http://localhost/details/ABC", "state", true);

            _target.Uri.Should().Be("http://localhost/details/ABC");
            _target.HistoryEntryState.Should().Be("state");
            observedUri.Should().Be("http://localhost/details/ABC");
        }

        [Fact]
        public async Task GIVEN_DisposedManager_WHEN_NotifyLocationChangedFromJs_THEN_DoesNotUpdateState()
        {
            await _target.DisposeAsync();

            _target.NotifyLocationChangedFromJs("http://localhost/details/ABC", "state", true);

            _target.Uri.Should().Be("http://localhost/");
            _target.HistoryEntryState.Should().BeNull();
        }

        [Fact]
        public async Task GIVEN_InternalNavigation_WHEN_NavigateToCalled_THEN_DelegatesToJsNavigateTo()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            _target.NavigateTo("/settings");

            await WaitForAsync(() => _jSRuntime.Module.Calls.Count(call => call.Identifier == "navigateTo") == 1);
            var call = _jSRuntime.Module.Calls.Single(call => call.Identifier == "navigateTo");

            call.Arguments.Count.Should().Be(3);
            call.Arguments[0].Should().Be("http://localhost/settings");
            call.Arguments[1].Should().Be(false);
            call.Arguments[2].Should().BeNull();
        }

        [Fact]
        public async Task GIVEN_InternalNavigationBeforeInitialization_WHEN_NavigateToCalled_THEN_InitializesAndDelegatesToJsNavigateTo()
        {
            _target.NavigateTo("/settings");

            await WaitForAsync(() => _jSRuntime.Module.Calls.Count(call => call.Identifier == "navigateTo") == 1);
            _jSRuntime.ImportCalls.Should().ContainSingle();
            _jSRuntime.Module.Calls.Count(call => call.Identifier == "initialize").Should().Be(1);
        }

        [Fact]
        public async Task GIVEN_InternalNavigationWithHistoryOptions_WHEN_NavigateToCalled_THEN_PassesFlattenedInteropArguments()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            _target.NavigateTo("/settings", new NavigationOptions
            {
                ReplaceHistoryEntry = true,
                HistoryEntryState = "state"
            });

            await WaitForAsync(() => _jSRuntime.Module.Calls.Count(call => call.Identifier == "navigateTo") == 1);
            var call = _jSRuntime.Module.Calls.Single(call => call.Identifier == "navigateTo");

            call.Arguments.Count.Should().Be(3);
            call.Arguments[0].Should().Be("http://localhost/settings");
            call.Arguments[1].Should().Be(true);
            call.Arguments[2].Should().Be("state");
        }

        [Fact]
        public async Task GIVEN_SameUriNavigationWithoutStateOrReplace_WHEN_NavigateToCalled_THEN_DoesNotDelegateToJsNavigateTo()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            var navigateToCallCountBefore = _jSRuntime.Module.Calls.Count(call => call.Identifier == "navigateTo");

            _target.NavigateTo("/");

            await Task.Yield();

            _jSRuntime.Module.Calls.Count(call => call.Identifier == "navigateTo").Should().Be(navigateToCallCountBefore);
        }

        [Fact]
        public async Task GIVEN_ForceLoadNavigation_WHEN_NavigateToCalled_THEN_DelegatesToJsForceLoad()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            _target.NavigateTo("/settings", forceLoad: true);

            await WaitForAsync(() => _jSRuntime.Module.Calls.Count(call => call.Identifier == "forceLoad") == 1);
            var call = _jSRuntime.Module.Calls.Single(record => record.Identifier == "forceLoad");

            call.Arguments[0].Should().Be("http://localhost/#/settings");
            call.Arguments[1].Should().Be(false);
        }

        [Fact]
        public async Task GIVEN_ForceLoadNavigationWithinBasePath_WHEN_NavigateToCalled_THEN_DelegatesToCanonicalHashForceLoad()
        {
            var basePathJsRuntime = new RecordingJSRuntime();
            var manager = new HashNavigationManager(
                "http://localhost/proxy/app/",
                "http://localhost/proxy/app/",
                basePathJsRuntime,
                NullLogger<HashNavigationManager>.Instance,
                new HashRoutingOptions());

            await manager.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            manager.NavigateTo("settings", forceLoad: true);

            await WaitForAsync(() => basePathJsRuntime.Module.Calls.Count(call => call.Identifier == "forceLoad") == 1);
            var call = basePathJsRuntime.Module.Calls.Single(record => record.Identifier == "forceLoad");

            call.Arguments[0].Should().Be("http://localhost/proxy/app/#/settings");
            call.Arguments[1].Should().Be(false);
        }

        [Fact]
        public async Task GIVEN_ExternalAbsoluteNavigation_WHEN_NavigateToCalled_THEN_DelegatesToJsExternalNavigationWithoutRewritingHost()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            _target.NavigateTo("https://example.com/path?query=value");

            await WaitForAsync(() => _jSRuntime.Module.Calls.Count(call => call.Identifier == "navigateExternally") == 1);
            _jSRuntime.Module.Calls.Single(call => call.Identifier == "navigateExternally").Arguments[0].Should().Be("https://example.com/path?query=value");
        }

        [Fact]
        public async Task GIVEN_ExternalAbsoluteForceLoadNavigation_WHEN_NavigateToCalled_THEN_DelegatesToJsExternalNavigationWithOriginalUri()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            _target.NavigateTo("https://example.com/path?query=value", forceLoad: true);

            await WaitForAsync(() => _jSRuntime.Module.Calls.Count(call => call.Identifier == "navigateExternally") == 1);
            var call = _jSRuntime.Module.Calls.Single(record => record.Identifier == "navigateExternally");
            call.Arguments[0].Should().Be("https://example.com/path?query=value");
            call.Arguments[1].Should().Be(false);
        }

        [Fact]
        public async Task GIVEN_NavigationLockStateChanges_WHEN_HandlerRegisteredAndRemoved_THEN_UpdatesJsLockState()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            using var registration = _target.RegisterLocationChangingHandler(_ => ValueTask.CompletedTask);

            await WaitForAsync(() => _jSRuntime.Module.Calls.Any(call => call.Identifier == "setNavigationLockState" && call.Arguments.Count == 1 && call.Arguments[0] is true));

            registration.Dispose();

            await WaitForAsync(() => _jSRuntime.Module.Calls.Any(call => call.Identifier == "setNavigationLockState" && call.Arguments.Count == 1 && call.Arguments[0] is false));
        }

        [Fact]
        public async Task GIVEN_HandlerRegisteredBeforeInitialization_WHEN_InitializeAsyncCalled_THEN_AppliesNavigationLockStateAfterInitialization()
        {
            using var registration = _target.RegisterLocationChangingHandler(_ => ValueTask.CompletedTask);

            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            _jSRuntime.Module.Calls.Should().Contain(call => call.Identifier == "setNavigationLockState" && call.Arguments.Count == 1 && Equals(call.Arguments[0], true));
            registration.Dispose();
        }

        [Fact]
        public async Task GIVEN_ProgrammaticNavigationPrevented_WHEN_NavigateToCalled_THEN_DoesNotDelegateToJsNavigateTo()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);
            using var registration = _target.RegisterLocationChangingHandler(context =>
            {
                context.PreventNavigation();
                return ValueTask.CompletedTask;
            });

            var navigateCallCount = _jSRuntime.Module.Calls.Count(call => call.Identifier == "navigateTo");

            _target.NavigateTo("/settings");

            await Task.Yield();

            _jSRuntime.Module.Calls.Count(call => call.Identifier == "navigateTo").Should().Be(navigateCallCount);
        }

        [Fact]
        public async Task GIVEN_LocationChangingHandlerThrows_WHEN_NavigateToCalled_THEN_LogsError()
        {
            var logger = new Mock<ILogger<HashNavigationManager>>();
            var runtime = new RecordingJSRuntime();
            var target = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                runtime,
                logger.Object,
                new HashRoutingOptions());

            await target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);
            using var registration = target.RegisterLocationChangingHandler(_ =>
            {
                throw new InvalidOperationException("Boom");
            });

            target.NavigateTo("/settings");

            await WaitForAsync(() => runtime.Module.Calls.Count(call => call.Identifier == "navigateTo") == 1);
            VerifyLog(logger, LogLevel.Error, "Navigation lock handler failed for");
        }

        [Fact]
        public async Task GIVEN_OperationCanceledDuringNavigation_WHEN_NavigateToCalled_THEN_ExceptionIsSwallowed()
        {
            var runtime = new RecordingJSRuntime();
            runtime.Module.ExceptionByIdentifier["navigateTo"] = new OperationCanceledException();
            var target = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                runtime,
                NullLogger<HashNavigationManager>.Instance,
                new HashRoutingOptions());

            await target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            target.NavigateTo("/settings");

            await WaitForAsync(() => runtime.Module.Calls.Count(call => call.Identifier == "navigateTo") == 1);
            target.Uri.Should().Be("http://localhost/");
        }

        [Fact]
        public async Task GIVEN_ExceptionDuringNavigation_WHEN_NavigateToCalled_THEN_LogsError()
        {
            var logger = new Mock<ILogger<HashNavigationManager>>();
            var runtime = new RecordingJSRuntime();
            runtime.Module.ExceptionByIdentifier["navigateTo"] = new InvalidOperationException("Boom");
            var target = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                runtime,
                logger.Object,
                new HashRoutingOptions());

            await target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            target.NavigateTo("/settings");

            await WaitForAsync(() => runtime.Module.Calls.Count(call => call.Identifier == "navigateTo") == 1);
            VerifyLog(logger, LogLevel.Error, "Navigation failed for");
        }

        [Fact]
        public async Task GIVEN_ExceptionDuringApplyNavigationLockState_WHEN_HandlerRegistered_THEN_LogsDebug()
        {
            var logger = new Mock<ILogger<HashNavigationManager>>();
            var runtime = new RecordingJSRuntime();
            runtime.Module.ExceptionByIdentifier["setNavigationLockState"] = new InvalidOperationException("Boom");
            var target = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                runtime,
                logger.Object,
                new HashRoutingOptions());

            await target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);
            using var registration = target.RegisterLocationChangingHandler(_ => ValueTask.CompletedTask);

            await WaitForAsync(() => runtime.Module.Calls.Any(call => call.Identifier == "setNavigationLockState" && call.Arguments.Count == 1 && call.Arguments[0] is true));
            VerifyLog(logger, LogLevel.Debug, "Unable to apply navigation lock state");
            registration.Dispose();
        }

        [Fact]
        public async Task GIVEN_ObjectDisposedDuringApplyNavigationLockState_WHEN_HandlerRegistered_THEN_ExceptionIsSwallowed()
        {
            var runtime = new RecordingJSRuntime();
            runtime.Module.ExceptionByIdentifier["setNavigationLockState"] = new ObjectDisposedException("module");
            var target = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                runtime,
                NullLogger<HashNavigationManager>.Instance,
                new HashRoutingOptions());

            await target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);
            using var registration = target.RegisterLocationChangingHandler(_ => ValueTask.CompletedTask);

            await WaitForAsync(() => runtime.Module.Calls.Any(call => call.Identifier == "setNavigationLockState" && call.Arguments.Count == 1 && call.Arguments[0] is true));
            registration.Dispose();
        }

        [Fact]
        public async Task GIVEN_JsDisconnectedDuringApplyNavigationLockState_WHEN_HandlerRegistered_THEN_ExceptionIsSwallowed()
        {
            var runtime = new RecordingJSRuntime();
            runtime.Module.ExceptionByIdentifier["setNavigationLockState"] = new JSDisconnectedException("Disconnected");
            var target = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                runtime,
                NullLogger<HashNavigationManager>.Instance,
                new HashRoutingOptions());

            await target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);
            using var registration = target.RegisterLocationChangingHandler(_ => ValueTask.CompletedTask);

            await WaitForAsync(() => runtime.Module.Calls.Any(call => call.Identifier == "setNavigationLockState" && call.Arguments.Count == 1 && call.Arguments[0] is true));
            registration.Dispose();
        }

        [Fact]
        public async Task GIVEN_InitializedManager_WHEN_Disposed_THEN_DisposesJsResources()
        {
            await _target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            await _target.DisposeAsync();

            _jSRuntime.Module.Calls.Should().Contain(call => call.Identifier == "dispose");
            _jSRuntime.Module.DisposeCallCount.Should().Be(1);
        }

        [Fact]
        public async Task GIVEN_ModuleDisposeThrowsJsDisconnectedException_WHEN_Disposed_THEN_ExceptionIsSwallowed()
        {
            var runtime = new RecordingJSRuntime();
            runtime.Module.ExceptionByIdentifier["dispose"] = new JSDisconnectedException("Disconnected");
            runtime.Module.DisposeException = new JSDisconnectedException("Disconnected");
            var target = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                runtime,
                NullLogger<HashNavigationManager>.Instance,
                new HashRoutingOptions());

            await target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            await target.DisposeAsync();
        }

        [Fact]
        public async Task GIVEN_ModuleDisposeThrowsObjectDisposedException_WHEN_Disposed_THEN_ExceptionIsSwallowed()
        {
            var runtime = new RecordingJSRuntime();
            runtime.Module.ExceptionByIdentifier["dispose"] = new ObjectDisposedException("module");
            runtime.Module.DisposeException = new ObjectDisposedException("module");
            var target = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                runtime,
                NullLogger<HashNavigationManager>.Instance,
                new HashRoutingOptions());

            await target.InitializeAsync(Xunit.TestContext.Current.CancellationToken);

            await target.DisposeAsync();
        }

        [Fact]
        public async Task GIVEN_ManagerAlreadyDisposed_WHEN_DisposeAsyncCalledAgain_THEN_ReturnsWithoutFailure()
        {
            await _target.DisposeAsync();

            await _target.DisposeAsync();
        }

        [Fact]
        public void GIVEN_BaseUriWithoutTrailingSlash_WHEN_Constructed_THEN_SupportsBaseUriSpaceChecks()
        {
            var runtime = new RecordingJSRuntime();
            var target = new HashNavigationManager(
                "http://localhost/proxy/app",
                "http://localhost/proxy/app",
                runtime,
                NullLogger<HashNavigationManager>.Instance,
                new HashRoutingOptions());

            target.NotifyLocationChangedFromJs("http://localhost/proxy/app/settings", null, false);

            target.Uri.Should().Be("http://localhost/proxy/app/settings");
        }

        private static async Task WaitForAsync(Func<bool> predicate)
        {
            var timeoutAt = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < timeoutAt)
            {
                if (predicate())
                {
                    return;
                }

                await Task.Yield();
            }

            predicate().Should().BeTrue();
        }

        private static void VerifyLog(Mock<ILogger<HashNavigationManager>> logger, LogLevel level, string messageFragment)
        {
            logger.Verify(
                value => value.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(messageFragment, StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private sealed class RecordingJSRuntime : IJSRuntime
        {
            public RecordingJSRuntime()
            {
                Module = new RecordingJSObjectReference();
                ImportCalls = new List<IReadOnlyList<object?>>();
            }

            public RecordingJSObjectReference Module { get; }

            public List<IReadOnlyList<object?>> ImportCalls { get; }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (!string.Equals(identifier, "import", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Unexpected JS runtime identifier: {identifier}");
                }

                var arguments = args ?? Array.Empty<object?>();
                ImportCalls.Add(arguments);

                return new ValueTask<TValue>((TValue)(object)Module);
            }
        }

        private sealed class RecordingJSObjectReference : IJSObjectReference
        {
            public RecordingJSObjectReference()
            {
                Calls = new List<CallRecord>();
                ExceptionByIdentifier = new Dictionary<string, Exception>();
            }

            public List<CallRecord> Calls { get; }

            public Dictionary<string, Exception> ExceptionByIdentifier { get; }

            public int DisposeCallCount { get; private set; }

            public Exception? DisposeException { get; set; }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                var arguments = args ?? Array.Empty<object?>();
                Calls.Add(new CallRecord(identifier, arguments));
                if (ExceptionByIdentifier.TryGetValue(identifier, out var exception))
                {
                    return ValueTask.FromException<TValue>(exception);
                }

                if (string.Equals(identifier, "initialize", StringComparison.Ordinal))
                {
                    return new ValueTask<TValue>((TValue)(object)(string)arguments[3]!);
                }

                return new ValueTask<TValue>(default(TValue)!);
            }

            public ValueTask DisposeAsync()
            {
                DisposeCallCount++;
                if (DisposeException is not null)
                {
                    return ValueTask.FromException(DisposeException);
                }

                return ValueTask.CompletedTask;
            }

            public sealed record CallRecord(string Identifier, IReadOnlyList<object?> Arguments);
        }
    }
}
