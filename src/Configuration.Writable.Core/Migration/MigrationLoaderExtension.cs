using System;
using Configuration.Writable.FormatProvider;
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
        this FormatProvider.IWritableFormatProvider formatProvider,
        WritableOptionsConfiguration<T> options
    )
        where T : class, new()
    {
        var migrationLookup = options.MigrationLookup;

        // If the target type is not versioned, simply load it directly.
        var targetVersion = migrationLookup is null
            ? VersionCache.GetVersion(typeof(T))
            : migrationLookup.TargetVersion;
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
            var fromNoneStep = migrationLookup?.FromNoneStep;

            if (fromNoneStep is null)
            {
                // No migration from an unversioned type is registered; load as the target type.
                return (T)formatProvider.LoadConfiguration(typeof(T), options);
            }

            // Start from the unversioned type and apply the migration chain.
            return ApplyMigrationChain<T>(
                formatProvider,
                options,
                migrationLookup!,
                fromNoneStep.FromType
            );
        }

        // The file declares a version. If it already matches the target, load directly.
        if (fileVersion == targetVersion)
        {
            return (T)formatProvider.LoadConfiguration(typeof(T), options);
        }

        // Find the type matching the declared file version.
        if (
            migrationLookup is null
            || !migrationLookup.TryGetType(fileVersion.Value, out var currentType)
        )
        {
            throw new InvalidOperationException(
                $"No type found matching version {fileVersion} in migration chain."
            );
        }

        return ApplyMigrationChain<T>(formatProvider, options, migrationLookup, currentType);
    }

    private static T ApplyMigrationChain<T>(
        FormatProvider.IWritableFormatProvider formatProvider,
        WritableOptionsConfiguration<T> options,
        MigrationLookup migrationLookup,
        Type startingType
    )
        where T : class, new()
    {
        var currentType = startingType;
        var current = formatProvider.LoadConfiguration(currentType, options);

        while (currentType != typeof(T))
        {
            if (!migrationLookup.TryGetMigration(currentType, out var migration))
            {
                throw new InvalidOperationException(
                    $"""
                    No migration found from {currentType.Name} to reach {typeof(T).Name}.
                    Ensure all migration steps are registered in the correct order.
                    """
                );
            }

            var fromVersion = migrationLookup.GetVersion(migration.FromType) ?? 0;
            var toVersion = migrationLookup.GetVersion(migration.ToType) ?? 0;

            options.Logger?.ZLogInformation(
                $"Applying migration from {migration.FromType.Name} (v{fromVersion}) to {migration.ToType.Name} (v{toVersion})"
            );

            current = migration.Migrate(current);
            currentType = migration.ToType;
        }

        return (T)current;
    }
}
