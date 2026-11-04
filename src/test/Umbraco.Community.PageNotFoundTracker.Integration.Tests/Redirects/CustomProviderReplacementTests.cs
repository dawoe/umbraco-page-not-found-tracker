// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Tests.Integration.ManagementApi;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;
using Umbraco.Community.PageNotFoundTracker.Core;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Redirects;

/// <summary>
/// Proves the implementer path for replacing the redirect provider: a composer marked
/// <c>[ComposeAfter(typeof(PageNotFoundTrackerComposer))]</c> registers its own <see cref="IRedirectProvider"/> with
/// <c>AddUnique</c> and it replaces the default in the real composition (no test service override).
/// </summary>
internal sealed class CustomProviderReplacementTests : ManagementApiTest<GetTrackedUrlsController>
{
    private const string ListUrl = "/umbraco/pagenotfoundtracker/api/v1/tracked-urls";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    protected override Expression<Func<GetTrackedUrlsController, object>> MethodSelector { get; set; } =
        x => x.GetTrackedUrls(CancellationToken.None, 0, 20, TrackedUrlSortColumn.Hits, Direction.Descending, null, null, false, null, false);

    /// <summary>
    /// Enables the replacement composer for this fixture only (composers in the test assembly run in every fixture).
    /// </summary>
    [OneTimeSetUp]
    public void EnableComposer()
    {
        CustomRedirectProvider.Enabled = true;
        CustomRedirectProvider.Reset();
    }

    /// <summary>
    /// Disables the replacement composer again.
    /// </summary>
    [OneTimeTearDown]
    public void DisableComposer() => CustomRedirectProvider.Enabled = false;

