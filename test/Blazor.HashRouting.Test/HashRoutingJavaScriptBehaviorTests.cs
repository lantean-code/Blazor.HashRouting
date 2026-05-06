using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Jint;
using Jint.Native;

namespace Blazor.HashRouting.Test
{
    public sealed class HashRoutingJavaScriptBehaviorTests
    {
        private readonly HashRoutingJavaScriptTestHost _target;

        public HashRoutingJavaScriptBehaviorTests()
        {
            _target = new HashRoutingJavaScriptTestHost();
        }

        [Fact]
        public void GIVEN_PathLocationUnderBasePath_WHEN_InitializeCalled_THEN_LocationIsRewrittenToCanonicalHashRelativeToBasePath()
        {
            var result = _target.Initialize(
                "http://localhost/proxy/app/",
                "http://localhost/proxy/app/details/ABC?tab=Peers",
                "http://localhost/proxy/app/details/ABC?tab=Peers");

            result.Should().Be("http://localhost/proxy/app/details/ABC?tab=Peers");
            _target.GetLocationHref().Should().Be("http://localhost/proxy/app/#/details/ABC?tab=Peers");
        }

        [Fact]
        public void GIVEN_CustomHashPrefixLocation_WHEN_InitializeCalled_THEN_ReturnsPathAbsoluteUriUsingPrefixRoute()
        {
            var result = _target.Initialize(
                "http://localhost/",
                "http://localhost/#/route/details/ABC?tab=Peers",
                "http://localhost/",
                new
                {
                    canonicalizeToHash = true,
                    hashPrefix = "route",
                    interceptInternalLinks = true
                });

            result.Should().Be("http://localhost/details/ABC?tab=Peers");
            _target.GetLocationHref().Should().Be("http://localhost/#/route/details/ABC?tab=Peers");
        }

        [Fact]
        public void GIVEN_CustomHashPrefixAndBasePath_WHEN_NavigateToCalled_THEN_HistoryUsesCanonicalHashUriRelativeToBasePath()
        {
            _target.Initialize(
                "http://localhost/proxy/app/",
                "http://localhost/proxy/app/#/route/",
                "http://localhost/proxy/app/",
                new
                {
                    canonicalizeToHash = true,
                    hashPrefix = "route",
                    interceptInternalLinks = true
                });

            _target.NavigateTo("http://localhost/proxy/app/settings", false, "state");

            _target.GetLocationHref().Should().Be("http://localhost/proxy/app/#/route/settings");
            _target.GetHistoryState().Should().BeEquivalentTo(new BrowserHistoryState
            {
                HistoryIndex = 1,
                UserState = "state"
            });
        }

        [Fact]
        public void GIVEN_ReplaceNavigation_WHEN_NavigateToCalled_THEN_HistoryIndexIsRetained()
        {
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");
            _target.SetLocationAndHistory("http://localhost/#/first", null);
            _target.SetCurrentHistoryIndex(3);

            _target.NavigateTo("http://localhost/second", true, "second");

            _target.GetLocationHref().Should().Be("http://localhost/#/second");
            _target.GetHistoryState().Should().BeEquivalentTo(new BrowserHistoryState
            {
                HistoryIndex = 3,
                UserState = "second"
            });
        }

        [Fact]
        public void GIVEN_ReplaceExternalNavigation_WHEN_NavigateExternallyCalled_THEN_WindowLocationReplaceIsUsed()
        {
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");

            _target.NavigateExternally("https://example.com/path?query=value", true);

            _target.GetLocationHref().Should().Be("https://example.com/path?query=value");
            _target.GetLastReplacedHref().Should().Be("https://example.com/path?query=value");
        }

        [Fact]
        public void GIVEN_InternalForceLoad_WHEN_ForceLoadCalled_THEN_UpdatesLocationAndReloadsDocument()
        {
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");

            _target.ForceLoad("http://localhost/#/settings", false);

            _target.GetLocationHref().Should().Be("http://localhost/#/settings");
            _target.GetLastReloadedHref().Should().Be("http://localhost/#/settings");
            _target.GetReloadCount().Should().Be(1);
        }

        [Fact]
        public void GIVEN_InternalAnchorPresentBeforeInitialization_WHEN_InitializeCalled_THEN_AnchorHrefIsCanonicalizedToHashRoute()
        {
            var anchorIndex = _target.AppendAnchor("details/ABC?tab=Peers");

            _target.Initialize(
                "http://localhost/",
                "http://localhost/#/",
                "http://localhost/");

            _target.GetAnchorHref(anchorIndex).Should().Be("http://localhost/#/details/ABC?tab=Peers");
        }

