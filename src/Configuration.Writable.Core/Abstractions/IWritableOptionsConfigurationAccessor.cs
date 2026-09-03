namespace Configuration.Writable;

/// <summary>
/// Provides the Core configuration object for the default options instance.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface IWritableOptionsConfigurationAccessor<T>
    where T : class, new()
{
    /// <summary>
    /// Gets the writable configuration for the default options instance.
    /// </summary>
    WritableOptionsConfiguration<T> GetOptionsConfiguration();
}

/// <summary>
/// Provides Core configuration objects for named options instances.
/// </summary>
/// <typeparam name="T">The type of the options class.</typeparam>
public interface INamedWritableOptionsConfigurationAccessor<T>
    where T : class, new()
{
    /// <summary>
    /// Gets the writable configuration for the specified options instance.
    /// </summary>
    /// <param name="name">The name of the options instance.</param>
    WritableOptionsConfiguration<T> GetOptionsConfiguration(string name);
}
