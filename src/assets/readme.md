# Umbraco Page Not Found Tracker

An Umbraco package that records requests that end in a 404 and, in later versions, lets editors
turn tracked URLs into redirects or block them.

This first version contains only the package foundation (a placeholder backoffice dashboard). Tracking,
redirects and blocking are planned for upcoming versions.

Supports **Umbraco 17** only.

## Installation

```bash
dotnet add package Umbraco.Community.PageNotFoundTracker
```

Call `UsePageNotFoundTracker()` in `Program.cs`, after `BootUmbracoAsync()` and before `UseUmbraco()`:

```csharp
using Umbraco.Community.PageNotFoundTracker.Extensions;

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UsePageNotFoundTracker();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
```

## Links

- Source, issues and documentation: https://github.com/dawoe/umbraco-page-not-found-tracker
- License: MIT
