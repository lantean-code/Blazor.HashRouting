using Blazor.HashRouting;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.AspNetCore.Components.WebAssembly.Hosting
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
        [ExcludeFromCodeCoverage]
        public static Task InitializeHashRoutingAsync(this WebAssemblyHost host, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(host);

            if (!OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException("Blazor.HashRouting supports browser-hosted Blazor WebAssembly applications only.");
            }

            return InitializeHashRoutingCoreAsync(host.Services, cancellationToken);
        }

        internal static Task InitializeHashRoutingCoreAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var navigationManager = serviceProvider.GetRequiredService<HashNavigationManager>();

            return navigationManager.InitializeAsync(cancellationToken);
        }
    }
}
