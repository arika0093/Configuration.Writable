using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.Provider;

/// <summary>
/// Provides functionality to write data to a file, with support for creating backups and restoring the original file in
/// case of failure.
/// </summary>
public class CommonWriteFileProvider : IWriteFileProvider
{
    /// <inheritdoc />
    public async Task SaveToFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
    )
    {
        string? backupPath = null;
        // create directory if not exists
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        // make backup if file exists
        if (File.Exists(path))
        {
            backupPath = path + ".bak";
            File.Copy(path, backupPath, true);
        }
        try
        {
#if NET9_0_OR_GREATER
            await File.WriteAllBytesAsync(path, content, cancellationToken);
#elif NET
            await File.WriteAllBytesAsync(path, content.ToArray(), cancellationToken);
#else
            await Task.Run(() => File.WriteAllBytes(path, content.ToArray()), cancellationToken);
#endif
            // delete backup if write succeeded
            if (backupPath is not null && File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        catch
        {
            // if operation was cancelled, restore from backup
            if (backupPath is not null && File.Exists(backupPath))
            {
                File.Copy(backupPath, path, true);
            }
            throw;
        }
    }
}
