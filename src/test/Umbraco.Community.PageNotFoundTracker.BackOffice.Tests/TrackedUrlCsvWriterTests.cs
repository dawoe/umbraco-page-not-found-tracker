// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using Umbraco.Community.PageNotFoundTracker.BackOffice.Export;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Tests;

/// <summary>
/// Tests <see cref="TrackedUrlCsvWriter"/>: the Excel-friendly CSV format of the export.
/// </summary>
[TestFixture]
internal sealed class TrackedUrlCsvWriterTests
{
    /// <summary>
    /// Tests that the header row has the eight columns separated by semicolons.
    /// </summary>
    [Test]
    public void HeaderRow_IsTheEightColumnsSeparatedBySemicolon()
    {
        Assert.That(TrackedUrlCsvWriter.HeaderRow, Is.EqualTo("Url;Relative path;Domain;Culture;Type;First seen (UTC);Last seen (UTC);Hits\r\n"));
    }

    /// <summary>
    /// Tests that the preamble is the UTF-8 byte order mark.
    /// </summary>
    [Test]
    public void Preamble_IsTheUtf8Bom()
    {
        Assert.That(TrackedUrlCsvWriter.Preamble.ToArray(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    /// <summary>
    /// Tests the row of a page.
    /// </summary>
    [Test]
    public void FormatRow_PageRow()
    {
        TrackedUrl row = Row();

        Assert.That(
            TrackedUrlCsvWriter.FormatRow(row),
            Is.EqualTo("/Old-Page;/old-page;localhost:44402/sitea;en-US;Page;2026-11-01 08:00:00;2026-11-02 09:30:05;1234\r\n"));
    }

    /// <summary>
    /// Tests that a file row has the type File.
    /// </summary>
    [Test]
    public void FormatRow_FileRow_IsTypeFile()
    {
        TrackedUrl row = Row();
        row.IsFile = true;

        Assert.That(TrackedUrlCsvWriter.FormatRow(row), Does.Contain(";File;"));
    }

    /// <summary>
    /// Tests that an empty domain and culture are empty cells.
    /// </summary>
    [Test]
    public void FormatRow_EmptyDomainAndCulture_AreEmptyCells()
    {
        TrackedUrl row = Row();
        row.Domain = string.Empty;
        row.Culture = string.Empty;

        Assert.That(TrackedUrlCsvWriter.FormatRow(row), Does.StartWith("/Old-Page;/old-page;;;Page;"));
    }

    /// <summary>
    /// Tests that a field with a semicolon, a quote, a carriage return or a line feed is quoted, with quotes doubled.
    /// </summary>
    /// <param name="value">The field.</param>
    /// <param name="expected">The escaped field.</param>
    [TestCase("a;b", "\"a;b\"")]
    [TestCase("a\"b", "\"a\"\"b\"")]
    [TestCase("a\nb", "\"a\nb\"")]
    [TestCase("a\rb", "\"a\rb\"")]
    public void EscapeField_Quotes_WhenItContainsSemicolonQuoteCrOrLf(string value, string expected)
    {
        Assert.That(TrackedUrlCsvWriter.EscapeField(value), Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests that a plain value is left unchanged.
    /// </summary>
    [Test]
    public void EscapeField_PlainValue_IsUnchanged()
    {
        Assert.That(TrackedUrlCsvWriter.EscapeField("/plain-page"), Is.EqualTo("/plain-page"));
    }

    /// <summary>
    /// Tests that a field that Excel would read as a formula gets a leading apostrophe.
    /// </summary>
    /// <param name="value">The field.</param>
    /// <param name="expected">The escaped field.</param>
    [TestCase("=1+1", "'=1+1")]
    [TestCase("+1", "'+1")]
    [TestCase("-1", "'-1")]
    [TestCase("@SUM(A1)", "'@SUM(A1)")]
    [TestCase("\tx", "'\tx")]
    [TestCase("\rx", "\"'\rx\"")]
    public void EscapeField_FormulaStart_GetsLeadingApostrophe(string value, string expected)
    {
        Assert.That(TrackedUrlCsvWriter.EscapeField(value), Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests that every text column is protected, not only the URL.
    /// </summary>
    [Test]
    public void FormatRow_FormulaInEveryTextColumn_IsPrefixed()
    {
        TrackedUrl row = Row();
        row.DisplayUrl = "=cmd|' /C calc'!A0";
        row.RelativePath = "+1";
        row.Domain = "-1";
        row.Culture = "@x";

        string[] cells = TrackedUrlCsvWriter.FormatRow(row).TrimEnd('\r', '\n').Split(';');

        // The URL contains no semicolon, so it stays unquoted; the four text cells all start with an apostrophe.
        Assert.That(cells[0], Does.StartWith("'="));
        Assert.That(cells[1], Is.EqualTo("'+1"));
        Assert.That(cells[2], Is.EqualTo("'-1"));
        Assert.That(cells[3], Is.EqualTo("'@x"));
    }

    /// <summary>
    /// Tests that the formats do not depend on the current culture.
    /// </summary>
    [Test]
    public void FormatRow_UnderDutchCulture_StillUsesInvariantFormats()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
            TrackedUrl row = Row();
            row.HitCount = 1234567;

            string line = TrackedUrlCsvWriter.FormatRow(row);

            Assert.That(line, Does.EndWith(";2026-11-01 08:00:00;2026-11-02 09:30:05;1234567\r\n"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// Tests that non-ASCII characters are kept as they are.
    /// </summary>
    [Test]
    public void FormatRow_NonAscii_IsKeptAsIs()
    {
        TrackedUrl row = Row();
        row.DisplayUrl = "/café-ü";

        Assert.That(TrackedUrlCsvWriter.FormatRow(row), Does.StartWith("/café-ü;"));
    }

    /// <summary>
    /// Tests that a timestamp without a kind (as SQLite returns it) is written as the UTC value it is.
    /// </summary>
    [Test]
    public void FormatRow_TimestampKindUnspecified_IsWrittenAsUtc()
    {
        TrackedUrl row = Row();
        row.FirstSeen = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Unspecified);
        row.LastSeen = new DateTime(2026, 11, 2, 9, 30, 5, DateTimeKind.Local);

        string line = TrackedUrlCsvWriter.FormatRow(row);

        // The local-kind value is a wall-clock value read from the database, never converted.
        Assert.That(line, Does.Contain(";2026-11-01 08:00:00;2026-11-02 09:30:05;"));
    }

    private static TrackedUrl Row() =>
        new()
        {
            Url = "/old-page",
            UrlHash = "hash",
            DisplayUrl = "/Old-Page",
            RelativePath = "/old-page",
            Domain = "localhost:44402/sitea",
            Culture = "en-US",
            IsFile = false,
            HitCount = 1234,
            FirstSeen = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc),
            LastSeen = new DateTime(2026, 11, 2, 9, 30, 5, DateTimeKind.Utc),
        };
}
