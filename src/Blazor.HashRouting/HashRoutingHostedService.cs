using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Blazor.HashRouting
{
    /// <summary>
    /// Hosted service that initializes hash routing during host startup.
    /// </summary>
    public sealed class HashRoutingHostedService : IHostedService
    {
        private readonly HashNavigationManager _navigationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="HashRoutingHostedService"/> class.
        /// </summary>
        /// <param name="navigationManager">The hash navigation manager.</param>
        public HashRoutingHostedService(HashNavigationManager navigationManager)
        {
            ArgumentNullException.ThrowIfNull(navigationManager);

            _navigationManager = navigationManager;
        }

        /// <summary>
        /// Initializes hash routing when the host starts.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ExcludeFromCodeCoverage]
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException("Blazor.HashRouting supports browser-hosted Blazor WebAssembly applications only.");
            }

            return StartAsyncCore(cancellationToken);
        }

        internal Task StartAsyncCore(CancellationToken cancellationToken)
        {
            return _navigationManager.InitializeAsync(cancellationToken);
        }

        /// <summary>
        /// Stops the hosted service.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A completed task.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
