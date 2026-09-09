namespace Configuration.Writable;

/// <summary>
/// Migrates an options model from one schema version to the next.
/// </summary>
/// <typeparam name="TOld">The previous options model type.</typeparam>
/// <typeparam name="TNew">The current options model type.</typeparam>
public interface IOptionsMigration<in TOld, out TNew>
    where TOld : class, new()
    where TNew : class, new()
{
    /// <summary>
    /// Migrates a previous options model to the current model.
    /// </summary>
    /// <param name="source">The previous options model.</param>
    /// <returns>The migrated options model.</returns>
    TNew Migrate(TOld source);
}
