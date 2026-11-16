// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.BackOffice.Export;

/// <summary>
/// Formats the tracked URL export as a CSV that Excel opens by double-click: UTF-8 with a byte order mark, a semicolon
/// as separator, <c>\r\n</c> line ends and dates Excel reads as dates. Pure: it only builds strings.
/// </summary>
internal static class TrackedUrlCsvWriter
{
    private const char Separator = ';';
    private const string LineEnd = "\r\n";
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];
    private static readonly SearchValues<char> QuoteTriggers = SearchValues.Create(";\"\r\n");

    /// <summary>
    /// Gets the UTF-8 byte order mark, written once at the start of the file: Excel reads a CSV as UTF-8 only with it.
    /// </summary>
    internal static ReadOnlyMemory<byte> Preamble => Bom;

    /// <summary>
    /// Gets the header row, with its line end. The texts are English: the server has no backoffice language.
    /// </summary>
    internal static string HeaderRow { get; } =
        string.Join(Separator, "Url", "Relative path", "Domain", "Culture", "Type", "First seen (UTC)", "Last seen (UTC)", "Hits") + LineEnd;

    /// <summary>
    /// Formats one tracked URL as a row, with its line end.
    /// </summary>
    /// <param name="row">The tracked URL.</param>
    /// <returns>The row.</returns>
    internal static string FormatRow(TrackedUrl row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var line = new StringBuilder();
        line.Append(EscapeField(row.DisplayUrl)).Append(Separator)
            .Append(EscapeField(row.RelativePath)).Append(Separator)
            .Append(EscapeField(row.Domain)).Append(Separator)
            .Append(EscapeField(row.Culture)).Append(Separator)
            .Append(row.IsFile ? "File" : "Page").Append(Separator)
            .Append(FormatTimestamp(row.FirstSeen)).Append(Separator)
            .Append(FormatTimestamp(row.LastSeen)).Append(Separator)
            .Append(row.HitCount.ToString(CultureInfo.InvariantCulture))
            .Append(LineEnd);
        return line.ToString();
    }

    /// <summary>
    /// Escapes a text field. A field that starts with a character Excel reads as the start of a formula gets a leading
    /// apostrophe (tracked URLs, domains and cultures come from requests, so they are attacker-controlled); a field
    /// that contains the separator, a quote or a line break is wrapped in quotes with its quotes doubled.
    /// </summary>
    /// <param name="value">The field.</param>
    /// <returns>The escaped field.</returns>
    internal static string EscapeField(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        if (value.AsSpan().IndexOfAny(QuoteTriggers) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    // Stored as UTC; providers such as SQLite return the value without its kind, so the kind is set, never converted.
    private static string FormatTimestamp(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString(DateFormat, CultureInfo.InvariantCulture);
}
