using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.State;

/// <summary>Observes relevant changes for a file-backed state resource.</summary>
internal sealed class FileStateWatcher<T> : IStateWatcher
    where T : class, new()
{
    private readonly WritableOptionsConfiguration<T> _options;

    internal FileStateWatcher(WritableOptionsConfiguration<T> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async ValueTask WaitForChangeAsync(
        string? observedRevision,
        CancellationToken cancellationToken = default
    )
    {
        var watchedFilePath = GetWatchedPath();
        var watchedPath =
            _options.FileProvider is IPhysicalFileProvider physicalFileProvider
                ? physicalFileProvider.GetPhysicalFilePath(watchedFilePath)
                : watchedFilePath;
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

        var filter =
            _options.FormatProvider is FallbackFormatProvider ? "*" : Path.GetFileName(watchedPath);
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
            if (!_options.FileProvider.FileExists(watchedFilePath))
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

        if (!_options.FileProvider.FileExists(watchedFilePath))
        {
            change.TrySetException(CreateDeletedFileException(watchedFilePath));
        }
        else if (RevisionChanged(observedRevision))
        {
            change.TrySetResult(true);
        }

        await change.Task.ConfigureAwait(false);
    }

    private string GetWatchedPath() =>
        _options.FormatProvider is FallbackFormatProvider fallbackProvider
            ? fallbackProvider.GetSelectedFilePath(_options)
            : _options.ConfigFilePath;

    private bool RevisionChanged(string? observedRevision)
    {
        if (observedRevision is null)
        {
            return false;
        }

        var currentRevision = ConfigurationFileFingerprint.Capture(_options)?.ToRevision();
        return !string.Equals(observedRevision, currentRevision, StringComparison.Ordinal);
    }

    private void ThrowIfWatchedFileWasDeleted(string watchedFilePath)
    {
        if (!_options.FileProvider.FileExists(watchedFilePath))
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
        if (_options.FormatProvider is not FallbackFormatProvider)
        {
            return string.Equals(changedPath, watchedPath, comparison);
        }

        var canonicalPath = _options.FileProvider is IPhysicalFileProvider physicalFileProvider
            ? physicalFileProvider.GetPhysicalFilePath(_options.ConfigFilePath)
            : _options.ConfigFilePath;
        return string.Equals(changedPath, watchedPath, comparison)
            || string.Equals(changedPath, canonicalPath, comparison);
    }
}
