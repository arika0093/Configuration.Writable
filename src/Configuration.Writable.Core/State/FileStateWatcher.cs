using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.State;

internal sealed class FileStateWatcher<T> : IStateWatcher
    where T : class, new()
{
    private readonly WritableOptionsConfiguration<T> _options;
    private readonly IFileBackend _backend;

    internal FileStateWatcher(WritableOptionsConfiguration<T> options, IFileBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
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
            ThrowIfNoSelectableFile();
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
            // Re-resolve the selection: the watched file may have been deleted
            // while another registered fallback still exists. Only report a
            // deletion failure when nothing is selectable anymore.
            if (!HasSelectableFile(out var currentPath))
            {
                change.TrySetException(CreateDeletedFileException(currentPath));
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
            if (!HasSelectableFile(out var currentPath))
            {
                change.TrySetException(CreateDeletedFileException(currentPath));
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
        if (_backend.IsPhysical)
        {
            return _backend.GetPhysicalPath(path);
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
            .Capture(_options.GetSelectedFilePath(), _backend)
            ?.ToRevision();
        return !string.Equals(observedRevision, currentRevision, StringComparison.Ordinal);
    }

    private void ThrowIfNoSelectableFile()
    {
        if (!HasSelectableFile(out var currentPath))
        {
            throw CreateDeletedFileException(currentPath);
        }
    }

    private bool HasSelectableFile(out string currentPath)
    {
        currentPath = GetWatchedPath();
        return _backend.FileExists(currentPath);
    }

    private static FileNotFoundException CreateDeletedFileException(string watchedFilePath) =>
        new($"Configuration file was deleted: {watchedFilePath}", watchedFilePath);

    private bool IsRelevant(string changedPath, string watchedPath)
    {
        var comparison =
            Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        if (string.Equals(changedPath, watchedPath, comparison))
        {
            return true;
        }

        var canonicalPath = GetPhysicalPath(_options.ConfigFilePath);
        if (string.Equals(changedPath, canonicalPath, comparison))
        {
            return true;
        }

        if (!_options.HasFallbackFormats)
        {
            return false;
        }

        // A fallback file created after startup must wake the watcher, but only
        // while nothing is selected yet. Once the canonical file or another
        // fallback is selected, unrelated fallback changes cannot affect the
        // effective value and must not trigger spurious reloads.
        if (_backend.FileExists(GetWatchedPath()))
        {
            return false;
        }

        foreach (var fallback in _options.FallbackFormats)
        {
            var candidate = GetPhysicalPath(
                Path.ChangeExtension(_options.ConfigFilePath, fallback.FileExtension)
            );
            if (string.Equals(changedPath, candidate, comparison))
            {
                return true;
            }
        }

        return false;
    }
}
