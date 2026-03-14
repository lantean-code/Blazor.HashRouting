using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;

namespace Blazor.HashRouting.Test
{
    public sealed class HashRoutingHostExtensionsTests
    {
        [Fact]
        public void GIVEN_NullHost_WHEN_InitializeHashRoutingAsyncCalled_THEN_ThrowsArgumentNullException()
        {
            WebAssemblyHost? host = null;

            Action action = () =>
            {
                _ = host!.InitializeHashRoutingAsync(Xunit.TestContext.Current.CancellationToken);
            };

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task GIVEN_NullServiceProvider_WHEN_InitializeHashRoutingCoreAsyncCalled_THEN_ThrowsArgumentNullException()
        {
            IServiceProvider? serviceProvider = null;

            Func<Task> action = async () =>
            {
                await serviceProvider!.InitializeHashRoutingCoreAsync(Xunit.TestContext.Current.CancellationToken);
            };

            await action.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task GIVEN_ServiceProvider_WHEN_InitializeHashRoutingCoreAsyncCalled_THEN_InitializesNavigationManager()
        {
            var runtime = new RecordingJSRuntime();
            var serviceProvider = CreateServiceProvider(runtime);

            await serviceProvider.InitializeHashRoutingCoreAsync(Xunit.TestContext.Current.CancellationToken);

            runtime.ImportCalls.Should().ContainSingle();
            runtime.Module.Calls.Should().Contain(call => call.Identifier == "initialize");
        }

        private static IServiceProvider CreateServiceProvider(IJSRuntime? runtime = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<NavigationManager>(new TestNavigationManager("http://localhost/", "http://localhost/"));
            services.AddSingleton(runtime ?? Mock.Of<IJSRuntime>());
            services.AddSingleton<ILogger<HashNavigationManager>>(NullLogger<HashNavigationManager>.Instance);
            services.AddHashRoutingCore();

            return services.BuildServiceProvider();
        }

        private sealed class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager(string baseUri, string uri)
            {
                Initialize(baseUri, uri);
            }

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
                Uri = ToAbsoluteUri(uri).ToString();
                NotifyLocationChanged(false);
            }
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
