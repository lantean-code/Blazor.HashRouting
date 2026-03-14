# Changelog

## Unreleased

- Replaced hosted-service startup with explicit `InitializeHashRoutingAsync()` host initialization.
- Canonicalized internal anchor `href` values to hash-route URLs for browser-managed navigation.
- Clarified that relative anchor URLs such as `./foo` resolve relative to the application base URI.

## 1.0.0-rc.1

- First release candidate of `Blazor.HashRouting`.
- Added reusable hash-fragment routing for browser-hosted Blazor WebAssembly applications.
- Added canonical `/#/...` routing, internal link interception, back/forward handling, and navigation lock integration.
- Added standalone NuGet packaging, static web assets, and independent CI/release automation.
- Multi-targeted the package for `net9.0` and `net10.0`.
