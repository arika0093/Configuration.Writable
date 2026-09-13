using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;

namespace Configuration.Writable.State;

internal sealed class FileStateWatcher<T> : IStateWatcher
    where T : class, new()
{
    private readonly WritableOptionsConfiguration<T> _options;
    private readonly IWritableFileProvider _fileProvider;

    internal FileStateWatcher(
        WritableOptionsConfiguration<T> options,
        IWritableFileProvider fileProvider
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
    }

    public async ValueTask WaitForChangeAsync(
        string? observedRevision,
        CancellationToken cancellationToken = default
    )
    {
        var watchedFilePath = GetWatchedPath();
        var watchedPath = GetPhysicalPath(watchedFilePath);
        var directory = Path.GetDirectoryName(watchedPath);
        if (string.IsNullOrEmpty(directory))
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return;
        }

        Directory.CreateDirectory(directory);
        if (RevisionChanged(observedRevision))
        {
            ThrowIfWatchedFileWasDeleted(watchedFilePath);
            return;
        }

        var filter = _options.HasFallbackFormats ? "*" : Path.GetFileName(watchedPath);
        var change = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var cancellation = cancellationToken.Register(() =>
            change.TrySetCanceled(cancellationToken)
        );
        using var watcher = new FileSystemWatcher(directory, filter)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };

        void SignalChange()
        {
            if (!_fileProvider.FileExists(watchedFilePath))
            {
                change.TrySetException(CreateDeletedFileException(watchedFilePath));
                return;
            }

            change.TrySetResult(true);
        }

        FileSystemEventHandler onFileChanged = (_, eventArgs) =>
        {
            if (IsRelevant(eventArgs.FullPath, watchedPath))
            {
                SignalChange();
            }
        };
        RenamedEventHandler onRenamed = (_, eventArgs) =>
        {
            if (
                IsRelevant(eventArgs.FullPath, watchedPath)
                || IsRelevant(eventArgs.OldFullPath, watchedPath)
            )
            {
                SignalChange();
            }
        };
        ErrorEventHandler onError = (_, eventArgs) =>
            change.TrySetException(eventArgs.GetException());

        watcher.Changed += onFileChanged;
        watcher.Created += onFileChanged;
        watcher.Deleted += onFileChanged;
        watcher.Renamed += onRenamed;
        watcher.Error += onError;
        watcher.EnableRaisingEvents = true;

        if (RevisionChanged(observedRevision))
        {
            if (!_fileProvider.FileExists(watchedFilePath))
            {
                change.TrySetException(CreateDeletedFileException(watchedFilePath));
            }
            else
            {
                change.TrySetResult(true);
            }
        }

        await change.Task.ConfigureAwait(false);
    }

    private string GetWatchedPath()
    {
        if (_options.HasFallbackFormats)
        {
            return _options.GetSelectedFilePath();
        }
        return _options.ConfigFilePath;
    }

    private string GetPhysicalPath(string path)
    {
        if (_fileProvider is IPhysicalFileProvider physicalFileProvider)
        {
            return physicalFileProvider.GetPhysicalFilePath(path);
        }
        return Path.GetFullPath(path);
    }

    private bool RevisionChanged(string? observedRevision)
    {
        if (observedRevision is null)
        {
            return false;
        }

        var currentRevision = ConfigurationFileFingerprint
            .Capture(_options.GetSelectedFilePath(), _fileProvider)
            ?.ToRevision();
        return !string.Equals(observedRevision, currentRevision, StringComparison.Ordinal);
    }

    private void ThrowIfWatchedFileWasDeleted(string watchedFilePath)
    {
        if (!_fileProvider.FileExists(watchedFilePath))
        {
            throw CreateDeletedFileException(watchedFilePath);
        }
    }

    private static FileNotFoundException CreateDeletedFileException(string watchedFilePath) =>
        new($"Configuration file was deleted: {watchedFilePath}", watchedFilePath);

    private bool IsRelevant(string changedPath, string watchedPath)
    {
        var comparison =
            Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        if (!_options.HasFallbackFormats)
        {
            return string.Equals(changedPath, watchedPath, comparison);
        }

        var canonicalPath = GetPhysicalPath(_options.ConfigFilePath);
        return string.Equals(changedPath, watchedPath, comparison)
            || string.Equals(changedPath, canonicalPath, comparison);
    }
}
