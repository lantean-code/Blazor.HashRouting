const hashRoutingState = {
    dotNetObjectReference: null,
    initialized: false,
    navigationLockEnabled: false,
    baseUri: "",
    baseUriWithoutTrailingSlash: "",
    baseProtocol: "",
    baseHostname: "",
    basePort: "",
    normalizedHashPrefix: "/",
    options: {
        canonicalizeToHash: true,
        hashPrefix: "/",
        interceptInternalLinks: true,
    },
    currentHistoryIndex: 0,
    lastAcceptedHashAbsoluteUri: "",
    lastAcceptedHistoryState: undefined,
    clickHandler: null,
    hashChangeHandler: null,
    popStateHandler: null,
    anchorMutationObserver: null,
    processingBrowserNavigation: false,
    lastProcessedBrowserNavigationKey: "",
};

export function initialize(dotNetObjectReference, options, baseUri, currentPathUri) {
    hashRoutingState.dotNetObjectReference = dotNetObjectReference;
    hashRoutingState.options = normalizeOptions(options);
    hashRoutingState.normalizedHashPrefix = hashRoutingState.options.hashPrefix;
    hashRoutingState.baseUri = normalizeBaseUri(baseUri);
    hashRoutingState.baseUriWithoutTrailingSlash = hashRoutingState.baseUri.endsWith("/") ? hashRoutingState.baseUri.slice(0, -1) : hashRoutingState.baseUri;

    const baseUriUrl = new URL(hashRoutingState.baseUri);
    hashRoutingState.baseProtocol = baseUriUrl.protocol;
    hashRoutingState.baseHostname = baseUriUrl.hostname;
    hashRoutingState.basePort = baseUriUrl.port;

    if (!hashRoutingState.initialized) {
        attachHandlers();
        startAnchorMonitoring();
        hashRoutingState.initialized = true;
    }

    const initialPathAbsoluteUri = toPathAbsoluteUri(window.location.href, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix) || currentPathUri;
    const initialHashAbsoluteUri = toHashAbsoluteUri(initialPathAbsoluteUri, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix);

    hashRoutingState.currentHistoryIndex = getHistoryIndex(window.history.state);
    hashRoutingState.lastAcceptedHashAbsoluteUri = initialHashAbsoluteUri;
    hashRoutingState.lastAcceptedHistoryState = getHistoryUserState(window.history.state);
    hashRoutingState.lastProcessedBrowserNavigationKey = "";

    const canCanonicalizeInitialHash = isCanonicalizableHash(window.location.hash, hashRoutingState.normalizedHashPrefix);
    if (hashRoutingState.options.canonicalizeToHash && canCanonicalizeInitialHash && !sameUri(window.location.href, initialHashAbsoluteUri)) {
        const replacementState = withHistoryMetadata(window.history.state, hashRoutingState.lastAcceptedHistoryState, hashRoutingState.currentHistoryIndex);
        window.history.replaceState(replacementState, "", initialHashAbsoluteUri);
    }

    return initialPathAbsoluteUri;
}

export function navigateTo(pathAbsoluteUri, replaceHistoryEntry, historyEntryState) {
    const normalizedHistoryEntryState = normalizeHistoryState(historyEntryState);
    const shouldReplaceHistoryEntry = Boolean(replaceHistoryEntry);

    const hashAbsoluteUri = toHashAbsoluteUri(pathAbsoluteUri, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix);
    commitNavigation(hashAbsoluteUri, shouldReplaceHistoryEntry, normalizedHistoryEntryState);
}

export function navigateExternally(uri, replaceHistoryEntry) {
    if (replaceHistoryEntry) {
        window.location.replace(uri);
        return;
    }

    window.location.href = uri;
}

export function forceLoad(uri, replaceHistoryEntry) {
    if (replaceHistoryEntry) {
        window.history.replaceState(window.history.state, "", uri);
    } else {
        window.history.pushState(window.history.state, "", uri);
    }

    window.location.reload();
}

export function setNavigationLockState(value) {
    hashRoutingState.navigationLockEnabled = Boolean(value);
}

