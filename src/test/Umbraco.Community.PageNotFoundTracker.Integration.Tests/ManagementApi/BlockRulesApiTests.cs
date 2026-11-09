// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Tests.Integration.ManagementApi;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Controllers;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.ManagementApi;

/// <summary>
/// Tests the block rule endpoints on the test server (SQLite) with real backoffice users and tokens: authorisation,
/// validation messages, create, update by creating again, list, delete and the scope options.
/// </summary>
internal sealed class BlockRulesApiTests : ManagementApiTest<GetBlockRulesController>
{
    private const string RulesUrl = "/umbraco/pagenotfoundtracker/api/v1/block-rules";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    protected override Expression<Func<GetBlockRulesController, object>> MethodSelector { get; set; } =
        x => x.GetBlockRules(CancellationToken.None, 0, 20, BlockRuleSortColumn.Created, Direction.Descending, null, null, false, null, false);

    /// <summary>
    /// An anonymous caller gets 401.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Anonymous_Returns401()
    {
        using HttpResponseMessage list = await this.Client.GetAsync(new Uri(RulesUrl, UriKind.Relative));
        using HttpResponseMessage create = await this.PostAsync("/x*", 410);

        Assert.Multiple(() =>
        {
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    /// <summary>
    /// A user whose only group is not in the setting gets 403.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GroupOutsideSetting_Returns403()
    {
        await this.AuthenticateWithSectionsAsync("pnftRulesOutside", Constants.Applications.Content);

        using HttpResponseMessage list = await this.Client.GetAsync(new Uri(RulesUrl, UriKind.Relative));
        using HttpResponseMessage create = await this.PostAsync("/x*", 410);
        using HttpResponseMessage options = await this.Client.GetAsync(new Uri(RulesUrl + "/scope-options", UriKind.Relative));

        Assert.Multiple(() =>
        {
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(options.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    /// <summary>
    /// An anonymous caller gets 401 from delete, update and the scope options.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Anonymous_DeleteUpdateAndScopeOptions_Return401()
    {
        using HttpResponseMessage delete = await this.Client.DeleteAsync(new Uri(RulesUrl + "/1", UriKind.Relative));
        using HttpResponseMessage update = await this.PutAsync(1, "/x*", 410);
        using HttpResponseMessage options = await this.Client.GetAsync(new Uri(RulesUrl + "/scope-options", UriKind.Relative));

        Assert.Multiple(() =>
        {
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(options.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    /// <summary>
    /// A user outside the configured groups gets 403 from delete and update.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GroupOutsideSetting_Delete_Returns403()
    {
        await this.AuthenticateWithSectionsAsync("pnftRulesOutsideDelete", Constants.Applications.Content);

        using HttpResponseMessage delete = await this.Client.DeleteAsync(new Uri(RulesUrl + "/1", UriKind.Relative));
        using HttpResponseMessage update = await this.PutAsync(1, "/x*", 410);

        Assert.Multiple(() =>
        {
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    /// <summary>
    /// A malformed or empty body is a 400, never a 500.
    /// </summary>
    /// <param name="body">The raw body.</param>
    /// <returns>A task.</returns>
    [TestCase("not json {")]
    [TestCase("")]
    public async Task Create_MalformedOrEmptyBody_Returns400(string body)
    {
        await this.SignInAsync("rules-badbody@umbraco.com");
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await this.Client.PostAsync(new Uri(RulesUrl, UriKind.Relative), content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Patterns the service rejects are a 400 with the message in <c>detail</c>.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>A task.</returns>
    [TestCase("*")]
    [TestCase("/um*")]
    [TestCase("/*")]
    public async Task Create_TooBroadPattern_Returns400WithDetail(string pattern)
    {
        await this.SignInAsync("rules-400@umbraco.com");

        using HttpResponseMessage response = await this.PostAsync(pattern, 410);
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), body);
        Assert.That(JsonNode.Parse(body)!["detail"]!.GetValue<string>(), Is.Not.Empty, body);
    }

    /// <summary>
    /// A status code outside 400 to 499 is a 400 with a readable detail.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_StatusCodeOutOfRange_Returns400WithDetail()
    {
        await this.SignInAsync("rules-status@umbraco.com");

        using HttpResponseMessage response = await this.PostAsync("/wp-admin*", 200);
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), body);
        Assert.That(JsonNode.Parse(body)!["detail"]!.GetValue<string>(), Is.Not.Empty, body);
    }

    /// <summary>
    /// A blank pattern is a 400.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_BlankPattern_Returns400()
    {
        await this.SignInAsync("rules-blank@umbraco.com");

        using HttpResponseMessage response = await this.PostAsync("  ", 410);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    /// <summary>
    /// An unknown sort column is a 400.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task List_UnknownOrderBy_Returns400()
    {
        await this.SignInAsync("rules-orderby@umbraco.com");

        using HttpResponseMessage response = await this.Client.GetAsync(new Uri(RulesUrl + "?orderBy=Bogus", UriKind.Relative));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    /// <summary>
    /// A created rule is listed; creating it again updates the status code instead of adding a second rule; the list
    /// carries UTC timestamps and the entered pattern.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Create_ThenList_ShowsRule_CreateAgainUpdatesStatus()
    {
        await this.SignInAsync("rules-create@umbraco.com");

        using HttpResponseMessage first = await this.PostAsync("/WP-Admin*", 410);
        using HttpResponseMessage second = await this.PostAsync("/WP-Admin*", 404);
        using HttpResponseMessage list = await this.Client.GetAsync(new Uri(RulesUrl + "?search=wp-admin", UriKind.Relative));
        string body = await list.Content.ReadAsStringAsync();

        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK), await first.Content.ReadAsStringAsync());
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK), await second.Content.ReadAsStringAsync());
        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        JsonNode json = JsonNode.Parse(body)!;
        JsonNode item = json["items"]!.AsArray().Single()!;
        JsonNode firstJson = JsonNode.Parse(await first.Content.ReadAsStringAsync())!;
        Assert.Multiple(() =>
        {
            Assert.That(json["total"]!.GetValue<long>(), Is.EqualTo(1));
            Assert.That(item["pattern"]!.GetValue<string>(), Is.EqualTo("/WP-Admin*"));
            Assert.That(item["statusCode"]!.GetValue<int>(), Is.EqualTo(404));
            Assert.That(item["created"]!.GetValue<string>(), Does.EndWith("Z"));
            Assert.That(firstJson["recordsRemoved"], Is.Not.Null);
        });
    }

    /// <summary>
    /// Updating a rule changes it in place: the list shows the new values under the same id and the old pattern is gone.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_ChangesTheRule_ThenList_ShowsNewValues()
    {
        await this.SignInAsync("rules-update@umbraco.com");
        using HttpResponseMessage created = await this.PostAsync("/to-edit*", 410);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.OK), await created.Content.ReadAsStringAsync());
        int id = await this.FindIdAsync("to-edit");

        using HttpResponseMessage update = await this.PutAsync(id, "/Edited-Rule/*", 403);
        string updateBody = await update.Content.ReadAsStringAsync();
        using HttpResponseMessage edited = await this.Client.GetAsync(new Uri(RulesUrl + "?search=edited-rule", UriKind.Relative));
        using HttpResponseMessage old = await this.Client.GetAsync(new Uri(RulesUrl + "?search=to-edit", UriKind.Relative));

        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK), updateBody);
        JsonNode editedJson = JsonNode.Parse(await edited.Content.ReadAsStringAsync())!;
        JsonNode oldJson = JsonNode.Parse(await old.Content.ReadAsStringAsync())!;
        JsonNode item = editedJson["items"]!.AsArray().Single()!;
        Assert.Multiple(() =>
        {
            Assert.That(JsonNode.Parse(updateBody)!["recordsRemoved"], Is.Not.Null);
            Assert.That(item["id"]!.GetValue<int>(), Is.EqualTo(id));
            Assert.That(item["pattern"]!.GetValue<string>(), Is.EqualTo("/Edited-Rule/*"));
            Assert.That(item["statusCode"]!.GetValue<int>(), Is.EqualTo(403));
            Assert.That(oldJson["total"]!.GetValue<long>(), Is.EqualTo(0));
        });
    }

    /// <summary>
    /// Updating an unknown rule is a 404 with a detail.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_UnknownId_Returns404()
    {
        await this.SignInAsync("rules-update404@umbraco.com");

        using HttpResponseMessage response = await this.PutAsync(999999, "/x", 410);
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), body);
        Assert.That(JsonNode.Parse(body)!["detail"]!.GetValue<string>(), Is.Not.Empty, body);
    }

    /// <summary>
    /// Changing a rule into the identity of another rule is a 409 with the message as detail.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_IntoExistingRule_Returns409WithDetail()
    {
        await this.SignInAsync("rules-update409@umbraco.com");
        using HttpResponseMessage a = await this.PostAsync("/conflict-a", 410);
        using HttpResponseMessage b = await this.PostAsync("/conflict-b", 410);
        Assert.That(a.StatusCode, Is.EqualTo(HttpStatusCode.OK), await a.Content.ReadAsStringAsync());
        Assert.That(b.StatusCode, Is.EqualTo(HttpStatusCode.OK), await b.Content.ReadAsStringAsync());
        int idB = await this.FindIdAsync("conflict-b");

        using HttpResponseMessage response = await this.PutAsync(idB, "/conflict-a", 410);
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), body);
        Assert.That(JsonNode.Parse(body)!["detail"]!.GetValue<string>(), Is.EqualTo("A rule with this pattern, domain and culture already exists."), body);
    }

    /// <summary>
    /// A pattern the service rejects is a 400 with the message in <c>detail</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Update_TooBroadPattern_Returns400WithDetail()
    {
        await this.SignInAsync("rules-update400@umbraco.com");
        using HttpResponseMessage created = await this.PostAsync("/to-break", 410);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.OK), await created.Content.ReadAsStringAsync());
        int id = await this.FindIdAsync("to-break");

        using HttpResponseMessage response = await this.PutAsync(id, "/*", 410);
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), body);
        Assert.That(JsonNode.Parse(body)!["detail"]!.GetValue<string>(), Is.Not.Empty, body);
    }

    /// <summary>
    /// Deleting a rule answers 200, deleting it again 404.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Delete_ExistingThenAgain_Returns200Then404()
    {
        await this.SignInAsync("rules-delete@umbraco.com");
        using HttpResponseMessage created = await this.PostAsync("/to-delete*", 410);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.OK), await created.Content.ReadAsStringAsync());
        using HttpResponseMessage list = await this.Client.GetAsync(new Uri(RulesUrl + "?search=to-delete", UriKind.Relative));
        int id = JsonNode.Parse(await list.Content.ReadAsStringAsync())!["items"]!.AsArray().Single()!["id"]!.GetValue<int>();

        using HttpResponseMessage first = await this.Client.DeleteAsync(new Uri($"{RulesUrl}/{id}", UriKind.Relative));
        using HttpResponseMessage second = await this.Client.DeleteAsync(new Uri($"{RulesUrl}/{id}", UriKind.Relative));

        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    /// <summary>
    /// The scope options list the configured domain and the default language.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScopeOptions_IncludeConfiguredDomain()
    {
        await this.SignInAsync("rules-scope@umbraco.com");
        var contentType = new ContentType(this.GetRequiredService<IShortStringHelper>(), -1)
        {
            Alias = "pnftScopeHome",
            Name = "Scope home",
            AllowedAsRoot = true,
        };
        var typeResult = await this.GetRequiredService<IContentTypeService>().CreateAsync(contentType, Constants.Security.SuperUserKey);
        Assert.That(typeResult.Success, Is.True, $"Creating the document type failed: {typeResult.Result}.");
        var home = new Content("Home", -1, contentType);
        this.GetRequiredService<IContentService>().Save(home);
        var domains = await this.GetRequiredService<IDomainService>().UpdateDomainsAsync(
            home.Key,
            new DomainsUpdateModel { Domains = [new DomainModel { DomainName = "WWW.Scope-Test.local", IsoCode = "en-US" }] });
        Assert.That(domains.Success, Is.True, $"Assigning the domain failed: {domains.Status}.");

        using HttpResponseMessage response = await this.Client.GetAsync(new Uri(RulesUrl + "/scope-options", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        JsonNode json = JsonNode.Parse(body)!;
        Assert.Multiple(() =>
        {
            Assert.That(json["domains"]!.AsArray().Select(d => d!.GetValue<string>()), Does.Contain("www.scope-test.local"));
            Assert.That(json["cultures"]!.AsArray().Select(c => c!.GetValue<string>()), Does.Contain("en-US"));
        });
    }

    /// <inheritdoc />
    protected override void CustomMvcSetup(IMvcBuilder mvcBuilder)
    {
        // See TrackedUrlsApiTests: the test server does not discover the package's controllers by itself.
        mvcBuilder.AddApplicationPart(typeof(TrackedUrlsControllerBase).Assembly);
    }

    private async Task SignInAsync(string email)
    {
        using (var timeout = new CancellationTokenSource(ReadyTimeout))
        {
            bool ready = await this.GetRequiredService<ITrackingPersistenceState>().WaitUntilReadyAsync(timeout.Token);
            Assert.That(ready, Is.True, "The tracking persistence must become available.");
        }

        await this.AuthenticateClientAsync(this.Client, email, UserPassword, Constants.Security.EditorGroupKey);
    }

    private async Task<int> FindIdAsync(string search)
    {
        using HttpResponseMessage list = await this.Client.GetAsync(new Uri(RulesUrl + "?search=" + search, UriKind.Relative));
        return JsonNode.Parse(await list.Content.ReadAsStringAsync())!["items"]!.AsArray().Single()!["id"]!.GetValue<int>();
    }

    private async Task<HttpResponseMessage> PutAsync(int id, string pattern, int statusCode)
    {
        string json = new JsonObject { ["pattern"] = pattern, ["statusCode"] = statusCode, ["domain"] = string.Empty, ["culture"] = string.Empty }.ToJsonString();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await this.Client.PutAsync(new Uri($"{RulesUrl}/{id}", UriKind.Relative), content);
    }

    private async Task<HttpResponseMessage> PostAsync(string pattern, int statusCode)
    {
        string json = new JsonObject { ["pattern"] = pattern, ["statusCode"] = statusCode, ["domain"] = string.Empty, ["culture"] = string.Empty }.ToJsonString();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await this.Client.PostAsync(new Uri(RulesUrl, UriKind.Relative), content);
    }
}