    /// <summary>
    /// The custom provider replaces the default: it handles file rows, is the one called, and the record is removed.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Custom_provider_registered_in_a_composer_replaces_the_default()
    {
        CustomRedirectProvider.Reset();
        await this.SeedAsync();
        await this.AuthenticateClientAsync(this.Client, "replace-editor@umbraco.com", UserPassword, Umbraco.Cms.Core.Constants.Security.EditorGroupKey);
        var factory = this.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();
        int fileId = await IdOfAsync(factory, "/replace.pdf");

        using HttpResponseMessage list = await this.Client.GetAsync(new Uri(ListUrl + "?take=10", UriKind.Relative));
        string listBody = await list.Content.ReadAsStringAsync();
        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK), listBody);
        JsonNode fileItem = JsonNode.Parse(listBody)!["items"]!.AsArray().Single(i => i!["isFile"]!.GetValue<bool>())!;

        using HttpResponseMessage response = await this.PostAsync(fileId, "{\"payload\":{\"target\":\"/new\"}}");
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        await using PageNotFoundTrackerDbContext check = await factory.CreateDbContextAsync();
        Assert.Multiple(() =>
        {
            Assert.That(this.GetRequiredService<IRedirectProvider>(), Is.TypeOf<CustomRedirectProvider>());
            Assert.That(fileItem["canCreateRedirect"]!.GetValue<bool>(), Is.True, "The custom provider handles file rows.");
            Assert.That(JsonNode.Parse(body)!["recordRemoved"]!.GetValue<bool>(), Is.True);
            Assert.That(check.TrackedUrls.Any(u => u.Id == fileId), Is.False);
            Assert.That(CustomRedirectProvider.Calls, Has.Count.EqualTo(1));
            Assert.That(CustomRedirectProvider.Calls[0].Url, Is.EqualTo("/replace.pdf"));
            Assert.That(CustomRedirectProvider.Calls[0].Payload, Is.EqualTo("{\"target\":\"/new\"}"));
        });
    }

    /// <summary>
    /// A failure message of the custom provider surfaces as a 422 with that message and the record stays.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Custom_provider_failure_message_surfaces_as_422_and_keeps_the_record()
    {
        CustomRedirectProvider.Reset();
        await this.SeedAsync();
        await this.AuthenticateClientAsync(this.Client, "replace-fail-editor@umbraco.com", UserPassword, Umbraco.Cms.Core.Constants.Security.EditorGroupKey);
        var factory = this.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();
        int fileId = await IdOfAsync(factory, "/replace.pdf");

        using HttpResponseMessage response = await this.PostAsync(fileId, "{\"payload\":{\"fail\":\"Custom provider says no\"}}");
        string body = await response.Content.ReadAsStringAsync();

        await using PageNotFoundTrackerDbContext check = await factory.CreateDbContextAsync();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity), body);
            Assert.That(body, Does.Contain("Custom provider says no"));
            Assert.That(check.TrackedUrls.Any(u => u.Id == fileId), Is.True);
        });
    }

    /// <inheritdoc />
    protected override void CustomMvcSetup(IMvcBuilder mvcBuilder) =>
        mvcBuilder.AddApplicationPart(typeof(TrackedUrlsControllerBase).Assembly);

    private static async Task<int> IdOfAsync(IDbContextFactory<PageNotFoundTrackerDbContext> factory, string url)
    {
        await using PageNotFoundTrackerDbContext db = await factory.CreateDbContextAsync();
        return await db.TrackedUrls.Where(u => u.DisplayUrl == url).Select(u => u.Id).SingleAsync();
    }

    private async Task<HttpResponseMessage> PostAsync(int id, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await this.Client.PostAsync(new Uri($"{ListUrl}/{id}/redirect", UriKind.Relative), content);
    }

    private async Task SeedAsync()
    {
        using (var timeout = new CancellationTokenSource(ReadyTimeout))
        {
            bool ready = await this.GetRequiredService<ITrackingPersistenceState>().WaitUntilReadyAsync(timeout.Token);
            Assert.That(ready, Is.True, "The tracking persistence must become available.");
        }

        var factory = this.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();
        await using PageNotFoundTrackerDbContext db = await factory.CreateDbContextAsync();
        await db.TrackedUrls.ExecuteDeleteAsync();
        db.TrackedUrls.Add(new TrackedUrl
        {
            Url = "/replace.pdf",
            UrlHash = new string('c', 64),
            RelativePath = "/replace.pdf",
            DisplayUrl = "/replace.pdf",
            Domain = string.Empty,
            Culture = string.Empty,
            IsFile = true,
            HitCount = 1,
            FirstSeen = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
            LastSeen = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The implementer's provider: handles every row, records its calls and fails when the payload asks for it.
    /// </summary>
    internal sealed class CustomRedirectProvider : IRedirectProvider
    {
        private static readonly List<(string Url, string? Payload)> CallList = new();

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="CustomRedirectComposer"/> registers the provider.
        /// </summary>
        internal static bool Enabled { get; set; }

        /// <summary>
        /// Gets the calls received since the last <see cref="Reset"/>.
        /// </summary>
        internal static IReadOnlyList<(string Url, string? Payload)> Calls
        {
            get
            {
                lock (CallList)
                {
                    return CallList.ToList();
                }
            }
        }

        /// <summary>
        /// Clears the recorded calls.
        /// </summary>
        internal static void Reset()
        {
            lock (CallList)
            {
                CallList.Clear();
            }
        }

        /// <inheritdoc />
        public bool CanHandle(TrackedUrl url) => true;

        /// <inheritdoc />
        public Task<RedirectResult> CreateAsync(TrackedUrl url, JsonElement? payload, CancellationToken cancellationToken)
        {
            string? raw = payload?.GetRawText();
            lock (CallList)
            {
                CallList.Add((url.DisplayUrl, raw));
            }

            if (payload is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty("fail", out JsonElement message))
            {
                return Task.FromResult(RedirectResult.Failure(message.GetString()!));
            }

            return Task.FromResult(RedirectResult.Success());
        }
    }

    /// <summary>
    /// A consumer-style composer replacing the provider. It does nothing unless the fixture enabled it.
    /// </summary>
    [ComposeAfter(typeof(PageNotFoundTrackerComposer))]
    internal sealed class CustomRedirectComposer : IComposer
    {
        /// <inheritdoc />
        public void Compose(IUmbracoBuilder builder)
        {
            if (CustomRedirectProvider.Enabled)
            {
                builder.Services.AddUnique<IRedirectProvider, CustomRedirectProvider>();
            }
        }
    }
}