export function dispose() {
    if (!hashRoutingState.initialized) {
        return;
    }

    if (hashRoutingState.clickHandler) {
        document.removeEventListener("click", hashRoutingState.clickHandler, true);
    }

    if (hashRoutingState.hashChangeHandler) {
        window.removeEventListener("hashchange", hashRoutingState.hashChangeHandler);
    }

    if (hashRoutingState.popStateHandler) {
        window.removeEventListener("popstate", hashRoutingState.popStateHandler);
    }

    hashRoutingState.initialized = false;
    hashRoutingState.dotNetObjectReference = null;
    hashRoutingState.baseUri = "";
    hashRoutingState.baseUriWithoutTrailingSlash = "";
    hashRoutingState.baseProtocol = "";
    hashRoutingState.baseHostname = "";
    hashRoutingState.basePort = "";
    hashRoutingState.normalizedHashPrefix = "/";
    hashRoutingState.clickHandler = null;
    hashRoutingState.hashChangeHandler = null;
    hashRoutingState.popStateHandler = null;
    if (hashRoutingState.anchorMutationObserver) {
        hashRoutingState.anchorMutationObserver.disconnect();
    }
    hashRoutingState.anchorMutationObserver = null;
    hashRoutingState.lastProcessedBrowserNavigationKey = "";
}

function attachHandlers() {
    hashRoutingState.clickHandler = function (event) {
        if (!hashRoutingState.options.interceptInternalLinks) {
            return;
        }

        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        const anchor = event.target instanceof Element ? event.target.closest("a[href]") : null;
        if (!anchor) {
            return;
        }

        if (anchor.hasAttribute("download")) {
            return;
        }

        const target = anchor.getAttribute("target");
        if (target && target !== "_self") {
            return;
        }

        const absoluteHrefUrl = tryCreateAnchorAbsoluteHrefUrl(anchor);
        if (!absoluteHrefUrl) {
            return;
        }

        if (!isCanonicalizableHash(absoluteHrefUrl.hash, hashRoutingState.normalizedHashPrefix)) {
            return;
        }

        if (!isWithinBaseUriSpace(absoluteHrefUrl)) {
            return;
        }

        event.preventDefault();
        void processInterceptedLinkNavigation(absoluteHrefUrl.href);
    };

    hashRoutingState.hashChangeHandler = function () {
        void processBrowserNavigation(window.location.href, false);
    };

    hashRoutingState.popStateHandler = function () {
        hashRoutingState.currentHistoryIndex = getHistoryIndex(window.history.state);
        void processBrowserNavigation(window.location.href, false);
    };

    document.addEventListener("click", hashRoutingState.clickHandler, true);
    window.addEventListener("hashchange", hashRoutingState.hashChangeHandler);
    window.addEventListener("popstate", hashRoutingState.popStateHandler);
}

function startAnchorMonitoring() {
    rewriteInternalAnchors();

    if (typeof MutationObserver !== "function") {
        return;
    }

    const observationTarget = document.body || document.documentElement || document;
    hashRoutingState.anchorMutationObserver = new MutationObserver(function (mutations) {
        for (const mutation of mutations) {
            if (mutation.type === "attributes") {
                rewriteAnchorsForNode(mutation.target);
                continue;
            }

            if (!mutation.addedNodes) {
                continue;
            }

            for (const addedNode of mutation.addedNodes) {
                rewriteAnchorsForNode(addedNode);
            }
        }
    });

    hashRoutingState.anchorMutationObserver.observe(observationTarget, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["href"],
    });
}

function rewriteInternalAnchors() {
    if (typeof document.querySelectorAll !== "function") {
        return;
    }

    for (const anchor of document.querySelectorAll("a[href]")) {
        rewriteAnchorHref(anchor);
    }
}

function rewriteAnchorsForNode(node) {
    if (!(node instanceof Element)) {
        return;
    }

    if (typeof node.matches === "function" && node.matches("a[href]")) {
        rewriteAnchorHref(node);
    }

    if (typeof node.querySelectorAll !== "function") {
        return;
    }

    for (const anchor of node.querySelectorAll("a[href]")) {
        rewriteAnchorHref(anchor);
    }
}

function rewriteAnchorHref(anchor) {
    const target = anchor.getAttribute("target");
    if (!hashRoutingState.options.interceptInternalLinks || anchor.hasAttribute("download") || (target && target !== "_self")) {
        return;
    }

    const absoluteHrefUrl = tryCreateAnchorAbsoluteHrefUrl(anchor);
    if (!absoluteHrefUrl) {
        return;
    }

    if (!isCanonicalizableHash(absoluteHrefUrl.hash, hashRoutingState.normalizedHashPrefix)) {
        return;
    }

    if (!isWithinBaseUriSpace(absoluteHrefUrl)) {
        return;
    }

    const pathAbsoluteUri = toPathAbsoluteUriFromAbsolute(absoluteHrefUrl, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix);
    const hashAbsoluteUri = toHashAbsoluteUri(pathAbsoluteUri, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix);

    if (sameUri(absoluteHrefUrl.href, hashAbsoluteUri)) {
        return;
    }

    anchor.setAttribute("href", hashAbsoluteUri);
}

