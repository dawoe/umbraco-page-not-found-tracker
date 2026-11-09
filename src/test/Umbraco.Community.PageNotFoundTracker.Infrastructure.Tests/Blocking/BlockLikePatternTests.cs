// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Data.Sqlite;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Blocking;

/// <summary>
/// Unit tests for <see cref="BlockLikePattern"/>.
/// </summary>
[TestFixture]
internal sealed class BlockLikePatternTests
{
    /// <summary>
    /// Stars become percent signs and LIKE specials are escaped.
    /// </summary>
    [Test]
    public void ToLike_EscapesAndTranslates()
    {
        var (like, exact) = BlockLikePattern.ToLike("/a_b%c[d]\\e*");

        Assert.That(like, Is.EqualTo("/a\\_b\\%c\\[d]\\\\e%"));
        Assert.That(exact, Is.Null);
    }

    /// <summary>
    /// A pattern ending in a slash and a star also gives the exact path before it.
    /// </summary>
    [Test]
    public void ToLike_TrailingSlashStar_GivesExact()
    {
        var (like, exact) = BlockLikePattern.ToLike("/x/*");

        Assert.That(like, Is.EqualTo("/x/%"));
        Assert.That(exact, Is.EqualTo("/x"));
    }

    /// <summary>
    /// The SQL translation selects exactly the paths the in-memory matcher accepts.
    /// </summary>
    [Test]
    public void Like_and_matcher_agree()
    {
        string[] paths =
        [
            "/", "/x", "/x/", "/x/y", "/xy", "/a_b", "/aXb", "/a%b", "/aZZb", "/a[b]", "/a[c]", "/a\\b", "/a\\\\b",
            "/wp-admin", "/wp-admin/a/b", "/y/wp-admin/a", "/file.php", "/d/file.php", "/file.php5", "/abcde", "/ace", "/ae",
        ];
        string[] patterns =
        [
            "/x/*", "/x*", "/a_b", "/a%b", "/a[b]", "/a\\b", "/a*b", "/*.php", "/wp-admin/*", "/a*c*e", "/a_*", "/a%*", "/a[*", "/a\\*",
        ];

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE p (path TEXT NOT NULL)";
            create.ExecuteNonQuery();
        }

        foreach (string path in paths)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO p (path) VALUES ($path)";
            insert.Parameters.AddWithValue("$path", path);
            insert.ExecuteNonQuery();
        }

        foreach (string pattern in patterns)
        {
            var (like, exact) = BlockLikePattern.ToLike(pattern);
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT path FROM p WHERE path LIKE $like ESCAPE '\\' OR path = $exact";
            select.Parameters.AddWithValue("$like", like);
            select.Parameters.AddWithValue("$exact", (object?)exact ?? DBNull.Value);
            var fromSql = new List<string>();
            using (var reader = select.ExecuteReader())
            {
                while (reader.Read())
                {
                    fromSql.Add(reader.GetString(0));
                }
            }

            var compiled = CompiledBlockPattern.Compile(pattern);
            var fromMatcher = paths.Where(compiled.IsMatch).OrderBy(p => p, StringComparer.Ordinal).ToList();

            Assert.That(fromSql.OrderBy(p => p, StringComparer.Ordinal).ToList(), Is.EqualTo(fromMatcher), "pattern " + pattern);
        }
    }
}
