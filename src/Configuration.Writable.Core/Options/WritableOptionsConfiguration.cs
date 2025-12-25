using System;
using System.Collections.Generic;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

/// <summary>
/// Represents a single migration step from one configuration version to another.
/// </summary>
public abstract class MigrationStep
{
    /// <summary>
    /// Gets the source type that will be migrated from.
    /// </summary>
    public Type FromType { get; protected init; } = null!;

    /// <summary>
    /// Gets the target type that will be migrated to.
    /// </summary>
    public Type ToType { get; protected init; } = null!;

    /// <summary>
    /// Applies the migration to the given object.
    /// </summary>
    /// <param name="oldValue">The old configuration instance to migrate.</param>
    /// <returns>The migrated configuration instance.</returns>
    public abstract object Migrate(object oldValue);
}

/// <summary>
/// Type-safe migration step from one configuration version to another.
/// </summary>
/// <typeparam name="TOld">The old configuration type.</typeparam>
/// <typeparam name="TNew">The new configuration type.</typeparam>
public sealed class MigrationStep<TOld, TNew> : MigrationStep
    where TOld : class, IHasVersion
    where TNew : class, IHasVersion
{
    private readonly Func<TOld, TNew> _migrationFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationStep{TOld, TNew}"/> class.
    /// </summary>
    /// <param name="migrationFunc">The function that performs the migration.</param>
    public MigrationStep(Func<TOld, TNew> migrationFunc)
    {
        _migrationFunc = migrationFunc;
        FromType = typeof(TOld);
        ToType = typeof(TNew);
    }

    /// <inheritdoc />
    public override object Migrate(object oldValue)
    {
        if (oldValue is not TOld typedOldValue)
        {
            throw new InvalidOperationException(
                $"Expected type {typeof(TOld).Name} but received {oldValue.GetType().Name}"
            );
        }

        return _migrationFunc(typedOldValue);
    }
}

/// <summary>
/// Options for initializing writable configuration.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public record WritableOptionsConfiguration<T>
    where T : class, new()
{
    /// <summary>
    /// Gets or sets a instance of <see cref="IFormatProvider"/> used to handle the serialization and deserialization of the configuration data.<br/>
    /// Defaults to <see cref="JsonFormatProvider"/> which uses JSON format. <br/>
    /// </summary>
    public required FormatProvider.IFormatProvider FormatProvider { get; init; }

    /// <summary>
    /// Gets or sets a instance of <see cref="IFileProvider"/> used to handle the file writing operations.
    /// </summary>
    public required IFileProvider FileProvider { get; init; }

    /// <summary>
    /// Gets the full file path to the configuration file, combining config folder and file name.
    /// </summary>
    public required string ConfigFilePath { get; init; }

    /// <summary>
    /// Gets or sets the name of the configuration instance. Defaults to Options.DefaultName ("").
    /// </summary>
    public required string InstanceName { get; init; }

    /// <summary>
    /// Gets the parts of the section name split by ':' and '__' for hierarchical navigation.
    /// If empty, that means the root of the configuration file.
    /// </summary>
    public required List<string> SectionNameParts { get; init; }

    /// <summary>
    /// Gets or sets the throttle duration in milliseconds for change events.
    /// This helps to prevent excessive event firing during rapid changes.
    /// </summary>
    public required int OnChangeThrottleMs { get; init; }

    /// <summary>
    /// Gets or sets the cloning strategy function to create deep copies of the configuration object.
    /// </summary>
    public required Func<T, T> CloneMethod { get; init; }

    /// <summary>
    /// Gets or sets the logger for configuration operations.
    /// If null, logging is disabled. Defaults to null.
    /// </summary>
    public ILogger? Logger { get; init; }

    /// <summary>
    /// Gets or sets the validation function to be executed before saving configuration.
    /// If null, no validation is performed. Defaults to null.
    /// </summary>
    public Func<T, ValidateOptionsResult>? Validator { get; init; }

    /// <summary>
    /// Gets the list of migration steps to apply when loading configuration from older versions.
    /// The migrations are applied in the order they are defined (e.g., V1 -> V2 -> V3).
    /// </summary>
    public List<MigrationStep> MigrationSteps { get; init; } = [];
}
