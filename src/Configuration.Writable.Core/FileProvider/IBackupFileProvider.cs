using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FileProvider;

/// <summary>Provides optional configuration backup support.</summary>
public interface IBackupFileProvider
{
    /// <summary>Attempts to create a backup of the specified file.</summary>
    /// <param name="path">The path of the file to back up.</param>
    /// <param name="backupPath">The provider-defined backup path when a backup was created; otherwise, <see langword="null"/>.</param>
    /// <param name="logger">An optional logger for logging operations and errors.</param>
    /// <returns><see langword="true"/> when a backup was created; otherwise, <see langword="false"/>.</returns>
    bool TryBackup(string path, out string? backupPath, ILogger? logger = null);
}
