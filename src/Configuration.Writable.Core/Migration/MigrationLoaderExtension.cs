using System;
using Configuration.Writable.FileProvider;
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

        // If the target type is not versioned, simply load it directly.
        var targetVersion = targetMetadata?.Version;
        if (targetVersion is null)
        {
            return (T)formatProvider.LoadConfiguration(typeof(T), options);
        }

        var metadataProvider = formatProvider as IOptionsSchemaMetadataProvider;
        var fileMetadata = metadataProvider is null
            ? null
            : FormatProviderBase.ExecuteWithBackupRecovery(
                options,
                () => metadataProvider.ReadSchemaMetadata(options)
            );
        ValidateFileMetadata(fileMetadata);
        ValidateModelId(targetMetadata, fileMetadata);

        // Missing documents or sections are initialized directly as the target type.
        if (fileMetadata is null)
        {
            return (T)formatProvider.LoadConfiguration(typeof(T), options);
        }

        var fileVersion = fileMetadata.Version ?? 1;

        // The file declares a version. If it already matches the target, load directly.
        if (fileVersion == targetVersion)
        {
            return (T)formatProvider.LoadConfiguration(typeof(T), options);
        }

        // The file declares a version that is newer than the target. This is an unsupported scenario.
        if (fileVersion > targetVersion.Value)
        {
            throw new InvalidOperationException(
                $"Configuration schema version {fileVersion} is newer than supported version {targetVersion.Value}."
            );
        }

        // Find the type matching the declared file version.
        if (
            migrationLookup is null
            || !migrationLookup.TryGetType(fileVersion, out var currentType)
        )
        {
            // The compatibility is broken, so create a backup and return the default values.
            string? backupPath = null;
            var success =
                options.FileProvider is IBackupFileProvider backupFileProvider
                && backupFileProvider.TryBackup(
                    options.ConfigFilePath,
                    out backupPath,
                    options.Logger
                );
            if (success)
            {
                options.Logger?.ZLogWarning(
                    $"Configuration schema version {fileVersion} is no longer supported by {typeof(T).Name}. Using defaults. The original configuration was backed up to: {backupPath}"
                );
            }
            else
            {
                options.Logger?.ZLogWarning(
                    $"Configuration schema version {fileVersion} is no longer supported by {typeof(T).Name}. Using defaults without a backup."
                );
            }
            return new T();
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
