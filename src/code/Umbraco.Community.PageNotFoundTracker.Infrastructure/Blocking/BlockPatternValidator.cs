// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

/// <summary>
/// Validates a normalised block pattern and its status code.
/// </summary>
internal static class BlockPatternValidator
{
    /// <summary>
    /// Validates a pattern and status code.
    /// </summary>
    /// <param name="normalisedPattern">The normalised pattern.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <returns><c>null</c> when valid, otherwise the message to show.</returns>
    public static string? Validate(string normalisedPattern, int statusCode)
    {
        if (statusCode < BlockingConstants.MinStatusCode || statusCode > BlockingConstants.MaxStatusCode)
        {
            return $"The status code must be between {BlockingConstants.MinStatusCode} and {BlockingConstants.MaxStatusCode}.";
        }

        if (normalisedPattern.Length > BlockingConstants.MaxPatternLength)
        {
            return $"The pattern can be at most {BlockingConstants.MaxPatternLength} characters.";
        }

        if (normalisedPattern.All(c => c is '/' or '*'))
        {
            return "The pattern must contain at least one character besides '/' and '*'.";
        }

        var compiled = CompiledBlockPattern.Compile(normalisedPattern);
        string literalPrefix = normalisedPattern[..compiled.LiteralPrefixLength];
        foreach (string owned in UmbracoOwnedPaths.Prefixes)
        {
            string prefix = owned.ToLowerInvariant();
            if (compiled.IsMatch(prefix)
                || literalPrefix == prefix
                || literalPrefix.StartsWith(prefix + "/", StringComparison.Ordinal))
            {
                return $"The pattern covers {owned}, which belongs to Umbraco and cannot be blocked.";
            }
        }

        return null;
    }
}
