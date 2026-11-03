// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Security;
using Umbraco.Community.PageNotFoundTracker.Core.Dashboard;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Tests;

/// <summary>
/// Unit tests for <see cref="DashboardAccessHandler"/>.
/// </summary>
[TestFixture]
internal sealed class DashboardAccessHandlerTests
{
    private Mock<IDashboardAccess> access = null!;

    /// <summary>
    /// Creates an access rule that allows only the <c>editor</c> group.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.access = new Mock<IDashboardAccess>();
        this.access
            .Setup(a => a.IsAllowed(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(aliases => aliases.Contains("editor"));
    }

    /// <summary>
    /// A signed-in user with an allowed group role claim passes.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Handle_UserWithAllowedGroupRole_Succeeds()
    {
        AuthorizationHandlerContext context = CreateContext(Authenticated("writer", "editor"));

        await new DashboardAccessHandler(this.access.Object).HandleAsync(context);

        Assert.That(context.HasSucceeded, Is.True);
    }

    /// <summary>
    /// A signed-in user without an allowed group role claim does not pass.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Handle_UserWithoutAllowedGroupRole_Fails()
    {
        AuthorizationHandlerContext context = CreateContext(Authenticated("writer"));

        await new DashboardAccessHandler(this.access.Object).HandleAsync(context);

        Assert.That(context.HasSucceeded, Is.False);
    }

    /// <summary>
    /// An anonymous user does not pass, even when the rule would allow an empty group list.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Handle_Anonymous_Fails()
    {
        this.access.Setup(a => a.IsAllowed(It.IsAny<IEnumerable<string>>())).Returns(true);
        AuthorizationHandlerContext context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await new DashboardAccessHandler(this.access.Object).HandleAsync(context);

        Assert.That(context.HasSucceeded, Is.False);
    }

    /// <summary>
    /// The group aliases are the role claims of the user.
    /// </summary>
    [Test]
    public void GetUserGroupAliases_ReturnsRoleClaims()
    {
        ClaimsPrincipal user = Authenticated("admin", "editor");

        Assert.That(DashboardAccessHandler.GetUserGroupAliases(user), Is.EquivalentTo(new[] { "admin", "editor" }));
    }

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user) =>
        new([new DashboardAccessRequirement()], user, resource: null);

    private static ClaimsPrincipal Authenticated(params string[] groupAliases)
    {
        var identity = new ClaimsIdentity(
            groupAliases.Select(alias => new Claim(ClaimTypes.Role, alias)).Append(new Claim(ClaimTypes.Name, "user@example.com")),
            authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }
}
