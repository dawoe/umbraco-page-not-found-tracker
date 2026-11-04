// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Models;

/// <summary>
/// The body of a create redirect request.
/// </summary>
public sealed class CreateRedirectRequestModel
{
    /// <summary>
    /// Gets provider specific data, or <c>null</c>. For the default provider: <c>{ "targetKey": "&lt;guid&gt;" }</c>.
    /// </summary>
    public JsonElement? Payload { get; init; }
}
