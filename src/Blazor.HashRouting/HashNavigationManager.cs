using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazor.HashRouting
{
    /// <summary>
    /// Navigation manager implementation that maps Blazor routes to hash-fragment URLs.
    /// </summary>
    public sealed class HashNavigationManager : NavigationManager, IAsyncDisposable
    {
        private const string JSImportIdentifier = "import";
        private const string JSImportPath = "./_content/Blazor.HashRouting/hash-routing.module.js";

        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private readonly IJSRuntime _jSRuntime;
        private readonly ILogger<HashNavigationManager> _logger;
        private readonly HashRoutingOptions _options;
        private readonly string _baseUriWithoutTrailingSlash;
        private readonly string _hashPrefix;

        private IJSObjectReference? _module;
        private DotNetObjectReference<HashNavigationManager>? _dotNetObjectReference;
        private bool _isInitialized;
        private bool _disposed;
        private bool _navigationLockEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="HashNavigationManager"/> class.
        /// </summary>
        /// <param name="baseUri">The app base URI.</param>
        /// <param name="uri">The current location URI.</param>
        /// <param name="jSRuntime">The JavaScript runtime.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="options">Hash routing options.</param>
        public HashNavigationManager(string baseUri, string uri, IJSRuntime jSRuntime, ILogger<HashNavigationManager> logger, HashRoutingOptions options)
        {
            ArgumentNullException.ThrowIfNull(baseUri);
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(jSRuntime);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(options);

            _jSRuntime = jSRuntime;
            _logger = logger;
            _options = options;
            _baseUriWithoutTrailingSlash = baseUri.EndsWith('/')
                ? baseUri[..^1]
                : baseUri;
            _hashPrefix = HashRoutingUriHelper.NormalizeHashPrefix(options.HashPrefix);

            var initialPathUri = HashRoutingUriHelper.ToPathAbsoluteUri(uri, baseUri, _hashPrefix);
            Initialize(baseUri, initialPathUri);
        }

        /// <summary>
        /// Initializes JavaScript routing hooks.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _initializationLock.WaitAsync(cancellationToken);
            try
            {
                if (_isInitialized)
                {
                    return;
                }

                _dotNetObjectReference = DotNetObjectReference.Create(this);
                var module = await GetModule(cancellationToken);

                var currentPathUri = await module.InvokeAsync<string>(
                    "initialize",
                    cancellationToken,
                    _dotNetObjectReference,
                    new
                    {
                        _options.CanonicalizeToHash,
                        HashPrefix = _hashPrefix,
                        _options.InterceptInternalLinks
                    },
                    BaseUri,
                    Uri);

                if (!string.IsNullOrWhiteSpace(currentPathUri))
                {
                    Uri = currentPathUri;
                }

                _isInitialized = true;

                if (_navigationLockEnabled)
                {
                    await module.InvokeVoidAsync("setNavigationLockState", cancellationToken, true);
                }
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        /// <summary>
        /// Receives navigation lock checks from JavaScript-initiated navigations.
        /// </summary>
        /// <param name="targetLocation">The target path-style absolute URI.</param>
        /// <param name="historyEntryState">The history entry state.</param>
        /// <param name="isNavigationIntercepted">Whether the navigation originated from an intercepted link.</param>
        /// <returns><see langword="true"/> when navigation can continue; otherwise, <see langword="false"/>.</returns>
        [JSInvokable]
        public async Task<bool> NotifyLocationChangingFromJs(string targetLocation, string? historyEntryState, bool isNavigationIntercepted)
        {
            return await NotifyLocationChangingAsync(targetLocation, historyEntryState, isNavigationIntercepted);
        }

        /// <summary>
        /// Receives location updates from JavaScript after navigation has completed.
        /// </summary>
        /// <param name="location">The path-style absolute URI.</param>
        /// <param name="historyEntryState">The history entry state.</param>
        /// <param name="isNavigationIntercepted">Whether the navigation originated from an intercepted link.</param>
        [JSInvokable]
        public void NotifyLocationChangedFromJs(string location, string? historyEntryState, bool isNavigationIntercepted)
        {
            if (_disposed)
            {
                return;
            }

            Uri = location;
            HistoryEntryState = historyEntryState;
            NotifyLocationChanged(isNavigationIntercepted);
        }

        /// <inheritdoc />
        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            _ = NavigateToCoreAsync(uri, options);
        }

        /// <inheritdoc />
        protected override void HandleLocationChangingHandlerException(Exception ex, LocationChangingContext context)
        {
            _logger.LogError(ex, "Navigation lock handler failed for {TargetLocation}.", context.TargetLocation);
        }

        /// <inheritdoc />
        protected override void SetNavigationLockState(bool value)
        {
            _navigationLockEnabled = value;

            if (!_isInitialized)
            {
                return;
            }

            _ = ApplyNavigationLockStateAsync(value);
        }

        /// <summary>
        /// Disposes JS module resources.
        /// </summary>
        /// <returns>A task representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_module is not null)
            {
                try
                {
                    await _module.InvokeVoidAsync("dispose");
                }
                catch (JSDisconnectedException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

                try
                {
                    await _module.DisposeAsync();
                }
                catch (JSDisconnectedException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            _dotNetObjectReference?.Dispose();
            _initializationLock.Dispose();
            GC.SuppressFinalize(this);
        }

        private async Task NavigateToCoreAsync(string uri, NavigationOptions options)
        {
            try
            {
                if (!_isInitialized)
                {
                    await InitializeAsync();
                }

                var targetAbsoluteUri = ToAbsoluteUri(uri).AbsoluteUri;
                var isInBaseUriSpace = IsInBaseUriSpace(targetAbsoluteUri);
                var pathTargetAbsoluteUri = isInBaseUriSpace
                    ? HashRoutingUriHelper.ToPathAbsoluteUri(targetAbsoluteUri, BaseUri, _hashPrefix)
                    : targetAbsoluteUri;
                var hashTargetAbsoluteUri = isInBaseUriSpace
                    ? HashRoutingUriHelper.ToHashAbsoluteUri(pathTargetAbsoluteUri, BaseUri, _hashPrefix)
                    : targetAbsoluteUri;
                var isNoOpNavigation = !options.ForceLoad
                    && !options.ReplaceHistoryEntry
                    && string.Equals(pathTargetAbsoluteUri, Uri, StringComparison.Ordinal)
                    && string.Equals(options.HistoryEntryState, HistoryEntryState, StringComparison.Ordinal);

                if (isNoOpNavigation)
                {
                    return;
                }

                if (options.ForceLoad || !isInBaseUriSpace)
                {
                    var externalNavigationModule = await GetModule();

                    if (options.ForceLoad && isInBaseUriSpace)
                    {
                        await externalNavigationModule.InvokeVoidAsync("forceLoad", hashTargetAbsoluteUri, options.ReplaceHistoryEntry);
                        return;
                    }

                    await externalNavigationModule.InvokeVoidAsync("navigateExternally", targetAbsoluteUri, options.ReplaceHistoryEntry);
                    return;
                }

                var shouldContinue = await NotifyLocationChangingAsync(pathTargetAbsoluteUri, options.HistoryEntryState, false);
                if (!shouldContinue)
                {
                    return;
                }

                var module = await GetModule();

                await module.InvokeVoidAsync(
                    "navigateTo",
                    pathTargetAbsoluteUri,
                    options.ReplaceHistoryEntry,
                    options.HistoryEntryState);

                Uri = pathTargetAbsoluteUri;
                HistoryEntryState = options.HistoryEntryState;
                NotifyLocationChanged(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Navigation failed for {Uri}.", uri);
            }
        }

        private async Task ApplyNavigationLockStateAsync(bool value)
        {
            try
            {
                var module = await GetModule();
                await module.InvokeVoidAsync("setNavigationLockState", value);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (JSDisconnectedException)
            {
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Unable to apply navigation lock state {Value} in JavaScript.", value);
            }
        }

        private bool IsInBaseUriSpace(string absoluteUri)
        {
            return absoluteUri.StartsWith(BaseUri, StringComparison.OrdinalIgnoreCase)
                || string.Equals(absoluteUri, _baseUriWithoutTrailingSlash, StringComparison.OrdinalIgnoreCase);
        }

        private ValueTask<IJSObjectReference> GetModule(CancellationToken cancellationToken = default)
        {
            if (_module is not null)
            {
                return ValueTask.FromResult(_module);
            }

            return LoadModuleAsync(cancellationToken);
        }

        private async ValueTask<IJSObjectReference> LoadModuleAsync(CancellationToken cancellationToken)
        {
            _module = await _jSRuntime.InvokeAsync<IJSObjectReference>(JSImportIdentifier, cancellationToken, JSImportPath);
            return _module;
        }
    }
}
