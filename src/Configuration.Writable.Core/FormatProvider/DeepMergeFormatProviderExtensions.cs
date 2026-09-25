using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.FormatProvider;

/// <summary>Provides provider-independent APIs for merging sparse configuration documents.</summary>
public static class DeepMergeFormatProviderExtensions
{
    /// <summary>
    /// Merges documents in precedence order and deserializes the result as <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The options model type.</typeparam>
    /// <param name="formatProvider">The format provider used to parse and deserialize the documents.</param>
    /// <param name="documents">Documents ordered from lowest to highest precedence.</param>
    /// <param name="sectionNameParts">The section path to merge, or <see langword="null"/> for the document root.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="ArgumentNullException">The provider or document collection is null.</exception>
    /// <exception cref="NotSupportedException">The provider does not implement <see cref="IDeepMergeableFormatProvider"/>.</exception>
    public static ValueTask<T> MergeConfigurationsAsync<T>(
        this IWritableFormatProvider formatProvider,
        IReadOnlyList<ReadOnlyMemory<byte>> documents,
        IReadOnlyList<string>? sectionNameParts = null,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        if (formatProvider is null)
            throw new ArgumentNullException(nameof(formatProvider));
        if (documents is null)
            throw new ArgumentNullException(nameof(documents));
        if (formatProvider is not IDeepMergeableFormatProvider mergeableFormatProvider)
            throw new NotSupportedException(
                $"Format provider {formatProvider.GetType().Name} does not support deep merging."
            );

        return mergeableFormatProvider.MergeConfigurationsAsync<T>(
            documents,
            sectionNameParts ?? Array.Empty<string>(),
            cancellationToken
        );
    }
}
