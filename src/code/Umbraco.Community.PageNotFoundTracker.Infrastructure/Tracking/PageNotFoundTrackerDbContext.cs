// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tracking;

/// <summary>
/// The EF Core context of the package. Public because the provider projects hold its migrations.
/// </summary>
/// <param name="options">The options, configured by the selected <see cref="ITrackingDatabaseProvider"/>.</param>
public class PageNotFoundTrackerDbContext(DbContextOptions<PageNotFoundTrackerDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the tracked URLs.
    /// </summary>
    public DbSet<TrackedUrl> TrackedUrls => this.Set<TrackedUrl>();

    /// <summary>
    /// Gets the block rules.
    /// </summary>
    public DbSet<BlockRule> BlockRules => this.Set<BlockRule>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<TrackedUrl>(entity =>
        {
            entity.ToTable(TrackingConstants.TableName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.UrlHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.RelativePath).IsRequired().HasMaxLength(800);
            entity.Property(e => e.DisplayUrl).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Domain).IsRequired().HasMaxLength(255).HasDefaultValue(string.Empty);
            entity.Property(e => e.Culture).IsRequired().HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.HasIndex(e => new { e.UrlHash, e.Domain, e.Culture }).IsUnique();
            entity.HasIndex(e => e.RelativePath);
            entity.HasIndex(e => e.LastSeen);
        });

        modelBuilder.Entity<BlockRule>(entity =>
        {
            entity.ToTable(BlockingConstants.TableName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Pattern).IsRequired().HasMaxLength(BlockingConstants.MaxPatternLength);
            entity.Property(e => e.PatternHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.DisplayPattern).IsRequired().HasMaxLength(BlockingConstants.MaxPatternLength);
            entity.Property(e => e.Domain).IsRequired().HasMaxLength(255).HasDefaultValue(string.Empty);
            entity.Property(e => e.Culture).IsRequired().HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.HasIndex(e => new { e.PatternHash, e.Domain, e.Culture }).IsUnique();
        });
    }
}
