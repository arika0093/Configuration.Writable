using Configuration.Writable.Configure;

namespace Configuration.Writable;

internal static class InMemoryFileBackendExtensions
{
    /// <summary>
    /// Configures the current instance to use the specified in-memory file backend for file operations. For testing purpose.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="backend">The in-memory file backend to use for subsequent file write and read operations.</param>
    internal static void UseInMemoryBackend<T>(
        this WritableOptionsConfigBuilder<T> builder,
        InMemoryFileBackend backend
    )
        where T : class, new()
    {
        builder.FileBackend = backend;
    }

    internal static void UseInMemoryBackend(
        this WritableOptionsConfigBuilder builder,
        InMemoryFileBackend backend
    )
    {
        builder.FileBackend = backend;
    }
}