        [Fact]
        public void GIVEN_InternalAnchorWithDotSlashHref_WHEN_InitializeCalled_THEN_AnchorHrefIsCanonicalizedToHashRoute()
        {
            var anchorIndex = _target.AppendAnchor("./tags");

            _target.Initialize(
                "http://localhost/",
                "http://localhost/#/cookies",
                "http://localhost/cookies");

            _target.GetAnchorHref(anchorIndex).Should().Be("http://localhost/#/tags");
        }

        [Fact]
        public void GIVEN_InternalAnchorWithDotSlashHrefAndRootLocation_WHEN_InitializeCalled_THEN_AnchorHrefIsCanonicalizedToHashRoute()
        {
            var anchorIndex = _target.AppendAnchor("./app-settings");

            _target.Initialize(
                "http://localhost/",
                "http://localhost",
                "http://localhost/");

            _target.GetAnchorHref(anchorIndex).Should().Be("http://localhost/#/app-settings");
        }

        [Fact]
        public void GIVEN_InternalAnchorAddedAfterInitialization_WHEN_AnchorObserved_THEN_AnchorHrefIsCanonicalizedToHashRoute()
        {
            _target.Initialize(
                "http://localhost/proxy/app/",
                "http://localhost/proxy/app/#/",
                "http://localhost/proxy/app/");

            var anchorIndex = _target.AppendAnchor("settings");

            _target.GetAnchorHref(anchorIndex).Should().Be("http://localhost/proxy/app/#/settings");
        }

        [Fact]
        public void GIVEN_InternalAnchor_WHEN_InitializeCalledWithLinkInterceptionDisabled_THEN_AnchorHrefRemainsPathRoute()
        {
            var anchorIndex = _target.AppendAnchor("settings");

            _target.Initialize(
                "http://localhost/",
                "http://localhost/#/",
                "http://localhost/",
                new
                {
                    canonicalizeToHash = true,
                    hashPrefix = "/",
                    interceptInternalLinks = false
                });

            _target.GetAnchorHref(anchorIndex).Should().Be("http://localhost/settings");
        }

        [Fact]
        public void GIVEN_InternalAnchorWithNonSelfTarget_WHEN_InitializeCalled_THEN_AnchorHrefRemainsPathRoute()
        {
            var anchorIndex = _target.AppendAnchor("settings", "_blank");

            _target.Initialize(
                "http://localhost/",
                "http://localhost/#/",
                "http://localhost/");

            _target.GetAnchorHref(anchorIndex).Should().Be("http://localhost/settings");
        }

        [Fact]
        public void GIVEN_RapidBrowserNavigationsBeforeLockCheckRuns_WHEN_LatestNavigationAllowed_THEN_BlazorIsNotifiedForLatestBrowserUrlOnly()
        {
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");
            _target.NavigateTo("http://localhost/first", false, "first");
            _target.NavigateTo("http://localhost/second", false, "second");
            _target.NavigateTo("http://localhost/third", false, "third");
            _target.SetNavigationLockState(true);
            _target.EnqueueLocationChangingResult(true);

            _target.HistoryGo(-2);
            _target.HistoryGo(1);
            _target.ProcessTasks();

            _target.GetLocationChangingCalls().Last().Location.Should().Be("http://localhost/second");
            _target.GetLocationHref().Should().Be("http://localhost/#/second");
            _target.GetLocationChangedCalls().Last().Location.Should().Be("http://localhost/second");
        }

        [Fact]
        public void GIVEN_StaleDeniedBrowserNavigation_WHEN_NewerNavigationIsPending_THEN_StaleResultDoesNotRollback()
        {
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");
            _target.NavigateTo("http://localhost/first", false, "first");
            _target.NavigateTo("http://localhost/second", false, "second");
            _target.NavigateTo("http://localhost/third", false, "third");
            _target.SetNavigationLockState(true);
            _target.EnqueueLocationChangingInvalidateSideEffect("http://localhost/#/second", 3, "second");
            _target.EnqueueLocationChangingResult(false);

            _target.HashChangeTo("http://localhost/#/first", 3, "first");

            _target.GetLocationHref().Should().Be("http://localhost/#/second");
            _target.GetLocationChangedCalls().Should().BeEmpty();
        }

        [Fact]
        public void GIVEN_StaleAllowedBrowserNavigation_WHEN_NewerNavigationIsPending_THEN_StaleResultDoesNotNotify()
        {
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");
            _target.NavigateTo("http://localhost/first", false, "first");
            _target.NavigateTo("http://localhost/second", false, "second");
            _target.NavigateTo("http://localhost/third", false, "third");
            _target.SetNavigationLockState(true);
            _target.EnqueueLocationChangingHashChangeSideEffect("http://localhost/#/second", 3, "second");
            _target.EnqueueLocationChangingResult(true);
            _target.EnqueueLocationChangingResult(true);

            _target.HashChangeTo("http://localhost/#/first", 3, "first");

            _target.GetLocationHref().Should().Be("http://localhost/#/second");
            _target.GetLocationChangedCalls().Should().ContainSingle().Which.Location.Should().Be("http://localhost/second");
        }