function tryCreateAnchorAbsoluteHrefUrl(anchor) {
    const authoredHref = typeof anchor.getAttribute === "function"
        ? anchor.getAttribute("href")
        : null;
    const href = authoredHref || anchor.href;
    if (!href) {
        return null;
    }

    try {
        return new URL(href, hashRoutingState.baseUri || document.baseURI);
    } catch {
        return null;
    }
}

async function processBrowserNavigation(rawLocation, interceptedLink) {
    if (hashRoutingState.processingBrowserNavigation) {
        return;
    }

    const historyState = window.history.state;
    const historyIndex = getHistoryIndex(historyState);
    const historyEntryState = normalizeHistoryState(getHistoryUserState(historyState));
    let destination;
    try {
        destination = new URL(rawLocation, hashRoutingState.baseUri);
    } catch {
        return;
    }
    const browserNavigationKey = createBrowserNavigationKey(destination.href, historyIndex, historyEntryState);

    if (browserNavigationKey === hashRoutingState.lastProcessedBrowserNavigationKey) {
        return;
    }

    hashRoutingState.lastProcessedBrowserNavigationKey = browserNavigationKey;
    hashRoutingState.processingBrowserNavigation = true;
    hashRoutingState.currentHistoryIndex = historyIndex;

    try {
        if (!isCanonicalizableHash(destination.hash, hashRoutingState.normalizedHashPrefix)) {
            return;
        }

        const targetPathAbsoluteUri = toPathAbsoluteUriFromAbsolute(destination, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix);
        const targetHashAbsoluteUri = toHashAbsoluteUri(targetPathAbsoluteUri, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix);

        const shouldContinue = await canContinueNavigation(targetPathAbsoluteUri, historyEntryState, interceptedLink);
        if (!shouldContinue) {
            if (hashRoutingState.lastAcceptedHashAbsoluteUri) {
                const rollbackState = withHistoryMetadata(window.history.state, hashRoutingState.lastAcceptedHistoryState, hashRoutingState.currentHistoryIndex);
                window.history.replaceState(rollbackState, "", hashRoutingState.lastAcceptedHashAbsoluteUri);
            }

            return;
        }

        if (!sameUri(window.location.href, targetHashAbsoluteUri)) {
            const replacementState = withHistoryMetadata(window.history.state, historyEntryState, hashRoutingState.currentHistoryIndex);
            window.history.replaceState(replacementState, "", targetHashAbsoluteUri);
        }

        hashRoutingState.lastAcceptedHashAbsoluteUri = targetHashAbsoluteUri;
        hashRoutingState.lastAcceptedHistoryState = historyEntryState;

        notifyLocationChanged(targetPathAbsoluteUri, historyEntryState, interceptedLink);
    } finally {
        hashRoutingState.processingBrowserNavigation = false;
    }
}

async function processInterceptedLinkNavigation(rawLocation) {
    if (hashRoutingState.processingBrowserNavigation) {
        return;
    }

    hashRoutingState.processingBrowserNavigation = true;

    try {
        const destination = new URL(rawLocation, hashRoutingState.baseUri);
        if (!isCanonicalizableHash(destination.hash, hashRoutingState.normalizedHashPrefix)) {
            return;
        }

        const targetPathAbsoluteUri = toPathAbsoluteUriFromAbsolute(destination, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix);
        const targetHashAbsoluteUri = toHashAbsoluteUri(targetPathAbsoluteUri, hashRoutingState.baseUri, hashRoutingState.normalizedHashPrefix);
        const historyEntryState = null;

        const shouldContinue = await canContinueNavigation(targetPathAbsoluteUri, historyEntryState, true);
        if (!shouldContinue) {
            return;
        }

        commitNavigation(targetHashAbsoluteUri, false, historyEntryState);
        notifyLocationChanged(targetPathAbsoluteUri, historyEntryState, true);
    } finally {
        hashRoutingState.processingBrowserNavigation = false;
    }
}

