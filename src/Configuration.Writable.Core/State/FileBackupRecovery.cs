using System;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.State;

/// <summary>
/// Backup-assisted read helper for file-backed state codecs. Restores the
/// latest backup when the target file is missing or unreadable.
/// </summary>
internal static class FileBackupRecovery
{
    internal static TResult Execute<TResult>(
        IFileBackend backend,
        string path,
        ILogger? logger,
        Func<TResult> operation
    )
    {
        if (!backend.FileExists(path))
        {
            backend.TryRestoreLatestBackup(path, logger);
        }

        try
        {
            return operation();
        }
        catch (Exception) when (backend.TryRestoreLatestBackup(path, logger))
        {
            return operation();
        }
    }
}
