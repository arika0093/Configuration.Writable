using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.State;

/// <summary>
/// Bridges the existing physical-file change notification behavior to the State watcher contract.
/// </summary>
/// <typeparam name="T">The options type.</typeparam>
internal sealed class LegacyFileStateWatcher<T> : IStateWatcher
    where T : class, new()
{
    private readonly WritableOptionsConfiguration<T> _options;

    internal LegacyFileStateWatcher(WritableOptionsConfiguration<T> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async ValueTask WaitForChangeAsync(
        string? observedRevision,
        CancellationToken cancellationToken = default
    )
    {
        if (_options.FileProvider is not IPhysicalFileProvider physicalFileProvider)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return;
        }

        var watchedPath = physicalFileProvider.GetPhysicalFilePath(GetWatchedPath());
        var directory = Path.GetDirectoryName(watchedPath);
        if (string.IsNullOrEmpty(directory))
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return;
        }

        Directory.CreateDirectory(directory);
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
            EnableRaisingEvents = true,
        };

        FileSystemEventHandler onFileChanged = (_, eventArgs) =>
        {
            if (IsRelevant(eventArgs.FullPath, watchedPath))
            {
                change.TrySetResult(true);
            }
        };
        RenamedEventHandler onRenamed = (_, eventArgs) =>
        {
            if (IsRelevant(eventArgs.FullPath, watchedPath))
            {
                change.TrySetResult(true);
            }
        };
        ErrorEventHandler onError = (_, eventArgs) =>
            change.TrySetException(eventArgs.GetException());

        watcher.Changed += onFileChanged;
        watcher.Created += onFileChanged;
        watcher.Deleted += onFileChanged;
        watcher.Renamed += onRenamed;
        watcher.Error += onError;

        await change.Task.ConfigureAwait(false);
    }

    private string GetWatchedPath() =>
        _options.FormatProvider is FallbackFormatProvider fallbackProvider
            ? fallbackProvider.GetSelectedFilePath(_options)
            : _options.ReadFilePath;

    private bool IsRelevant(string changedPath, string watchedPath)
    {
        if (_options.FormatProvider is not FallbackFormatProvider)
        {
            return string.Equals(changedPath, watchedPath, StringComparison.OrdinalIgnoreCase);
        }

        var canonicalPath = _options.FileProvider is IPhysicalFileProvider physicalFileProvider
            ? physicalFileProvider.GetPhysicalFilePath(_options.ConfigFilePath)
            : _options.ConfigFilePath;
        return string.Equals(changedPath, watchedPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(changedPath, canonicalPath, StringComparison.OrdinalIgnoreCase);
    }
}