async function canContinueNavigation(pathAbsoluteUri, historyEntryState, interceptedLink) {
    if (!hashRoutingState.navigationLockEnabled) {
        return true;
    }

    const dotNetObjectReference = hashRoutingState.dotNetObjectReference;
    if (!dotNetObjectReference || typeof dotNetObjectReference.invokeMethodAsync !== "function") {
        return true;
    }

    try {
        const result = await dotNetObjectReference.invokeMethodAsync("NotifyLocationChangingFromJs", pathAbsoluteUri, historyEntryState, interceptedLink);
        return Boolean(result);
    } catch {
        return true;
    }
}

function notifyLocationChanged(pathAbsoluteUri, historyEntryState, interceptedLink) {
    const dotNetObjectReference = hashRoutingState.dotNetObjectReference;
    if (!dotNetObjectReference || typeof dotNetObjectReference.invokeMethodAsync !== "function") {
        return;
    }

    void dotNetObjectReference.invokeMethodAsync("NotifyLocationChangedFromJs", pathAbsoluteUri, historyEntryState, interceptedLink);
}

function commitNavigation(hashAbsoluteUri, replaceHistoryEntry, historyEntryState) {
    if (replaceHistoryEntry) {
        const replacementState = withHistoryMetadata(window.history.state, historyEntryState, hashRoutingState.currentHistoryIndex);
        window.history.replaceState(replacementState, "", hashAbsoluteUri);
    } else {
        hashRoutingState.currentHistoryIndex++;
        const nextState = withHistoryMetadata(window.history.state, historyEntryState, hashRoutingState.currentHistoryIndex);
        window.history.pushState(nextState, "", hashAbsoluteUri);
    }

    hashRoutingState.lastAcceptedHashAbsoluteUri = hashAbsoluteUri;
    hashRoutingState.lastAcceptedHistoryState = historyEntryState;
}

function normalizeOptions(options) {
    const raw = options || {};

    return {
        canonicalizeToHash: raw.canonicalizeToHash !== false,
        hashPrefix: normalizeHashPrefix(raw.hashPrefix),
        interceptInternalLinks: raw.interceptInternalLinks !== false,
    };
}

function normalizeHashPrefix(hashPrefix) {
    const raw = typeof hashPrefix === "string" ? hashPrefix.trim() : "";
    if (!raw) {
        return "/";
    }

    const normalizedPrefix = raw.startsWith("/") ? raw : `/${raw}`;
    if (normalizedPrefix.length > 1 && !normalizedPrefix.endsWith("/")) {
        return `${normalizedPrefix}/`;
    }

    return normalizedPrefix;
}

function normalizeBaseUri(baseUri) {
    const normalizedBaseUri = new URL(baseUri, document.baseURI);

    normalizedBaseUri.search = "";
    normalizedBaseUri.hash = "";

    if (!normalizedBaseUri.pathname.endsWith("/")) {
        normalizedBaseUri.pathname = `${normalizedBaseUri.pathname}/`;
    }

    return normalizedBaseUri.href;
}

function normalizeHistoryState(state) {
    if (typeof state === "string") {
        return state;
    }

    if (state === null || state === undefined) {
        return null;
    }

    return `${state}`;
}

function getHistoryIndex(historyState) {
    if (historyState && typeof historyState === "object" && Number.isFinite(historyState._qhrIndex)) {
        return historyState._qhrIndex;
    }

    return 0;
}

function getHistoryUserState(historyState) {
    if (historyState && typeof historyState === "object" && Object.prototype.hasOwnProperty.call(historyState, "userState")) {
        return normalizeHistoryState(historyState.userState);
    }

    return null;
}

function withHistoryMetadata(existingState, userState, historyIndex) {
    const base = existingState && typeof existingState === "object" ? { ...existingState } : {};
    base.userState = userState;
    base._qhrIndex = historyIndex;

    return base;
}

function toPathAbsoluteUri(rawAbsoluteUri, baseUri, hashPrefix) {
    const absolute = new URL(rawAbsoluteUri, baseUri);
    return toPathAbsoluteUriFromAbsolute(absolute, baseUri, hashPrefix);
}

