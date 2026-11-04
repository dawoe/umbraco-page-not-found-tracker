// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Tests.Integration.ManagementApi;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.ManagementApi;

/// <summary>
/// Tests the Management API of the dashboard on the test server (SQLite) with real backoffice users and tokens: the
/// access policy (200 for an allowed group, 403 outside the configured groups, 401 anonymous), the access endpoint and
/// the operation IDs in the package's OpenAPI document. The settings are the defaults, so <c>admin</c> and
/// <c>editor</c> are allowed.
/// </summary>
internal sealed class TrackedUrlsApiTests : ManagementApiTest<GetTrackedUrlsController>
{
    private const string ApiBase = "/umbraco/pagenotfoundtracker/api/v1";

    private const string ListUrl = ApiBase + "/tracked-urls";

    private const string FilterOptionsUrl = ApiBase + "/tracked-urls/filter-options";

    private const string AccessUrl = ApiBase + "/access";

    private const string OpenApiDocumentUrl = "/umbraco/swagger/page-not-found-tracker-management-api/swagger.json";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    protected override Expression<Func<GetTrackedUrlsController, object>> MethodSelector { get; set; } =
        x => x.GetTrackedUrls(CancellationToken.None, 0, 20, TrackedUrlSortColumn.Hits, Direction.Descending, null, null, false, null, false);

