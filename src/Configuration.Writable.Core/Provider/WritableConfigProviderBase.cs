using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration provider base class.
/// </summary>
public abstract class WritableConfigProviderBase : IWritableConfigProvider
{
    /// <inheritdoc />
    public abstract string FileExtension { get; }

    /// <inheritdoc />
    public virtual IFileWriter FileWriter { get; set; } = new CommonFileWriter();

    /// <inheritdoc />
    public abstract T LoadConfiguration<T>(WritableConfigurationOptions<T> options)
        where T : class;

    /// <inheritdoc />
    public abstract T LoadConfiguration<T>(Stream stream, WritableConfigurationOptions<T> options)
        where T : class;

    /// <inheritdoc />
    public abstract ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class;

    /// <inheritdoc />
    public virtual async Task SaveAsync<T>(
        T config,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var contents = GetSaveContents(config, options);
        await FileWriter
            .SaveToFileAsync(options.ConfigFilePath, contents, cancellationToken, options.Logger)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a nested dictionary structure from a section name that supports ':' and '__' as separators.
    /// For example, "SectionA:SectionB" or "SectionA__SectionB" will create { "SectionA": { "SectionB": value } }.
    /// </summary>
    /// <param name="sectionName">The section name with potential separators.</param>
    /// <param name="value">The value to place at the deepest level.</param>
    /// <returns>A nested dictionary representing the section hierarchy, or the original value if no separators are found.</returns>
    protected static object CreateNestedSection(string sectionName, object value)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return value;
        }

        // Split by ':' or '__' separators
        var parts = GetSplitedSections(sectionName);

        if (parts.Length <= 1)
        {
            // No separators found, return a simple dictionary
            return new Dictionary<string, object> { [sectionName] = value };
        }

        // Build nested structure from the inside out
        object current = value;
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            current = new Dictionary<string, object> { [parts[i]] = current };
        }

        return current;
    }

    /// <summary>
    /// Splits the specified section name into its constituent parts using colon (:) and double underscore (__) as
    /// delimiters.
    /// </summary>
    /// <param name="sectionName">The section name to split. Cannot be null.</param>
    /// <returns>An array of strings containing the individual sections. The array does not include empty entries.</returns>
    protected static string[] GetSplitedSections(string sectionName) =>
        sectionName.Split([":", "__"], StringSplitOptions.RemoveEmptyEntries);
}
