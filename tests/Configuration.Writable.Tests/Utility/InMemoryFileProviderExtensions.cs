using Configuration.Writable.Configure;
using Configuration.Writable.FileProvider;

namespace Configuration.Writable;

public static class InMemoryFileProviderExtensions
{
    /// <summary>
    /// Configures the current instance to use the specified in-memory file writer for file operations. for testing purpose.
    /// </summary>
    /// <param name="inMemoryFileProvider">The in-memory file writer to use for subsequent file write and read operations.</param>
    public static void UseInMemoryFileProvider<T>(
        this WritableOptionsConfigBuilder<T> builder,
        InMemoryFileProvider inMemoryFileProvider
    )
        where T : class, new()
    {
        builder.FileProvider = inMemoryFileProvider;
    }
}
