using System;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.Configure;

/// <summary>Extensions for configuring fallback serialization formats.</summary>
public static class WritableOptionsConfigBuilderFallbackExtensions
{
    /// <summary>
    /// Registers an additional format provider that may be used to load an existing configuration
    /// when the canonical file for <see cref="WritableOptionsConfigBuilder.FormatProvider"/> does not exist.
    /// A successfully loaded fallback configuration is promoted to the canonical format after schema migration.
    /// </summary>
    /// <param name="builder">The options configuration builder.</param>
    /// <param name="formatProvider">The fallback format provider.</param>
    public static void AddFallbackFormatProvider(
        this WritableOptionsConfigBuilder builder,
        IWritableFormatProvider formatProvider
    )
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (formatProvider is null)
        {
            throw new ArgumentNullException(nameof(formatProvider));
        }

        if (builder.FormatProvider is not FallbackFormatProvider fallbackProvider)
        {
            fallbackProvider = new FallbackFormatProvider(builder.FormatProvider);
            builder.FormatProvider = fallbackProvider;
        }

        fallbackProvider.AddFallback(formatProvider);
    }
}
