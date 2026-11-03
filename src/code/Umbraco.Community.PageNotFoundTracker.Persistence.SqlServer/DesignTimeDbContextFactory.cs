// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer;

/// <summary>
/// Creates the context for <c>dotnet ef</c> with the SQL Server provider, so the tools need no running site or server.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PageNotFoundTrackerDbContext>
{
    /// <inheritdoc />
    public PageNotFoundTrackerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PageNotFoundTrackerDbContext>();
        new SqlServerTrackingDatabaseProvider().ConfigureDbContext(options, "Server=localhost;Database=PageNotFoundTrackerDesignTime;Integrated Security=true;TrustServerCertificate=true");
        return new PageNotFoundTrackerDbContext(options.Options);
    }
}
