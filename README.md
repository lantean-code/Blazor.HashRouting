# Blazor.HashRouting

`Blazor.HashRouting` provides hash-fragment routing for browser-hosted Blazor WebAssembly applications.

It is designed for environments where the host can reliably serve only the application root document and cannot deep-link arbitrary route paths on refresh.

## Installation

```bash
dotnet add package Blazor.HashRouting
```

## Usage

Register hash routing during startup:

```csharp
using Blazor.HashRouting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddHashRouting();

await builder.Build().RunAsync();
```

Optional configuration:

```csharp
builder.Services.AddHashRouting(options =>
{
    options.HashPrefix = "/";
    options.CanonicalizeToHash = true;
    options.InterceptInternalLinks = true;
});
```

## URL shape

- Root route: `/#/`
- Nested route: `/#/settings`
- Base-path route: `/proxy/app/#/settings`

## Supported environments

- Blazor WebAssembly in a browser

## Limitations

- Not intended for Blazor Server.
- Not intended for Blazor Hybrid.
- Assumes the host can serve the application root document and static assets, but may not support deep-link route paths.
