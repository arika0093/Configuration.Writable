using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.Migration;

/// <summary>
/// Extension methods for migration loading.
/// </summary>
internal static class MigrationLoaderExtension
{
    /// <summary>
    /// Attempts to deserialize and apply migrations to reach the target type T.
    /// This method handles version detection and migration chain application.
    /// </summary>
    /// <typeparam name="T">The target configuration type.</typeparam>
    internal static T LoadWithMigration<T>(
        this FormatProvider.IFormatProvider formatProvider,
        WritableOptionsConfiguration<T> options
    )
        where T : class, new()
    {
        var config = formatProvider.LoadConfiguration<T>(options); 
        // If loaded config doesn't implement IHasVersion (shouldn't happen but be safe), return it
        if (config is not IHasVersion versionedConfig)
        {
            return config;
        }

        // Get version from loaded config
        var fileVersion = versionedConfig.Version;

        // Get target version
        var targetVersion = VersionCache.GetVersion(typeof(T))
            ?? throw new InvalidOperationException(
                $"Target type {typeof(T).Name} does not implement IHasVersion correctly."
            );

        // If file version matches target version, return directly
        if (fileVersion == targetVersion)
        {
            return config;
        }

        // Build complete migration chain including all types
        HashSet<Type> allTypes = [typeof(T)];
        foreach (var step in options.MigrationSteps)
        {
            allTypes.Add(step.FromType);
            allTypes.Add(step.ToType);
        }

        // Find the type matching the file version
        Type? currentType = allTypes.FirstOrDefault(t => VersionCache.GetVersion(t) == fileVersion);
        if (currentType == null)
        {
            throw new InvalidOperationException(
                $"No type found matching version {fileVersion} in migration chain."
            );
        }

        // Deserialize as the found type
        var current = formatProvider.LoadConfiguration(
            currentType,
            options
        );

        // Apply migrations until we reach type T
        while (currentType != typeof(T))
        {
            var migration = options.MigrationSteps.FirstOrDefault(m => m.FromType == currentType);
            if (migration == null)
            {
                throw new InvalidOperationException($"""
                    No migration found from {currentType.Name} to reach {typeof(T).Name}.
                    Ensure all migration steps are registered in the correct order.
                    """
                );
            }

            var fromVersion = VersionCache.GetVersion(migration.FromType) ?? 0;
            var toVersion = VersionCache.GetVersion(migration.ToType) ?? 0;

            options.Logger?.LogInformation(
                "Applying migration from {FromType} (v{FromVersion}) to {ToType} (v{ToVersion})",
                migration.FromType.Name,
                fromVersion,
                migration.ToType.Name,
                toVersion
            );

            current = migration.Migrate(current);
            currentType = migration.ToType;
        }

        return (T)current;
    }
}
