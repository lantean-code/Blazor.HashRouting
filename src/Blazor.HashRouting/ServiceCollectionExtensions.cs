using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Blazor.HashRouting;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Service registration extensions for hash routing.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        private const string NavigationManagerDependencyNotSupportedMessage = "Unable to resolve the existing NavigationManager registration because the original registration depends on NavigationManager. This dependency shape is not supported by Blazor.HashRouting.";

        /// <summary>
        /// Registers hash-based routing services for Blazor WebAssembly applications.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional options configuration callback.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <remarks>
        /// Subsequent calls are idempotent for service registrations. A later call replaces the <see cref="HashRoutingOptions"/> registration,
        /// but it does not recreate an already-constructed <see cref="HashNavigationManager"/> instance in an existing service provider.
        /// </remarks>
        [ExcludeFromCodeCoverage]
        public static IServiceCollection AddHashRouting(this IServiceCollection services, Action<HashRoutingOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            if (!OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException("Blazor.HashRouting supports browser-hosted Blazor WebAssembly applications only.");
            }

            return AddHashRoutingCore(services, configure);
        }

        internal static IServiceCollection AddHashRoutingCore(this IServiceCollection services, Action<HashRoutingOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var options = new HashRoutingOptions();
            configure?.Invoke(options);
            options.HashPrefix = HashRoutingUriHelper.NormalizeHashPrefix(options.HashPrefix);

            if (services.Any(descriptor => descriptor.ServiceType == typeof(HashRoutingRegistrationMarker)))
            {
                // Keep AddHashRouting idempotent while allowing the latest options registration to win.
                services.RemoveAll<HashRoutingOptions>();
                services.AddSingleton(options);
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, HashRoutingHostedService>());
                return services;
            }

            var navigationManagerDescriptor = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(NavigationManager));
            if (navigationManagerDescriptor is null)
            {
                throw new InvalidOperationException("Unable to locate the existing NavigationManager registration.");
            }

            services.RemoveAll<HashRoutingOptions>();
            services.AddSingleton(options);
            services.RemoveAll<HashRoutingNavigationManagerSeed>();
            services.AddSingleton(serviceProvider =>
            {
                var originalNavigationManager = CreateOriginalNavigationManager(serviceProvider, navigationManagerDescriptor);
                return new HashRoutingNavigationManagerSeed(originalNavigationManager);
            });

            services.RemoveAll<NavigationManager>();

            services.AddSingleton<NavigationManager>(serviceProvider =>
            {
                var jSRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
                var logger = serviceProvider.GetRequiredService<ILogger<HashNavigationManager>>();
                var hashOptions = serviceProvider.GetRequiredService<HashRoutingOptions>();
                var navigationManagerSeed = serviceProvider.GetRequiredService<HashRoutingNavigationManagerSeed>();
                return new HashNavigationManager(navigationManagerSeed.NavigationManager.BaseUri, navigationManagerSeed.NavigationManager.Uri, jSRuntime, logger, hashOptions);
            });

            services.RemoveAll<HashNavigationManager>();
            services.AddSingleton(serviceProvider =>
            {
                return (HashNavigationManager)serviceProvider.GetRequiredService<NavigationManager>();
            });
            services.AddSingleton<HashRoutingRegistrationMarker>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, HashRoutingHostedService>());

            return services;
        }

        private static NavigationManager CreateOriginalNavigationManager(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
        {
            var nonNavigationManagerProvider = new NavigationManagerBlockingServiceProvider(serviceProvider);

            if (descriptor.ImplementationInstance is NavigationManager implementationInstance)
            {
                return implementationInstance;
            }

            if (descriptor.ImplementationFactory is not null)
            {
                var implementation = descriptor.ImplementationFactory(nonNavigationManagerProvider);
                return CastNavigationManager(implementation);
            }

            var createdImplementation = ActivatorUtilities.GetServiceOrCreateInstance(nonNavigationManagerProvider, descriptor.ImplementationType!);
            return CastNavigationManager(createdImplementation);
        }

        [return: NotNull]
        private static NavigationManager CastNavigationManager(object? implementation)
        {
            if (implementation is NavigationManager navigationManager)
            {
                return navigationManager;
            }

            throw new InvalidOperationException("Unable to resolve the existing NavigationManager registration.");
        }

        private sealed record HashRoutingNavigationManagerSeed(NavigationManager NavigationManager);

        private sealed class NavigationManagerBlockingServiceProvider : IServiceProvider
        {
            private readonly IServiceProvider _innerServiceProvider;

            public NavigationManagerBlockingServiceProvider(IServiceProvider innerServiceProvider)
            {
                ArgumentNullException.ThrowIfNull(innerServiceProvider);

                _innerServiceProvider = innerServiceProvider;
            }

            public object? GetService(Type serviceType)
            {
                ArgumentNullException.ThrowIfNull(serviceType);

                if (serviceType == typeof(NavigationManager))
                {
                    throw new InvalidOperationException(NavigationManagerDependencyNotSupportedMessage);
                }

                return _innerServiceProvider.GetService(serviceType);
            }
        }

        private sealed class HashRoutingRegistrationMarker
        {
        }
    }
}
