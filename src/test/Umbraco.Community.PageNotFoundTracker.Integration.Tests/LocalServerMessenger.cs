// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.Serialization;
using Umbraco.Cms.Infrastructure.Sync;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests;

/// <summary>
/// A server messenger that delivers cache refresher messages locally only. The integration test base registers a no-op
/// messenger, so without this one publishing content does not update the navigation, URL and published caches that
/// front-end routing reads. Umbraco's own integration tests use the same approach.
/// </summary>
internal sealed class LocalServerMessenger : ServerMessengerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalServerMessenger"/> class.
    /// </summary>
    public LocalServerMessenger()
        : base(false, new SystemTextJsonSerializer(new DefaultJsonSerializerEncoderFactory()))
    {
    }

    /// <inheritdoc />
    public override void SendMessages()
    {
    }

    /// <inheritdoc />
    public override void Sync()
    {
    }

    /// <inheritdoc />
    protected override void DeliverRemote(ICacheRefresher refresher, MessageType messageType, IEnumerable<object>? ids = null, string? json = null)
    {
    }
}
