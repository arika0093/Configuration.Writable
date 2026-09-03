using System;

namespace Configuration.Writable.Migration;

/// <summary>
/// Represents a single migration step from one configuration version to another.
/// </summary>
internal abstract class MigrationStep
{
    /// <summary>
    /// Gets the source type that will be migrated from.
    /// </summary>
    public Type FromType { get; protected init; } = null!;

    /// <summary>
    /// Gets the target type that will be migrated to.
    /// </summary>
    public Type ToType { get; protected init; } = null!;

    public string? ModelId { get; protected init; }

    public int? FromVersion { get; protected init; }

    public int ToVersion { get; protected init; }

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
internal sealed class MigrationStep<TOld, TNew> : MigrationStep
    where TOld : class, new()
    where TNew : class, new()
{
    private readonly Func<TOld, TNew> _migrationFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationStep{TOld, TNew}"/> class.
    /// </summary>
    /// <param name="migrationFunc">The function that performs the migration.</param>
    /// <param name="modelId">The stable model identifier.</param>
    /// <param name="oldVersion">The source schema version.</param>
    /// <param name="newVersion">The target schema version.</param>
    public MigrationStep(
        Func<TOld, TNew> migrationFunc,
        string? modelId,
        int oldVersion,
        int newVersion
    )
    {
        _migrationFunc = migrationFunc;
        FromType = typeof(TOld);
        ToType = typeof(TNew);
        ModelId = modelId;
        FromVersion = oldVersion;
        ToVersion = newVersion;
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
/// Type-safe migration step from a configuration without a version (version 0) to a versioned configuration.
/// </summary>
/// <typeparam name="TSource">The old configuration type without <see cref="IHasVersion"/>.</typeparam>
/// <typeparam name="TNew">The new configuration type. Must implement <see cref="IHasVersion"/>.</typeparam>
internal sealed class MigrationStepFromNone<TSource, TNew> : MigrationStep
    where TSource : class, new()
    where TNew : class, new()
{
    private readonly Func<TSource, TNew> _migrationFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationStepFromNone{TSource, TNew}"/> class.
    /// </summary>
    /// <param name="migrationFunc">The function that performs the migration.</param>
    /// <param name="modelId">The stable model identifier.</param>
    /// <param name="newVersion">The target schema version.</param>
    public MigrationStepFromNone(Func<TSource, TNew> migrationFunc, string? modelId, int newVersion)
    {
        _migrationFunc = migrationFunc;
        FromType = typeof(TSource);
        ToType = typeof(TNew);
        ModelId = modelId;
        FromVersion = null;
        ToVersion = newVersion;
    }

    /// <inheritdoc />
    public override object Migrate(object oldValue)
    {
        if (oldValue is not TSource typedOldValue)
        {
            throw new InvalidOperationException(
                $"Expected type {typeof(TSource).Name} but received {oldValue.GetType().Name}"
            );
        }

        return _migrationFunc(typedOldValue);
    }
}