    /// <summary>
    /// A user in an allowed group (editor) gets the tracked rows, most hits first, with UTC timestamps.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task List_AllowedGroup_Returns200WithRows()
    {
        await this.SeedAsync();
        await this.AuthenticateClientAsync(this.Client, "list-editor@umbraco.com", UserPassword, Constants.Security.EditorGroupKey);

        using HttpResponseMessage response = await this.Client.GetAsync(new Uri(ListUrl + "?take=10", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        JsonNode json = JsonNode.Parse(body)!;
        JsonArray items = json["items"]!.AsArray();
        Assert.Multiple(() =>
        {
            Assert.That(json["total"]!.GetValue<long>(), Is.EqualTo(2));
            Assert.That(items.Select(i => i!["url"]!.GetValue<string>()), Is.EqualTo(new[] { "/Popular", "/rare.pdf" }));
            Assert.That(items[0]!["relativePath"]!.GetValue<string>(), Is.EqualTo("/popular"));
            Assert.That(items[0]!["hitCount"]!.GetValue<long>(), Is.EqualTo(5));
            Assert.That(items[1]!["isFile"]!.GetValue<bool>(), Is.True);
            Assert.That(items[0]!["firstSeen"]!.GetValue<string>(), Is.EqualTo("2026-11-01T08:00:00Z"));
        });
    }

    /// <summary>
    /// A user whose only group is not in the setting gets 403 from the list.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task List_GroupOutsideSetting_Returns403()
    {
        await this.AuthenticateWithSectionsAsync("pnftListOutside", Constants.Applications.Content);

        using HttpResponseMessage response = await this.Client.GetAsync(new Uri(ListUrl, UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    /// <summary>
    /// An anonymous caller gets 401 from the list.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task List_Anonymous_Returns401()
    {
        using HttpResponseMessage response = await this.Client.GetAsync(new Uri(ListUrl, UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    /// <summary>
    /// An invalid page size is a 400 for an allowed user.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task List_TakeAbove100_Returns400()
    {
        await this.AuthenticateClientAsync(this.Client, "take-editor@umbraco.com", UserPassword, Constants.Security.EditorGroupKey);

        using HttpResponseMessage response = await this.Client.GetAsync(new Uri(ListUrl + "?take=101", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    /// <summary>
    /// The filter options answer 200 for an allowed group.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FilterOptions_AllowedGroup_Returns200()
    {
        await this.SeedAsync();
        await this.AuthenticateClientAsync(this.Client, "options-editor@umbraco.com", UserPassword, Constants.Security.EditorGroupKey);

        using HttpResponseMessage response = await this.Client.GetAsync(new Uri(FilterOptionsUrl, UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        JsonNode json = JsonNode.Parse(body)!;
        Assert.Multiple(() =>
        {
            Assert.That(json["domains"]!.AsArray().Select(d => d!.GetValue<string>()), Is.EqualTo(new[] { "localhost:44402/sitea" }));
            Assert.That(json["cultures"]!.AsArray().Select(c => c!.GetValue<string>()), Is.EqualTo(new[] { "en-US" }));
            Assert.That(json["hasEmptyDomain"]!.GetValue<bool>(), Is.True);
            Assert.That(json["hasEmptyCulture"]!.GetValue<bool>(), Is.True);
        });
    }

    /// <summary>
    /// A user whose only group is not in the setting gets 403 from the filter options.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FilterOptions_GroupOutsideSetting_Returns403()
    {
        await this.AuthenticateWithSectionsAsync("pnftOptionsOutside", Constants.Applications.Content);

        using HttpResponseMessage response = await this.Client.GetAsync(new Uri(FilterOptionsUrl, UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    /// <summary>
    /// The access endpoint answers every signed-in backoffice user: allowed for an editor, not for a user outside the
    /// configured groups; anonymous callers get 401.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Access_ReturnsAllowedTrueAndFalseByGroup()
    {
        await this.AuthenticateClientAsync(this.Client, "access-editor@umbraco.com", UserPassword, Constants.Security.EditorGroupKey);
        bool editorAllowed = await this.GetAllowedAsync(this.Client);

        using HttpClient anonymous = this.CreateClient();
        using HttpResponseMessage anonymousResponse = await anonymous.GetAsync(new Uri(AccessUrl, UriKind.Relative));

        using HttpClient outside = this.CreateClient();
        await this.AuthenticateInNewGroupAsync(outside, "pnftAccessOutside");
        bool outsideAllowed = await this.GetAllowedAsync(outside);

        Assert.Multiple(() =>
        {
            Assert.That(editorAllowed, Is.True);
            Assert.That(outsideAllowed, Is.False);
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    /// <summary>
    /// The list flags rows the active provider can redirect: a page row yes, a file row no.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task List_CanCreateRedirect_FalseForFileRow()
    {
        await this.SeedAsync();
        await this.AuthenticateClientAsync(this.Client, "flag-editor@umbraco.com", UserPassword, Constants.Security.EditorGroupKey);

        using HttpResponseMessage response = await this.Client.GetAsync(new Uri(ListUrl + "?take=10", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        JsonArray items = JsonNode.Parse(body)!["items"]!.AsArray();
        Assert.That(items.Select(i => (i!["url"]!.GetValue<string>(), i["canCreateRedirect"]!.GetValue<bool>())), Is.EqualTo(new[] { ("/Popular", true), ("/rare.pdf", false) }));
    }

    /// <summary>
    /// An anonymous caller gets 401 from the create redirect endpoint.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreateRedirect_Anonymous_Returns401()
    {
        using HttpResponseMessage response = await this.PostRedirectAsync(this.Client, 1);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    /// <summary>
    /// A user whose only group is not in the setting gets 403 from the create redirect endpoint.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreateRedirect_GroupOutsideSetting_Returns403()
    {
        await this.AuthenticateWithSectionsAsync("pnftRedirectOutside", Constants.Applications.Content);

        using HttpResponseMessage response = await this.PostRedirectAsync(this.Client, 1);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    /// <summary>
    /// An unknown id is a 404.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreateRedirect_UnknownId_Returns404()
    {
        await this.SeedAsync();
        await this.AuthenticateClientAsync(this.Client, "redirect404-editor@umbraco.com", UserPassword, Constants.Security.EditorGroupKey);

        using HttpResponseMessage response = await this.PostRedirectAsync(this.Client, 999999);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    /// <summary>
    /// With a provider that accepts the row, the endpoint answers 200 and the tracked URL is gone; a file row is a 409.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreateRedirect_WithFakeProvider_Returns200AndRemovesRow_FileIs409()
    {
        await this.SeedAsync();
        await this.AuthenticateClientAsync(this.Client, "redirect200-editor@umbraco.com", UserPassword, Constants.Security.EditorGroupKey);
        int pageId;
        int fileId;
        var factory = this.GetRequiredService<IDbContextFactory<PageNotFoundTrackerDbContext>>();
        await using (PageNotFoundTrackerDbContext db = await factory.CreateDbContextAsync())
        {
            pageId = await db.TrackedUrls.Where(u => !u.IsFile).Select(u => u.Id).SingleAsync();
            fileId = await db.TrackedUrls.Where(u => u.IsFile).Select(u => u.Id).SingleAsync();
        }

        using HttpResponseMessage file = await this.PostRedirectAsync(this.Client, fileId);
        using HttpResponseMessage page = await this.PostRedirectAsync(this.Client, pageId);
        string body = await page.Content.ReadAsStringAsync();

        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        JsonNode json = JsonNode.Parse(body)!;
        await using PageNotFoundTrackerDbContext check = await factory.CreateDbContextAsync();
        Assert.Multiple(() =>
        {
            Assert.That(file.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(json["recordRemoved"]!.GetValue<bool>(), Is.True);
            Assert.That(check.TrackedUrls.Any(u => u.Id == pageId), Is.False);
            Assert.That(check.TrackedUrls.Any(u => u.Id == fileId), Is.True);
        });
    }

    /// <summary>
    /// The package's OpenAPI document holds the operations with stable IDs.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenApiDocument_ContainsTheOperationIds()
    {
        using HttpResponseMessage response = await this.Client.GetAsync(new Uri(OpenApiDocumentUrl, UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        JsonNode document = JsonNode.Parse(body)!;
        var operations = document["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject().Select(operation => (Path: path.Key, Operation: operation.Value!)))
            .ToList();

        Assert.That(
            operations.Select(o => (o.Path, Id: o.Operation["operationId"]!.GetValue<string>())),
            Is.EquivalentTo(new[]
            {
                (ListUrl, "GetTrackedUrls"),
                (FilterOptionsUrl, "GetTrackedUrlFilterOptions"),
                (AccessUrl, "GetDashboardAccess"),
                (ListUrl + "/{id}/redirect", "CreateTrackedUrlRedirect"),
            }));
        Assert.Multiple(() =>
        {
            Assert.That(document["components"]?["securitySchemes"]?["Backoffice-User"], Is.Not.Null, "The backoffice security scheme.");
            foreach ((string path, JsonNode operation) in operations)
            {
                Assert.That(operation["tags"]!.AsArray().Select(t => t!.GetValue<string>()), Is.EqualTo(new[] { "Tracked URL" }), path);
                Assert.That(operation["security"]?.ToJsonString(), Does.Contain("Backoffice-User"), path);
            }
        });
    }

    /// <inheritdoc />
    protected override void CustomMvcSetup(IMvcBuilder mvcBuilder)
    {
        // The test server's entry assembly is Umbraco's test assembly, so MVC does not discover the package's controllers
        // by itself (a real site gets an ApplicationPart attribute for every referenced assembly with controllers).
        mvcBuilder.AddApplicationPart(typeof(TrackedUrlsControllerBase).Assembly);
    }

    /// <inheritdoc />
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // A provider that accepts page rows, so the endpoint can be exercised without a published target page.
        services.AddSingleton<IRedirectProvider, FakeRedirectProvider>();
    }

    private async Task<HttpResponseMessage> PostRedirectAsync(HttpClient client, int id)
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri($"{ListUrl}/{id}/redirect", UriKind.Relative), content);
    }

    private HttpClient CreateClient() =>
        this.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost/", UriKind.Absolute),
        });

    /// <summary>
    /// Creates a user group with access to the Content section and signs a new user of only that group in on the client.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="groupAlias">The alias of the new group; unique per fixture.</param>
    private async Task AuthenticateInNewGroupAsync(HttpClient client, string groupAlias)
    {
        var group = new UserGroup(this.GetRequiredService<IShortStringHelper>())
        {
            Name = groupAlias,
            Alias = groupAlias,
            Icon = "icon-users",
            HasAccessToAllLanguages = true,
        };
        group.AddAllowedSection(Constants.Applications.Content);
        var created = await this.GetRequiredService<IUserGroupService>().CreateAsync(group, Constants.Security.SuperUserKey);
        Assert.That(created.Success, Is.True, $"Could not create the user group: {created.Status}");

        string email = $"{groupAlias}@umbraco.com";
        await this.AuthenticateClientAsync(
            client,
            async userService =>
            {
                var result = await userService.CreateAsync(
                    Constants.Security.SuperUserKey,
                    new UserCreateModel { Email = email, Name = groupAlias, UserName = email, UserGroupKeys = new HashSet<Guid> { created.Result.Key } },
                    true);
                return (result.Result.CreatedUser!, UserPassword);
            },
            $"{email}:{groupAlias}");
    }

    private async Task<bool> GetAllowedAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(AccessUrl, UriKind.Relative));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        return JsonNode.Parse(body)!["allowed"]!.GetValue<bool>();
    }

    /// <summary>
    /// Waits for the tracking tables and replaces the rows with two known ones.
    /// </summary>
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
        db.TrackedUrls.AddRange(
            new TrackedUrl
            {
                Url = "/sitea/popular",
                UrlHash = new string('a', 64),
                RelativePath = "/popular",
                DisplayUrl = "/Popular",
                Domain = "localhost:44402/sitea",
                Culture = "en-US",
                HitCount = 5,
                FirstSeen = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc),
                LastSeen = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc),
            },
            new TrackedUrl
            {
                Url = "/rare.pdf",
                UrlHash = new string('b', 64),
                RelativePath = "/rare.pdf",
                DisplayUrl = "/rare.pdf",
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
    /// A provider that accepts every row that is not a file and creates redirects without doing anything.
    /// </summary>
    private sealed class FakeRedirectProvider : IRedirectProvider
    {
        /// <inheritdoc />
        public bool CanHandle(TrackedUrl url) => !url.IsFile;

        /// <inheritdoc />
        public Task<RedirectResult> CreateAsync(TrackedUrl url, System.Text.Json.JsonElement? payload, CancellationToken cancellationToken) =>
            Task.FromResult(RedirectResult.Success());
    }
}
