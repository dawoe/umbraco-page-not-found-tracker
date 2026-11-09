// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Retention;

/// <summary>
/// The outcome of one clean-up batch.
/// </summary>
/// <param name="Selected">
/// How many rows were due when the batch was selected. A full selection means more rows may be due, even when fewer were
/// deleted (a row seen again or removed by a redirect or block in between).
/// </param>
/// <param name="Deleted">How many rows were deleted.</param>
internal readonly record struct CleanupBatch(int Selected, int Deleted);
