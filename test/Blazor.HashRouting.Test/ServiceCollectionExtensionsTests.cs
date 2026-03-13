using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;

namespace Blazor.HashRouting.Test
{
    public sealed class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void GIVEN_NullServiceCollection_WHEN_AddHashRouting_THEN_ThrowsArgumentNullException()
        {
            IServiceCollection? services = null;

            Action action = () =>
            {
                _ = services!.AddHashRouting();
            };

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GIVEN_ServiceCollectionWithoutNavigationManager_WHEN_AddHashRouting_THEN_ThrowsInvalidOperationException()
        {
            var services = new ServiceCollection();

            Action action = () =>
            {
                services.AddHashRoutingCore();
            };

            action.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void GIVEN_ServiceCollection_WHEN_AddHashRouting_THEN_RegistersHashNavigationManagerAsNavigationManager()
        {
            var services = CreateServiceCollection();
            AddHashRoutingForTest(services, options =>
            {
                options.HashPrefix = "route";
            });

            var serviceProvider = services.BuildServiceProvider();
            var navigationManager = serviceProvider.GetRequiredService<NavigationManager>();
            var hashNavigationManager = serviceProvider.GetRequiredService<HashNavigationManager>();
            var options = serviceProvider.GetRequiredService<HashRoutingOptions>();

            navigationManager.Should().BeOfType<HashNavigationManager>();
            hashNavigationManager.Should().BeSameAs(navigationManager);
            options.HashPrefix.Should().Be("/route/");
        }

        [Fact]
        public void GIVEN_ServiceCollection_WHEN_AddHashRouting_THEN_RegistersHashRoutingHostedService()
        {
            var services = CreateServiceCollection();
            AddHashRoutingForTest(services);

            var serviceProvider = services.BuildServiceProvider();
            var hostedServices = serviceProvider.GetServices<IHostedService>();

            hostedServices.Should().ContainSingle(service => service.GetType() == typeof(HashRoutingHostedService));
        }

        [Fact]
        public void GIVEN_FactoryNavigationManagerRegistration_WHEN_AddHashRouting_THEN_RegistersHashNavigationManager()
        {
            var services = CreateServiceCollectionWithFactoryNavigationManager();
            AddHashRoutingForTest(services);

            var serviceProvider = services.BuildServiceProvider();
            var navigationManager = serviceProvider.GetRequiredService<NavigationManager>();
            var hashNavigationManager = serviceProvider.GetRequiredService<HashNavigationManager>();

            navigationManager.Should().BeOfType<HashNavigationManager>();
            hashNavigationManager.Should().BeSameAs(navigationManager);
        }

        [Fact]
        public void GIVEN_TypeNavigationManagerRegistration_WHEN_AddHashRouting_THEN_RegistersHashNavigationManager()
        {
            var services = CreateServiceCollectionWithTypeNavigationManager();
            AddHashRoutingForTest(services);

            var serviceProvider = services.BuildServiceProvider();
            var navigationManager = serviceProvider.GetRequiredService<NavigationManager>();
            var hashNavigationManager = serviceProvider.GetRequiredService<HashNavigationManager>();

            navigationManager.Should().BeOfType<HashNavigationManager>();
            hashNavigationManager.Should().BeSameAs(navigationManager);
        }

        [Fact]
        public void GIVEN_ServiceCollection_WHEN_AddHashRoutingCalledTwice_THEN_RegistrationRemainsIdempotent()
        {
            var services = CreateServiceCollection();
            AddHashRoutingForTest(services, options =>
            {
                options.HashPrefix = "old-prefix";
            });

            AddHashRoutingForTest(services, options =>
            {
                options.HashPrefix = "new-prefix";
            });

            var serviceProvider = services.BuildServiceProvider();
            var navigationManager = serviceProvider.GetRequiredService<NavigationManager>();
            var options = serviceProvider.GetRequiredService<HashRoutingOptions>();
            var hostedServices = serviceProvider.GetServices<IHostedService>();

            navigationManager.Should().BeOfType<HashNavigationManager>();
            options.HashPrefix.Should().Be("/new-prefix/");
            hostedServices.Where(service => service.GetType() == typeof(HashRoutingHostedService)).Should().ContainSingle();
        }

        [Fact]
        public void GIVEN_NavigationManagerFactoryDependsOnNavigationManager_WHEN_AddHashRouting_THEN_ThrowsInvalidOperationExceptionOnResolution()
        {
            var services = CreateServiceCollectionWithNavigationManagerDependentFactory();
            AddHashRoutingForTest(services);

            var serviceProvider = services.BuildServiceProvider();
            Action action = () =>
            {
                _ = serviceProvider.GetRequiredService<NavigationManager>();
            };

            action.Should().Throw<InvalidOperationException>().WithMessage("*depends on NavigationManager*");
        }

        [Fact]
        public void GIVEN_NavigationManagerTypeDependsOnNavigationManager_WHEN_AddHashRouting_THEN_ThrowsInvalidOperationExceptionOnResolution()
        {
            var services = CreateServiceCollectionWithNavigationManagerDependentType();
            AddHashRoutingForTest(services);

            var serviceProvider = services.BuildServiceProvider();
            Action action = () =>
            {
                _ = serviceProvider.GetRequiredService<NavigationManager>();
            };

            action.Should().Throw<InvalidOperationException>().WithMessage("*depends on NavigationManager*");
        }

        [Fact]
        public void GIVEN_NavigationManagerFactoryReturnsNonNavigationManager_WHEN_AddHashRouting_THEN_ThrowsInvalidOperationExceptionOnResolution()
        {
            var services = CreateServiceCollectionWithInvalidNavigationManagerFactory();
            AddHashRoutingForTest(services);

            var serviceProvider = services.BuildServiceProvider();
            Action action = () =>
            {
                _ = serviceProvider.GetRequiredService<NavigationManager>();
            };

            action.Should().Throw<InvalidOperationException>().WithMessage("*existing NavigationManager registration*");
        }

        [Fact]
        public void GIVEN_NonBrowserRuntime_WHEN_AddHashRoutingCalled_THEN_ThrowsPlatformNotSupportedException()
        {
            if (OperatingSystem.IsBrowser())
            {
                return;
            }

            var services = CreateServiceCollection();

            Action action = () =>
            {
                services.AddHashRouting();
            };

            action.Should().Throw<PlatformNotSupportedException>();
        }

        [Fact]
        public void GIVEN_NullServiceCollection_WHEN_AddHashRoutingCore_THEN_ThrowsArgumentNullException()
        {
            IServiceCollection? services = null;

            Action action = () =>
            {
                _ = services!.AddHashRoutingCore();
            };

            action.Should().Throw<ArgumentNullException>();
        }

        private static IServiceCollection CreateServiceCollection()
        {
            var services = new ServiceCollection();
            var existingNavigationManager = new TestNavigationManager("http://localhost/", "http://localhost/");
            var jSRuntime = Mock.Of<IJSRuntime>();

            services.AddSingleton<NavigationManager>(existingNavigationManager);
            services.AddSingleton(jSRuntime);
            services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<HashNavigationManager>), NullLogger<HashNavigationManager>.Instance);