function toPathAbsoluteUriFromAbsolute(absolute, baseUri, hashPrefix) {
    const hash = absolute.hash || "";

    const routeAndQuery = extractRouteAndQueryFromHash(hash, hashPrefix);
    if (routeAndQuery !== null) {
        const result = new URL(baseUri);
        const queryIndex = routeAndQuery.indexOf("?");
        const routePath = queryIndex >= 0
            ? routeAndQuery.substring(0, queryIndex) || "/"
            : routeAndQuery || "/";
        const routeSearch = queryIndex >= 0
            ? routeAndQuery.substring(queryIndex)
            : "";

        result.pathname = combineBasePathAndRoutePath(result.pathname, routePath);
        result.search = routeSearch;

        result.hash = "";
        return result.href;
    }

    absolute.hash = "";
    return absolute.href;
}

function toHashAbsoluteUri(pathAbsoluteUri, baseUri, hashPrefix) {
    const absolute = new URL(pathAbsoluteUri, baseUri);
    const base = new URL(baseUri);
    const basePathWithoutTrailingSlash = trimTrailingSlash(base.pathname);
    const basePathPrefix = `${basePathWithoutTrailingSlash}/`;

    let routePath;
    if (absolute.pathname === basePathWithoutTrailingSlash) {
        routePath = "/";
    } else if (absolute.pathname.startsWith(basePathPrefix)) {
        routePath = absolute.pathname.slice(basePathWithoutTrailingSlash.length);
    } else {
        routePath = absolute.pathname;
    }

    if (!routePath.startsWith("/")) {
        routePath = `/${routePath}`;
    }

    const relativePath = routePath.length > 1
        ? routePath.slice(1)
        : "";
    const relativePathAndQuery = relativePath + absolute.search;

    base.search = "";
    base.hash = `${hashPrefix}${relativePathAndQuery}`;

    return base.href;
}

function combineBasePathAndRoutePath(basePath, routePath) {
    const normalizedRoutePath = routePath && routePath.startsWith("/")
        ? routePath
        : `/${routePath || ""}`;
    const normalizedBasePath = normalizePathWithTrailingSlash(basePath);

    if (normalizedRoutePath === "/") {
        return normalizedBasePath;
    }

    return `${normalizedBasePath}${normalizedRoutePath.slice(1)}`;
}

function trimTrailingSlash(path) {
    if (path.length > 1 && path.endsWith("/")) {
        return path.slice(0, -1);
    }

    return path;
}

function normalizePathWithTrailingSlash(path) {
    const normalizedPath = path.startsWith("/")
        ? path
        : `/${path}`;

    return normalizedPath.endsWith("/")
        ? normalizedPath
        : `${normalizedPath}/`;
}

function extractRouteAndQueryFromHash(hash, hashPrefix) {
    if (!hash || hash[0] !== "#") {
        return null;
    }

    const value = hash.slice(1);
    if (!value.startsWith(hashPrefix)) {
        return null;
    }

    const remainder = value.slice(hashPrefix.length);
    if (!remainder) {
        return "/";
    }

    if (remainder.startsWith("/")) {
        return remainder;
    }

    return `/${remainder}`;
}

function isCanonicalizableHash(hash, hashPrefix) {
    if (!hash || hash === "#") {
        return true;
    }

    return extractRouteAndQueryFromHash(hash, hashPrefix) !== null;
}

function isWithinBaseUriSpace(absoluteUriOrUrl) {
    try {
        const absolute = absoluteUriOrUrl instanceof URL
            ? absoluteUriOrUrl
            : new URL(absoluteUriOrUrl, hashRoutingState.baseUri);
        if (!sameOrigin(absolute, hashRoutingState.baseProtocol, hashRoutingState.baseHostname, hashRoutingState.basePort)) {
            return false;
        }

        const absoluteHref = absolute.href;
        const hashIndex = absoluteHref.indexOf("#");
        const absoluteWithoutHash = hashIndex < 0
            ? absoluteHref
            : absoluteHref.slice(0, hashIndex);

        return absoluteWithoutHash.startsWith(hashRoutingState.baseUri) || absoluteWithoutHash === hashRoutingState.baseUriWithoutTrailingSlash;
    } catch {
        return false;
    }
}

function sameOrigin(left, protocol, hostname, port) {
    return left.protocol === protocol
        && left.hostname === hostname
        && left.port === port;
}

function sameUri(left, right) {
    const baseUri = hashRoutingState.baseUri || document.baseURI;

    try {
        return new URL(left, baseUri).href === new URL(right, baseUri).href;
    } catch {
        return left === right;
    }
}

function createBrowserNavigationKey(location, historyIndex, historyEntryState) {
    const statePart = historyEntryState === null ? "" : historyEntryState;
    return `${location}|${historyIndex}|${statePart}`;
}