        [Fact]
        public void GIVEN_PopStateAndHashChangePair_WHEN_BrowserNavigates_THEN_BlazorIsNotifiedOnce()
        {
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");
            _target.NavigateTo("http://localhost/first", false, "first");
            _target.NavigateTo("http://localhost/second", false, "second");

            _target.HistoryGo(-1);
            _target.ProcessTasks();

            _target.GetLocationHref().Should().Be("http://localhost/#/first");
            _target.GetLocationChangedCalls().Should().ContainSingle().Which.Location.Should().Be("http://localhost/first");
        }

        [Fact]
        public void GIVEN_LatestBrowserNavigationDenied_WHEN_LockCheckCompletes_THEN_BrowserRemainsOnLastAcceptedUrl()
        {
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");
            _target.NavigateTo("http://localhost/first", false, "first");
            _target.NavigateTo("http://localhost/second", false, "second");
            _target.SetNavigationLockState(true);
            _target.EnqueueLocationChangingResult(false);

            _target.HistoryGo(-1);
            _target.ProcessTasks();

            _target.GetLocationHref().Should().Be("http://localhost/#/second");
            _target.GetLocationChangedCalls().Should().BeEmpty();
        }

        [Fact]
        public void GIVEN_DeniedBrowserNavigationThenProgrammaticNavigation_WHEN_ReturningToSuppressedEntry_THEN_BlazorIsNotified()
        {
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");
            _target.NavigateTo("http://localhost/first", false, "first");
            _target.NavigateTo("http://localhost/second", false, "second");
            _target.SetNavigationLockState(true);
            _target.EnqueueLocationChangingResult(false);

            _target.HistoryGo(-1);
            _target.ProcessTasks();

            _target.NavigateTo("http://localhost/third", false, "third");
            _target.EnqueueLocationChangingResult(true);

            _target.HistoryGo(-1);
            _target.ProcessTasks();

            _target.GetLocationHref().Should().Be("http://localhost/#/second");
            _target.GetLocationChangedCalls().Should().ContainSingle().Which.Location.Should().Be("http://localhost/second");
        }

        [Fact]
        public void GIVEN_InternalAnchorClicked_WHEN_LinkNavigationAllowed_THEN_LinkNavigationStillPushesHistoryAndNotifiesOnce()
        {
            var anchorIndex = _target.AppendAnchor("settings");
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");

            _target.ClickAnchor(anchorIndex);
            _target.ProcessTasks();

            _target.GetLocationHref().Should().Be("http://localhost/#/settings");
            _target.GetHistoryState().Should().BeEquivalentTo(new BrowserHistoryState
            {
                HistoryIndex = 1,
                UserState = null
            });
            _target.GetLocationChangedCalls().Should().ContainSingle().Which.Should().BeEquivalentTo(new BrowserLocationChangedCall
            {
                Location = "http://localhost/settings",
                HistoryEntryState = null,
                IsNavigationIntercepted = true
            });
        }

        [Fact]
        public void GIVEN_UnrelatedBrowserNavigationArrivesDuringSuppressedWait_WHEN_NavigationKeysDoNotMatch_THEN_UnrelatedNavigationIsProcessed()
        {
            _target.Initialize("http://localhost/", "http://localhost/#/", "http://localhost/");
            _target.EnqueueSuppressedBrowserNavigationResolver("http://localhost/#/expected", 99, "expected");

            _target.HashChangeTo("http://localhost/#/third", 3, "third");
            _target.ProcessTasks();

            _target.GetLocationHref().Should().Be("http://localhost/#/third");
            _target.GetLocationChangedCalls().Should().ContainSingle().Which.Location.Should().Be("http://localhost/third");
            _target.GetSuppressedBrowserNavigationResolverCount().Should().Be(1);
        }

        private sealed class HashRoutingJavaScriptTestHost
        {
            private readonly Engine _engine;
            private static readonly JsonSerializerOptions? _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            public HashRoutingJavaScriptTestHost()
            {
                _engine = new Engine();
                _engine.SetValue("__urlFactory", new JavaScriptUrlFactory());
                _engine.Execute(_testHarnessScript);
                _engine.Execute(GetModuleScript());
            }

