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