            return services;
        }

        private static IServiceCollection CreateServiceCollectionWithFactoryNavigationManager()
        {
            var services = new ServiceCollection();
            var jSRuntime = Mock.Of<IJSRuntime>();

            services.AddSingleton<NavigationManager>(_ => new TestNavigationManager("http://localhost/", "http://localhost/"));
            services.AddSingleton(jSRuntime);
            services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<HashNavigationManager>), NullLogger<HashNavigationManager>.Instance);

            return services;
        }

        private static IServiceCollection CreateServiceCollectionWithTypeNavigationManager()
        {
            var services = new ServiceCollection();
            var jSRuntime = Mock.Of<IJSRuntime>();

            services.AddSingleton<NavigationManager, TestTypeNavigationManager>();
            services.AddSingleton(jSRuntime);
            services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<HashNavigationManager>), NullLogger<HashNavigationManager>.Instance);

            return services;
        }

        private static IServiceCollection CreateServiceCollectionWithNavigationManagerDependentFactory()
        {
            var services = new ServiceCollection();
            var jSRuntime = Mock.Of<IJSRuntime>();

            services.AddSingleton<NavigationManager>(serviceProvider =>
            {
                return serviceProvider.GetRequiredService<NavigationManager>();
            });
            services.AddSingleton(jSRuntime);
            services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<HashNavigationManager>), NullLogger<HashNavigationManager>.Instance);

            return services;
        }

        private static IServiceCollection CreateServiceCollectionWithNavigationManagerDependentType()
        {
            var services = new ServiceCollection();
            var jSRuntime = Mock.Of<IJSRuntime>();

            services.AddSingleton<NavigationManager, NavigationManagerDependentTypeNavigationManager>();
            services.AddSingleton(jSRuntime);
            services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<HashNavigationManager>), NullLogger<HashNavigationManager>.Instance);

            return services;
        }

        private static IServiceCollection CreateServiceCollectionWithInvalidNavigationManagerFactory()
        {
            var services = new ServiceCollection();
            var jSRuntime = Mock.Of<IJSRuntime>();

            ((ICollection<ServiceDescriptor>)services).Add(new ServiceDescriptor(typeof(NavigationManager), _ => new object(), ServiceLifetime.Singleton));
            services.AddSingleton(jSRuntime);
            services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<HashNavigationManager>), NullLogger<HashNavigationManager>.Instance);

            return services;
        }

        private static IServiceCollection AddHashRoutingForTest(IServiceCollection services, Action<HashRoutingOptions>? configure = null)
        {
            if (OperatingSystem.IsBrowser())
            {
                return services.AddHashRouting(configure);
            }

            return services.AddHashRoutingCore(configure);
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

        private sealed class TestTypeNavigationManager : NavigationManager
        {
            public TestTypeNavigationManager()
            {
                Initialize("http://localhost/", "http://localhost/");
            }

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
                Uri = ToAbsoluteUri(uri).ToString();
                NotifyLocationChanged(false);
            }
        }

        private sealed class NavigationManagerDependentTypeNavigationManager : NavigationManager
        {
            public NavigationManagerDependentTypeNavigationManager(NavigationManager navigationManager)
            {
                _ = navigationManager;
            }

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
                throw new NotSupportedException();
            }
        }
    }
}