            public BrowserHistoryState? GetHistoryState()
            {
                var json = _engine.Invoke("__getHistoryStateJson").AsString();

                return JsonSerializer.Deserialize<BrowserHistoryState>(json, _options);
            }

            public IReadOnlyList<BrowserLocationChangingCall> GetLocationChangingCalls()
            {
                var json = _engine.Invoke("__getLocationChangingCallsJson").AsString();

                return JsonSerializer.Deserialize<List<BrowserLocationChangingCall>>(json, _options) ?? [];
            }

            public IReadOnlyList<BrowserLocationChangedCall> GetLocationChangedCalls()
            {
                var json = _engine.Invoke("__getLocationChangedCallsJson").AsString();

                return JsonSerializer.Deserialize<List<BrowserLocationChangedCall>>(json, _options) ?? [];
            }

            public string GetLastReplacedHref()
            {
                return _engine.Invoke("__getLastReplacedHref").AsString();
            }

            public string GetLastReloadedHref()
            {
                return _engine.Invoke("__getLastReloadedHref").AsString();
            }

            public string GetLocationHref()
            {
                return _engine.Invoke("__getLocationHref").AsString();
            }

            public int GetReloadCount()
            {
                return (int)_engine.Invoke("__getReloadCount").AsNumber();
            }

            public int AppendAnchor(string href, string? target = null, bool download = false)
            {
                var targetValue = target is null
                    ? JsValue.Null
                    : JsValue.FromObject(_engine, target);

                return (int)_engine.Invoke("__appendAnchor", href, targetValue, download).AsNumber();
            }

            public string GetAnchorHref(int index)
            {
                return _engine.Invoke("__getAnchorHref", index).AsString();
            }

            public void ClickAnchor(int index)
            {
                _engine.Invoke("__clickAnchor", index);
            }

            public string Initialize(string baseUri, string locationHref, string currentPathUri, object? options = null)
            {
                _engine.Invoke("__setDocumentBaseUri", baseUri);
                SetLocationAndHistory(locationHref, null);

                var initializeOptions = options ?? new
                {
                    canonicalizeToHash = true,
                    hashPrefix = "/",
                    interceptInternalLinks = true
                };

                return _engine.Invoke("initialize", _engine.Evaluate("dotNetObjectReference"), initializeOptions, baseUri, currentPathUri).AsString();
            }

            public void NavigateExternally(string uri, bool replaceHistoryEntry)
            {
                _engine.Invoke("navigateExternally", uri, replaceHistoryEntry);
            }

            public void ForceLoad(string uri, bool replaceHistoryEntry)
            {
                _engine.Invoke("forceLoad", uri, replaceHistoryEntry);
            }

            public void NavigateTo(string pathAbsoluteUri, bool replaceHistoryEntry, string? historyEntryState)
            {
                _engine.Invoke("navigateTo", pathAbsoluteUri, replaceHistoryEntry, historyEntryState ?? JsValue.Null);
            }

            public void SetNavigationLockState(bool value)
            {
                _engine.Invoke("setNavigationLockState", value);
            }

            public void EnqueueLocationChangingResult(bool value)
            {
                _engine.Invoke("__enqueueLocationChangingResult", value);
            }

            public void EnqueueLocationChangingHistoryGoSideEffect(int delta)
            {
                _engine.Invoke("__enqueueLocationChangingHistoryGoSideEffect", delta);
            }

            public void EnqueueLocationChangingHashChangeSideEffect(string href, int historyIndex, string? userState)
            {
                _engine.Invoke("__enqueueLocationChangingHashChangeSideEffect", href, historyIndex, userState ?? JsValue.Null);
            }

            public void EnqueueLocationChangingInvalidateSideEffect(string href, int historyIndex, string? userState)
            {
                _engine.Invoke("__enqueueLocationChangingInvalidateSideEffect", href, historyIndex, userState ?? JsValue.Null);
            }

            public void HistoryGo(int delta)
            {
                _engine.Invoke("__historyGo", delta);
            }

            public void HashChangeTo(string href, int historyIndex, string? userState)
            {
                _engine.Invoke("__hashChangeTo", href, historyIndex, userState ?? JsValue.Null);
            }

            public void EnqueueSuppressedBrowserNavigationResolver(string href, int historyIndex, string? userState)
            {
                _engine.Invoke("__enqueueSuppressedBrowserNavigationResolver", href, historyIndex, userState ?? JsValue.Null);
            }

            public int GetSuppressedBrowserNavigationResolverCount()
            {
                return (int)_engine.Invoke("__getSuppressedBrowserNavigationResolverCount").AsNumber();
            }

            public void ProcessTasks()
            {
                for (var index = 0; index < 20; index++)
                {
                    _engine.Invoke("__runTimers");
                    _engine.Advanced.ProcessTasks();
                }
            }

