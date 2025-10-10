using Configuration.Writable.FileWriter;

namespace Configuration.Writable;

public static class InMemoryFileWriterExtensions
{
    /// <summary>
    /// Configures the current instance to use the specified in-memory file writer for file operations. for testing purpose.
    /// </summary>
    /// <param name="inMemoryFileWriter">The in-memory file writer to use for subsequent file write and read operations.</param>
    public static void UseInMemoryFileWriter<T>(
        this WritableConfigurationOptionsBuilder<T> builder,
        InMemoryFileWriter inMemoryFileWriter
    )
        where T : class
    {
        builder.FileWriter = inMemoryFileWriter;
        builder.FileReadStream = inMemoryFileWriter.GetFileStream(builder.ConfigFilePath);
    }
}
