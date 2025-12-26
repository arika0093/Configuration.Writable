using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.FormatProvider;

/// <summary>
/// Writable configuration provider base class.
/// </summary>
public abstract class FormatProviderBase : IFormatProvider
{
    /// <inheritdoc />
    public abstract string FileExtension { get; }

    /// <inheritdoc />
    public abstract T LoadConfiguration<T>(WritableOptionsConfiguration<T> options)
        where T : class, new();

    /// <inheritdoc />
    public abstract T LoadConfiguration<T>(Stream stream, WritableOptionsConfiguration<T> options)
        where T : class, new();

    /// <inheritdoc />
    public abstract object LoadConfiguration(Type type, Stream stream, List<string> sectionNameParts);

    /// <inheritdoc />
    public abstract Task SaveAsync<T>(
        T config,
        WritableOptionsConfiguration<T> options,
        CancellationToken cancellationToken = default
    )
        where T : class, new();

    /// <summary>
    /// Creates a nested dictionary structure from a section name that supports ':' and '__' as separators.
    /// For example, "SectionA:SectionB" or "SectionA__SectionB" will create { "SectionA": { "SectionB": value } }.
    /// </summary>
    /// <param name="parts">The list of section name parts split by the separators.</param>
    /// <param name="value">The value to place at the deepest level.</param>
    /// <returns>A nested dictionary representing the section hierarchy, or the original value if no separators are found.</returns>
    protected static object CreateNestedSection(List<string> parts, object value)
    {
        if (parts.Count <= 0)
        {
            // No separators found, return a simple dictionary
            return value;
        }

        // Build nested structure from the inside out
        object current = value;
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            current = new Dictionary<string, object> { [parts[i]] = current };
        }

        return current;
    }
}