            public void SetLocationAndHistory(string href, BrowserHistoryState? state)
            {
                if (state is null)
                {
                    _engine.Invoke("__setLocationAndHistory", href, JsValue.Null, JsValue.Null);
                    return;
                }

                var userState = state.UserState is null
                    ? JsValue.Null
                    : JsValue.FromObject(_engine, state.UserState);

                _engine.Invoke("__setLocationAndHistory", href, state.HistoryIndex, userState);
            }

            public void SetCurrentHistoryIndex(int historyIndex)
            {
                _engine.Invoke("__setCurrentHistoryIndex", historyIndex);
            }

            private static string GetModuleScript()
            {
                var path = Path.Combine(AppContext.BaseDirectory, "hash-routing.module.js");
                var script = File.ReadAllText(path);

                return script.Replace("export function ", "function ", StringComparison.Ordinal);
            }

            private const string _testHarnessScript = """
function URL(raw, base) {
    const inner = __urlFactory.Create(raw, base === undefined || base === null ? null : String(base));
    const wrapper = Object.create(URL.prototype);

    Object.defineProperties(wrapper, {
        hash: {
            get: function() { return inner.Hash; },
            set: function(value) { inner.Hash = value; }
        },
        host: {
            get: function() { return inner.Host; }
        },
        hostname: {
            get: function() { return inner.Hostname; },
            set: function(value) { inner.Hostname = value; }
        },
        href: {
            get: function() { return inner.Href; },
            set: function(value) { inner.Href = value; }
        },
        pathname: {
            get: function() { return inner.Pathname; },
            set: function(value) { inner.Pathname = value; }
        },
        port: {
            get: function() { return inner.Port; },
            set: function(value) { inner.Port = value; }
        },
        protocol: {
            get: function() { return inner.Protocol; },
            set: function(value) { inner.Protocol = value; }
        },
        search: {
            get: function() { return inner.Search; },
            set: function(value) { inner.Search = value; }
        }
    });

    return wrapper;
}

function Element() {
}

Element.prototype.closest = function(selector) {
    return this.matches(selector) ? this : null;
};

Element.prototype.matches = function() {
    return false;
};

Element.prototype.querySelectorAll = function() {
    return [];
};

function AnchorElement(href, target, download) {
    this._attributes = {};

    if (href !== null && href !== undefined) {
        this._attributes.href = String(href);
    }

    if (target !== null && target !== undefined) {
        this._attributes.target = String(target);
    }

    if (download) {
        this._attributes.download = "";
    }
}

AnchorElement.prototype = Object.create(Element.prototype);
AnchorElement.prototype.constructor = AnchorElement;

AnchorElement.prototype.getAttribute = function(name) {
    return Object.prototype.hasOwnProperty.call(this._attributes, name)
        ? this._attributes[name]
        : null;
};

AnchorElement.prototype.setAttribute = function(name, value) {
    this._attributes[name] = String(value);

    if (document._mutationObserver && name === "href") {
        document._mutationObserver._callback([{
            type: "attributes",
            target: this,
            attributeName: "href"
        }]);
    }
};

AnchorElement.prototype.hasAttribute = function(name) {
    return Object.prototype.hasOwnProperty.call(this._attributes, name);
};

AnchorElement.prototype.matches = function(selector) {
    return selector === "a[href]" && this.hasAttribute("href");
};

Object.defineProperty(AnchorElement.prototype, "href", {
    get: function() {
        return new URL(this.getAttribute("href") || "", document.baseURI).href;
    },
    set: function(value) {
        this.setAttribute("href", value);
    }
});

function MutationObserver(callback) {
    this._callback = callback;
}

MutationObserver.prototype.observe = function() {
    document._mutationObserver = this;
};

MutationObserver.prototype.disconnect = function() {
    if (document._mutationObserver === this) {
        document._mutationObserver = null;
    }
};

const dotNetObjectReference = {
    _locationChangingCalls: [],
    _locationChangedCalls: [],
    _locationChangingResults: [],
    _locationChangingHistoryGoSideEffects: [],
    _locationChangingHashChangeSideEffects: [],
    _locationChangingInvalidateSideEffects: [],
    invokeMethodAsync: function(methodName, location, historyEntryState, isNavigationIntercepted) {
        if (methodName === "NotifyLocationChangingFromJs") {
            this._locationChangingCalls.push({
                location: location,
                historyEntryState: historyEntryState,
                isNavigationIntercepted: Boolean(isNavigationIntercepted)
            });

            if (this._locationChangingHistoryGoSideEffects.length > 0) {
                window.history.go(this._locationChangingHistoryGoSideEffects.shift());
            }

            if (this._locationChangingHashChangeSideEffects.length > 0) {
                const sideEffect = this._locationChangingHashChangeSideEffects.shift();
                __hashChangeTo(sideEffect.href, sideEffect.historyIndex, sideEffect.userState);
            }

            if (this._locationChangingInvalidateSideEffects.length > 0) {
                const sideEffect = this._locationChangingInvalidateSideEffects.shift();
                __setLocationAndHistoryEntry(sideEffect.href, sideEffect.historyIndex, sideEffect.userState);
                hashRoutingState.currentBrowserNavigationId++;
            }

            const result = this._locationChangingResults.length > 0
                ? this._locationChangingResults.shift()
                : true;

            return Boolean(result);
        }

        if (methodName === "NotifyLocationChangedFromJs") {
            this._locationChangedCalls.push({
                location: location,
                historyEntryState: historyEntryState,
                isNavigationIntercepted: Boolean(isNavigationIntercepted)
            });

            return null;
        }

        return null;
    }
};

let nextTimerId = 0;
let timers = [];

function setTimeout(callback) {
    const timer = {
        id: ++nextTimerId,
        callback: callback,
        active: true
    };
    timers.push(timer);

    return timer.id;
}

function clearTimeout(timerId) {
    for (const timer of timers) {
        if (timer.id === timerId) {
            timer.active = false;
        }
    }
}

const document = {
    baseURI: "http://localhost/",
    _events: {},
    _anchors: [],
    _mutationObserver: null,
    addEventListener: function(name, handler) {
        this._events[name] = handler;
    },
    removeEventListener: function(name) {
        delete this._events[name];
    },
    querySelectorAll: function(selector) {
        if (selector !== "a[href]") {
            return [];
        }

        return this._anchors.filter(function(anchor) {
            return anchor.matches(selector);
        });
    }
};

document.body = document;
document.documentElement = document;

const window = {
    _events: {},
    _lastReplacedHref: "",
    _lastReloadedHref: "",
    _reloadCount: 0,
    _throwHistoryGoAfter: null,
    _ignoreHistoryGoAfter: null,
    location: {
        href: "http://localhost/#/",
        hash: "#/",
        replace: function(uri) {
            window._lastReplacedHref = String(uri);
            this.href = new URL(uri, this.href).href;
            __syncLocation();
        },
        reload: function() {
            window._lastReloadedHref = this.href;
            window._reloadCount++;
        }
    },
    history: {
        _entries: [],
        _entryIndex: -1,
        state: null,
        pushState: function(state, unused, uri) {
            const href = new URL(uri, window.location.href).href;
            this._entries = this._entries.slice(0, this._entryIndex + 1);
            this._entries.push({
                href: href,
                state: state
            });
            this._entryIndex = this._entries.length - 1;
            this.state = state;
            window.location.href = href;
            __syncLocation();
        },
        replaceState: function(state, unused, uri) {
            const href = new URL(uri, window.location.href).href;
            if (this._entryIndex < 0) {
                this._entries.push({
                    href: href,
                    state: state
                });
                this._entryIndex = 0;
            } else {
                this._entries[this._entryIndex] = {
                    href: href,
                    state: state
                };
            }
            this.state = state;
            window.location.href = href;
            __syncLocation();
        },
        go: function(delta) {
            if (window._throwHistoryGoAfter !== null) {
                if (window._throwHistoryGoAfter <= 0) {
                    window._throwHistoryGoAfter = null;
                    throw new Error("history.go failed");
                }

                window._throwHistoryGoAfter--;
            }

            if (window._ignoreHistoryGoAfter !== null) {
                if (window._ignoreHistoryGoAfter <= 0) {
                    window._ignoreHistoryGoAfter = null;
                    __runTimers();
                    return;
                }

                window._ignoreHistoryGoAfter--;
            }

            const nextIndex = this._entryIndex + Number(delta);
            if (nextIndex < 0 || nextIndex >= this._entries.length) {
                return;
            }

            const previousHash = window.location.hash;
            const entry = this._entries[nextIndex];
            this._entryIndex = nextIndex;
            this.state = entry.state;
            window.location.href = entry.href;
            __syncLocation();
            __dispatchWindowEvent("popstate", {
                state: this.state
            });

            if (window.location.hash !== previousHash) {
                __dispatchWindowEvent("hashchange", {});
            }
        }
    },
    addEventListener: function(name, handler) {
        this._events[name] = handler;
    },
    removeEventListener: function(name) {
        delete this._events[name];
    }
};

function __getHistoryStateJson() {
    return JSON.stringify(window.history.state);
}

function __getLocationChangingCallsJson() {
    return JSON.stringify(dotNetObjectReference._locationChangingCalls);
}

function __getLocationChangedCallsJson() {
    return JSON.stringify(dotNetObjectReference._locationChangedCalls);
}

function __getLastReplacedHref() {
    return window._lastReplacedHref;
}

function __getLastReloadedHref() {
    return window._lastReloadedHref;
}

function __getLocationHref() {
    return window.location.href;
}

function __getReloadCount() {
    return window._reloadCount;
}

function __setDocumentBaseUri(value) {
    document.baseURI = value;
}

function __appendAnchor(href, target, download) {
    const anchor = new AnchorElement(href, target, Boolean(download));
    document._anchors.push(anchor);

    if (document._mutationObserver) {
        document._mutationObserver._callback([{
            type: "childList",
            target: document.body,
            addedNodes: [anchor]
        }]);
    }

    return document._anchors.length - 1;
}

function __getAnchorHref(index) {
    return document._anchors[index].href;
}

function __clickAnchor(index) {
    const anchor = document._anchors[index];
    const handler = document._events.click;
    if (!handler) {
        return;
    }

    handler({
        defaultPrevented: false,
        button: 0,
        metaKey: false,
        ctrlKey: false,
        shiftKey: false,
        altKey: false,
        target: anchor,
        preventDefault: function() {
            this.defaultPrevented = true;
        }
    });
}

function __setLocationAndHistory(href, historyIndex, userState) {
    window._lastReplacedHref = "";
    window._lastReloadedHref = "";
    window._reloadCount = 0;
    dotNetObjectReference._locationChangingCalls = [];
    dotNetObjectReference._locationChangedCalls = [];
    dotNetObjectReference._locationChangingResults = [];
    dotNetObjectReference._locationChangingHistoryGoSideEffects = [];
    dotNetObjectReference._locationChangingHashChangeSideEffects = [];
    dotNetObjectReference._locationChangingInvalidateSideEffects = [];
    timers = [];
    window._throwHistoryGoAfter = null;
    window._ignoreHistoryGoAfter = null;
    window.location.href = href;
    if (historyIndex === null || historyIndex === undefined) {
        window.history.state = null;
    } else {
        window.history.state = {
            _qhrIndex: Number(historyIndex),
            userState: userState === undefined ? null : userState
        };
    }
    __syncLocation();
    window.history._entries = [{
        href: window.location.href,
        state: window.history.state
    }];
    window.history._entryIndex = 0;
}

function __setCurrentHistoryIndex(value) {
    hashRoutingState.currentHistoryIndex = Number(value);
}

function __enqueueLocationChangingResult(value) {
    dotNetObjectReference._locationChangingResults.push(Boolean(value));
}

function __enqueueLocationChangingHistoryGoSideEffect(delta) {
    dotNetObjectReference._locationChangingHistoryGoSideEffects.push(Number(delta));
}

function __enqueueLocationChangingHashChangeSideEffect(href, historyIndex, userState) {
    dotNetObjectReference._locationChangingHashChangeSideEffects.push({
        href: href,
        historyIndex: Number(historyIndex),
        userState: userState === undefined ? null : userState
    });
}

function __enqueueLocationChangingInvalidateSideEffect(href, historyIndex, userState) {
    dotNetObjectReference._locationChangingInvalidateSideEffects.push({
        href: href,
        historyIndex: Number(historyIndex),
        userState: userState === undefined ? null : userState
    });
}

function __historyGo(delta) {
    window.history.go(delta);
}

function __hashChangeTo(href, historyIndex, userState) {
    __setLocationAndHistoryEntry(href, historyIndex, userState);
    __dispatchWindowEvent("hashchange", {});
}

function __enqueueSuppressedBrowserNavigationResolver(href, historyIndex, userState) {
    const historyEntryState = userState === undefined ? null : userState;
    const targetPathAbsoluteUri = toPathAbsoluteUri(href, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix);
    const targetHashAbsoluteUri = toHashAbsoluteUri(targetPathAbsoluteUri, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix);
    hashRoutingState.suppressedBrowserNavigationEventResolvers.push({
        navigationKey: createBrowserNavigationKey(targetHashAbsoluteUri, Number(historyIndex), historyEntryState),
        resolve: function() {
        }
    });
}

function __getSuppressedBrowserNavigationResolverCount() {
    return hashRoutingState.suppressedBrowserNavigationEventResolvers.length;
}

function __throwHistoryGoAfter(callCount) {
    window._throwHistoryGoAfter = Number(callCount);
}

function __ignoreHistoryGoAfter(callCount) {
    window._ignoreHistoryGoAfter = Number(callCount);
}

function __runTimers() {
    const scheduledTimers = timers;
    timers = [];

    for (const timer of scheduledTimers) {
        if (timer.active) {
            timer.callback();
        }
    }
}

function __setLocationAndHistoryEntry(href, historyIndex, userState) {
    window.location.href = href;
    window.history.state = {
        _qhrIndex: Number(historyIndex),
        userState: userState === undefined ? null : userState
    };
    __syncLocation();
}

function __dispatchWindowEvent(name, event) {
    const handler = window._events[name];
    if (handler) {
        handler(event);
    }
}

function __syncLocation() {
    const absolute = new URL(window.location.href, document.baseURI);
    window.location.href = absolute.href;
    window.location.hash = absolute.hash;
}
""";
        }

