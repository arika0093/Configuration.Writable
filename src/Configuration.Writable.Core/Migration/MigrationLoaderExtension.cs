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
        var targetMetadata = options.SchemaMetadata;
        var metadataProvider = formatProvider as IOptionsSchemaMetadataProvider;
        var fileMetadata = metadataProvider?.ReadSchemaMetadata(options);
        ValidateFileMetadata(fileMetadata);
        ValidateModelId(targetMetadata, fileMetadata);

        // If the target type is not versioned, simply load it directly.
        var targetVersion = targetMetadata?.Version;
        if (targetVersion is null)
        {
            return (T)formatProvider.LoadConfiguration(typeof(T), options);
        }

        var fileVersion = fileMetadata?.Version;

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

    private static void ValidateFileMetadata(OptionsSchemaMetadata? fileMetadata)
    {
        if (fileMetadata?.ModelId is not null && string.IsNullOrWhiteSpace(fileMetadata.ModelId))
        {
            throw new FormatException("Configuration model ID cannot be empty.");
        }
        if (fileMetadata?.Version is <= 0)
        {
            throw new FormatException("Configuration schema version must be greater than zero.");
        }
    }

    private static void ValidateModelId(
        OptionsSchemaMetadata? targetMetadata,
        OptionsSchemaMetadata? fileMetadata
    )
    {
        if (
            targetMetadata?.ModelId is not null
            && fileMetadata?.ModelId is not null
            && targetMetadata.ModelId != fileMetadata.ModelId
        )
        {
            throw new InvalidOperationException(
                $"Configuration model ID '{fileMetadata.ModelId}' does not match expected model ID '{targetMetadata.ModelId}'."
            );
        }
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

            var fromVersion = migration.FromVersion ?? 0;
            var toVersion = migration.ToVersion;

            options.Logger?.ZLogInformation(
                $"Applying migration from {migration.FromType.Name} (v{fromVersion}) to {migration.ToType.Name} (v{toVersion})"
            );

            current = migration.Migrate(current);
            currentType = migration.ToType;
        }

        return (T)current;
    }
}
