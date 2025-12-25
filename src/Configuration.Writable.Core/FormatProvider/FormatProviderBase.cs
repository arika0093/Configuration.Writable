using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// Attempts to deserialize and apply migrations to reach the target type T.
    /// This method handles version detection and migration chain application.
    /// </summary>
    /// <typeparam name="T">The target configuration type.</typeparam>
    /// <param name="jsonElement">The JSON element containing the configuration data.</param>
    /// <param name="options">The writable options configuration containing migration steps.</param>
    /// <param name="deserializeFunc">A function that deserializes a JsonElement to a specific type.</param>
    /// <returns>The configuration object of type T, with all migrations applied if necessary.</returns>
    protected static T LoadConfigurationWithMigration<T>(
        JsonElement jsonElement,
        WritableOptionsConfiguration<T> options,
        Func<JsonElement, Type, object> deserializeFunc
    )
        where T : class, new()
    {
        // If no migrations are registered or T doesn't implement IHasVersion, deserialize directly
        if (options.MigrationSteps.Count == 0 || !typeof(IHasVersion).IsAssignableFrom(typeof(T)))
        {
            return (T)deserializeFunc(jsonElement, typeof(T));
        }

        // Try to read the version property
        int? fileVersion = null;
        if (jsonElement.TryGetProperty("Version", out var versionElement))
        {
            if (versionElement.ValueKind == JsonValueKind.Number)
            {
                fileVersion = versionElement.GetInt32();
            }
        }

        // If no version found in file, try deserializing as oldest type in migration chain
        if (fileVersion == null)
        {
            options.Logger?.Log(
                LogLevel.Debug,
                "No Version property found in configuration file. Attempting to deserialize as target type {TargetType}",
                typeof(T).Name
            );
            return (T)deserializeFunc(jsonElement, typeof(T));
        }

        options.Logger?.Log(
            LogLevel.Debug,
            "Found version {Version} in configuration file",
            fileVersion.Value
        );

        // Find the starting type based on version
        Type? currentType = null;
        var targetVersion = ((IHasVersion)new T()).Version;

        // If file version matches target version, deserialize directly
        if (fileVersion.Value == targetVersion)
        {
            options.Logger?.Log(
                LogLevel.Debug,
                "File version {FileVersion} matches target version, deserializing directly as {TargetType}",
                fileVersion.Value,
                typeof(T).Name
            );
            return (T)deserializeFunc(jsonElement, typeof(T));
        }

        // Build complete migration chain including all types
        var allTypes = new List<Type> { typeof(T) };
        foreach (var step in options.MigrationSteps)
        {
            if (!allTypes.Contains(step.FromType))
                allTypes.Add(step.FromType);
            if (!allTypes.Contains(step.ToType))
                allTypes.Add(step.ToType);
        }

        // Find the type matching the file version
        foreach (var type in allTypes)
        {
            if (typeof(IHasVersion).IsAssignableFrom(type))
            {
                var instance = (IHasVersion)Activator.CreateInstance(type)!;
                if (instance.Version == fileVersion.Value)
                {
                    currentType = type;
                    break;
                }
            }
        }

        if (currentType == null)
        {
            options.Logger?.Log(
                LogLevel.Warning,
                "No type found matching version {Version} in migration chain. Attempting to deserialize as target type {TargetType}",
                fileVersion.Value,
                typeof(T).Name
            );
            return (T)deserializeFunc(jsonElement, typeof(T));
        }

        // Deserialize as the found type
        options.Logger?.Log(
            LogLevel.Debug,
            "Deserializing as version {Version} type {CurrentType}",
            fileVersion.Value,
            currentType.Name
        );

        object current = deserializeFunc(jsonElement, currentType);

        // Apply migrations until we reach type T
        while (currentType != typeof(T))
        {
            var migration = options.MigrationSteps.FirstOrDefault(m => m.FromType == currentType);
            if (migration == null)
            {
                throw new InvalidOperationException(
                    $"No migration found from {currentType.Name} to reach {typeof(T).Name}. "
                        + "Ensure all migration steps are registered in the correct order."
                );
            }

            options.Logger?.Log(
                LogLevel.Information,
                "Applying migration from {FromType} (v{FromVersion}) to {ToType} (v{ToVersion})",
                migration.FromType.Name,
                ((IHasVersion)current).Version,
                migration.ToType.Name,
                ((IHasVersion)Activator.CreateInstance(migration.ToType)!).Version
            );

            current = migration.MigrationFunc(current);
            currentType = migration.ToType;
        }

        return (T)current;
    }
}