        private sealed class JavaScriptUrlFactory
        {
            public JavaScriptUrl Create(string raw, string? baseUri)
            {
                return new JavaScriptUrl(raw, baseUri);
            }
        }

        private sealed class JavaScriptUrl
        {
            private UriBuilder _builder;

            public JavaScriptUrl(string raw, string? baseUri)
            {
                ArgumentNullException.ThrowIfNull(raw);

                _builder = CreateBuilder(raw, baseUri);
            }

            public string Hash
            {
                get
                {
                    return _builder.Fragment.Length == 0 ? string.Empty : _builder.Fragment;
                }

                set
                {
                    _builder.Fragment = NormalizeFragment(value);
                }
            }

            public string Host
            {
                get
                {
                    if (_builder.Port < 0)
                    {
                        return _builder.Host;
                    }

                    return _builder.Host + ":" + _builder.Port.ToString(CultureInfo.InvariantCulture);
                }
            }

            public string Hostname
            {
                get
                {
                    return _builder.Host;
                }

                set
                {
                    _builder.Host = value;
                }
            }

            public string Href
            {
                get
                {
                    return _builder.Uri.AbsoluteUri;
                }

                set
                {
                    _builder = CreateBuilder(value, null);
                }
            }

            public string Pathname
            {
                get
                {
                    return string.IsNullOrEmpty(_builder.Path) ? "/" : _builder.Path;
                }

                set
                {
                    _builder.Path = NormalizePath(value);
                }
            }

