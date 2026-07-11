using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Configuration.Writable.FormatProvider;
using Microsoft.Extensions.Logging;
using ZLogger;

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
        // If the target type is not versioned, simply load it directly.
        var targetVersion = VersionCache.GetVersion(typeof(T));
        if (targetVersion is null)
        {
            return (T)formatProvider.LoadConfiguration(typeof(T), options);
        }

        // Try to read the version declared in the file without binding to a specific model.
        // A null result means the file does not declare a version field.
        var fileVersion = (formatProvider as FormatProviderBase)?.TryGetFileVersion(options);

        // When the file has no declared version, check for a migration from an unversioned type.
        if (fileVersion is null)
        {
            var fromNoneStep = options.MigrationSteps.FirstOrDefault(s =>
                VersionCache.GetVersion(s.FromType) is null
            );

            if (fromNoneStep is null)
            {
                // No migration from an unversioned type is registered; load as the target type.
                return (T)formatProvider.LoadConfiguration(typeof(T), options);
            }

            // Start from the unversioned type and apply the migration chain.
            return ApplyMigrationChain<T>(formatProvider, options, fromNoneStep.FromType);
        }

        // The file declares a version. If it already matches the target, load directly.
        if (fileVersion == targetVersion)
        {
            return (T)formatProvider.LoadConfiguration(typeof(T), options);
        }

        // Find the type matching the declared file version.
        HashSet<Type> allTypes = [typeof(T)];
        foreach (var step in options.MigrationSteps)
        {
            allTypes.Add(step.FromType);
            allTypes.Add(step.ToType);
        }

        var currentType = allTypes.FirstOrDefault(t => VersionCache.GetVersion(t) == fileVersion);
        if (currentType is null)
        {
            throw new InvalidOperationException(
                $"No type found matching version {fileVersion} in migration chain."
            );
        }

        return ApplyMigrationChain<T>(formatProvider, options, currentType);
    }

    private static T ApplyMigrationChain<T>(
        FormatProvider.IFormatProvider formatProvider,
        WritableOptionsConfiguration<T> options,
        Type startingType
    )
        where T : class, new()
    {
        var currentType = startingType;
        var current = formatProvider.LoadConfiguration(currentType, options);

        while (currentType != typeof(T))
        {
            var migration = options.MigrationSteps.FirstOrDefault(m => m.FromType == currentType);
            if (migration is null)
            {
                throw new InvalidOperationException(
                    $"""
                    No migration found from {currentType.Name} to reach {typeof(T).Name}.
                    Ensure all migration steps are registered in the correct order.
                    """
                );
            }

            var fromVersion = VersionCache.GetVersion(migration.FromType) ?? 0;
            var toVersion = VersionCache.GetVersion(migration.ToType) ?? 0;

            options.Logger?.ZLogInformation(
                $"Applying migration from {migration.FromType.Name} (v{fromVersion}) to {migration.ToType.Name} (v{toVersion})"
            );

            current = migration.Migrate(current);
            currentType = migration.ToType;
        }

        return (T)current;
    }
}
