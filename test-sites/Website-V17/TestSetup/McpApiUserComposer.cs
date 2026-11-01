// Test-site only. Not part of the Umbraco.Community.PageNotFoundTracker package.
//
// Ensures the Umbraco MCP API user for the running launch profile exists, so a deleted
// scenario database is rebuilt with working MCP credentials. The client id and secret come
// from the profile's environment variables (UmbracoMcp__ClientId / UmbracoMcp__ClientSecret
// in Properties/launchSettings.json) and must match the server entry in the repo-root
// .mcp.json. All values are TEST-ONLY and only used against https://localhost.
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Security.OperationStatus;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Website_V17.TestSetup;

/// <summary>
/// Registers <see cref="McpApiUserStartupHandler"/>.
/// </summary>
public sealed class McpApiUserComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
        => builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, McpApiUserStartupHandler>();
}

/// <summary>
/// Creates the MCP API user (member of Administrators, throwaway test databases only) and its
/// client credentials when they are missing. Only runs in the Development environment.
/// </summary>
public sealed class McpApiUserStartupHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string ApiUserEmail = "mcp@example.com";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IRuntimeState _runtimeState;
    private readonly IConfiguration _configuration;
    private readonly ILogger<McpApiUserStartupHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpApiUserStartupHandler"/> class.
    /// </summary>
    public McpApiUserStartupHandler(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment hostEnvironment,
        IRuntimeState runtimeState,
        IConfiguration configuration,
        ILogger<McpApiUserStartupHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _hostEnvironment = hostEnvironment;
        _runtimeState = runtimeState;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        string? clientId = _configuration["UmbracoMcp:ClientId"];
        string? clientSecret = _configuration["UmbracoMcp:ClientSecret"];

        if (!_hostEnvironment.IsDevelopment()
            || _runtimeState.Level != RuntimeLevel.Run
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret))
        {
            return;
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        IBackOfficeUserClientCredentialsManager credentialsManager =
            scope.ServiceProvider.GetRequiredService<IBackOfficeUserClientCredentialsManager>();

        if (await userService.FindByClientIdAsync(clientId) is not null)
        {
            return;
        }

        IUser? apiUser = userService.GetByEmail(ApiUserEmail);
        if (apiUser is null)
        {
            var model = new UserCreateModel
            {
                Email = ApiUserEmail,
                UserName = ApiUserEmail,
                Name = "Umbraco MCP (test-only)",
                Kind = UserKind.Api,
                UserGroupKeys = new HashSet<Guid> { Constants.Security.AdminGroupKey },
            };

            Attempt<UserCreationResult, UserOperationStatus> created =
                await userService.CreateAsync(Constants.Security.SuperUserKey, model, approveUser: true);
            if (!created.Success || created.Result.CreatedUser is null)
            {
                _logger.LogWarning("Could not create the MCP API user: {Status}", created.Status);
                return;
            }

            apiUser = created.Result.CreatedUser;
        }

        Attempt<BackOfficeUserClientCredentialsOperationStatus> saved =
            await credentialsManager.SaveAsync(apiUser.Key, clientId, clientSecret);
        if (saved.Success)
        {
            _logger.LogInformation("Created MCP API user {Email} with client id {ClientId} (test-only)", ApiUserEmail, clientId);
        }
        else
        {
            _logger.LogWarning("Could not add MCP client credentials {ClientId}: {Status}", clientId, saved.Result);
        }
    }
}
