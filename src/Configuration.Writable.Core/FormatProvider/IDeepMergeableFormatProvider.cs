using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.FormatProvider;

/// <summary>
/// A format provider that can merge sparse documents before deserializing them.
/// </summary>
/// <remarks>
/// Omitted properties retain earlier values. Explicit null property values replace earlier values,
/// nested objects merge recursively, scalar values use the last supplied value, and arrays replace by default.
/// </remarks>
public interface IDeepMergeableFormatProvider : IWritableFormatProvider
{
    /// <summary>
    /// Merges documents in order and deserializes the resulting configuration.
    /// </summary>
    /// <typeparam name="T">The options model type.</typeparam>
    /// <param name="documents">Documents ordered from lowest to highest precedence.</param>
    /// <param name="sectionNameParts">The section path to merge, or an empty list for the document root.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask<T> MergeConfigurationsAsync<T>(
        IReadOnlyList<ReadOnlyMemory<byte>> documents,
        IReadOnlyList<string> sectionNameParts,
        CancellationToken cancellationToken = default
    )
        where T : class, new();
}
