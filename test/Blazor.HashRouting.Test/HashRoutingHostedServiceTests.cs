using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;

namespace Blazor.HashRouting.Test
{
    public sealed class HashRoutingHostedServiceTests
    {
        [Fact]
        public void GIVEN_NullNavigationManager_WHEN_Constructing_THEN_ThrowsArgumentNullException()
        {
            Action action = () =>
            {
                _ = new HashRoutingHostedService(null!);
            };

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task GIVEN_NonBrowserRuntime_WHEN_StartAsyncCalled_THEN_ThrowsPlatformNotSupportedException()
        {
            if (OperatingSystem.IsBrowser())
            {
                return;
            }

            var target = CreateTarget();

            Func<Task> action = async () =>
            {
                await target.StartAsync(Xunit.TestContext.Current.CancellationToken);
            };

            await action.Should().ThrowAsync<PlatformNotSupportedException>();
        }

        [Fact]
        public async Task GIVEN_HostedService_WHEN_StartAsyncCoreCalled_THEN_InitializesNavigationManager()
        {
            var runtime = new RecordingJSRuntime();
            var navigationManager = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                runtime,
                NullLogger<HashNavigationManager>.Instance,
                new HashRoutingOptions());
            var target = new HashRoutingHostedService(navigationManager);

            await target.StartAsyncCore(Xunit.TestContext.Current.CancellationToken);

            runtime.ImportCalls.Should().ContainSingle();
            runtime.Module.Calls.Should().Contain(call => call.Identifier == "initialize");
        }

        [Fact]
        public async Task GIVEN_HostedService_WHEN_StopAsyncCalled_THEN_ReturnsCompletedTask()
        {
            var target = CreateTarget();

            await target.StopAsync(Xunit.TestContext.Current.CancellationToken);
        }

        private static HashRoutingHostedService CreateTarget()
        {
            var navigationManager = new HashNavigationManager(
                "http://localhost/",
                "http://localhost/",
                Mock.Of<IJSRuntime>(),
                NullLogger<HashNavigationManager>.Instance,
                new HashRoutingOptions());

            return new HashRoutingHostedService(navigationManager);
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
                identifier.Should().Be("import");

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
            }

            public List<CallRecord> Calls { get; }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                var arguments = args ?? Array.Empty<object?>();
                Calls.Add(new CallRecord(identifier, arguments));

                if (identifier == "initialize")
                {
                    return new ValueTask<TValue>((TValue)(object)(string)arguments[3]!);
                }

                return new ValueTask<TValue>(default(TValue)!);
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

            public sealed record CallRecord(string Identifier, IReadOnlyList<object?> Arguments);
        }
    }
}
