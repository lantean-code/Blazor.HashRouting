using Blazor.HashRouting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Host initialization extensions for hash routing.
    /// </summary>
    public static class HashRoutingHostExtensions
    {
        /// <summary>
        /// Initializes hash routing after the host has been built.
        /// </summary>
        /// <param name="host">The application host.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous initialization operation.</returns>
        public static Task InitializeHashRoutingAsync(this IHost host, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(host);

            if (!OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException("Blazor.HashRouting supports browser-hosted Blazor WebAssembly applications only.");
            }

            return InitializeHashRoutingCoreAsync(host, cancellationToken);
        }

        internal static Task InitializeHashRoutingCoreAsync(this IHost host, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(host);

            var navigationManager = host.Services.GetRequiredService<HashNavigationManager>();

            return navigationManager.InitializeAsync(cancellationToken);
        }
    }
}
