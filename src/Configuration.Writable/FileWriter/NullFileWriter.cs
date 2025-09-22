using System;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.FileWriter;

/// <summary>
/// Provides a no-op implementation of the IFileWriter interface. for testing or scenarios where file writing is not required.
/// </summary>
public class NullFileWriter : IFileWriter
{
    /// <inheritdoc />
    public Task SaveToFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
    )
    {
        // do nothing
        return Task.CompletedTask;
    }
}