            public string Port
            {
                get
                {
                    return _builder.Port < 0 ? string.Empty : _builder.Port.ToString(CultureInfo.InvariantCulture);
                }

                set
                {
                    _builder.Port = string.IsNullOrEmpty(value) ? -1 : int.Parse(value, CultureInfo.InvariantCulture);
                }
            }

            public string Protocol
            {
                get
                {
                    return _builder.Scheme + ":";
                }

                set
                {
                    _builder.Scheme = value.TrimEnd(':');
                }
            }

            public string Search
            {
                get
                {
                    return string.IsNullOrEmpty(_builder.Query) ? string.Empty : _builder.Query;
                }

                set
                {
                    _builder.Query = NormalizeQuery(value);
                }
            }

            private static UriBuilder CreateBuilder(string raw, string? baseUri)
            {
                var uri = Uri.TryCreate(raw, UriKind.Absolute, out var absoluteUri)
                    ? absoluteUri
                    : new Uri(new Uri(baseUri ?? "http://localhost/", UriKind.Absolute), raw);

                return new UriBuilder(uri);
            }

            private static string NormalizeFragment(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return string.Empty;
                }

                return value[0] == '#'
                    ? value[1..]
                    : value;
            }

            private static string NormalizePath(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return "/";
                }

                return value[0] == '/'
                    ? value
                    : "/" + value;
            }

            private static string NormalizeQuery(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return string.Empty;
                }

                return value[0] == '?'
                    ? value[1..]
                    : value;
            }
        }

        private sealed class BrowserHistoryState
        {
            [JsonPropertyName("_qhrIndex")]
            public int HistoryIndex { get; init; }

            [JsonPropertyName("userState")]
            public string? UserState { get; init; }
        }

        private sealed class BrowserLocationChangingCall
        {
            [JsonPropertyName("location")]
            public string? Location { get; init; }

            [JsonPropertyName("historyEntryState")]
            public string? HistoryEntryState { get; init; }

            [JsonPropertyName("isNavigationIntercepted")]
            public bool IsNavigationIntercepted { get; init; }
        }

        private sealed class BrowserLocationChangedCall
        {
            [JsonPropertyName("location")]
            public string? Location { get; init; }

            [JsonPropertyName("historyEntryState")]
            public string? HistoryEntryState { get; init; }

            [JsonPropertyName("isNavigationIntercepted")]
            public bool IsNavigationIntercepted { get; init; }
        }
    }
}
