using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;

namespace Configuration.Writable.State;

internal sealed class FileStateResource<T> : IStateResource
    where T : class, new()
{
    private readonly WritableOptionsConfiguration<T> _options;
    private readonly FileStateWatcher<T> _watcher;

    internal FileStateResource(WritableOptionsConfiguration<T> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _watcher = new FileStateWatcher<T>(_options, options.FileProvider);
    }

    internal WritableOptionsConfiguration<T> Options => _options;

    public string? GetRevision() =>
        ConfigurationFileFingerprint
            .Capture(_options.GetSelectedFilePath(), _options.FileProvider)
            ?.ToRevision();

    public ValueTask WaitForChangeAsync(
        string? observedRevision,
        CancellationToken cancellationToken = default
    ) => _watcher.WaitForChangeAsync(observedRevision, cancellationToken);

    internal string GetPhysicalPath(string path)
    {
        if (_options.FileProvider is IPhysicalFileProvider physicalFileProvider)
        {
            return physicalFileProvider.GetPhysicalFilePath(path);
        }
        return Path.GetFullPath(path);
    }

    internal bool FileExists(string path) => _options.FileProvider.FileExists(path);

    internal Stream OpenRead(string path)
    {
        var pipeReader = _options.FileProvider.GetFilePipeReader(path);
        if (pipeReader == null)
        {
            throw new FileNotFoundException($"File not found: {path}");
        }
        return pipeReader.AsStream(leaveOpen: false);
    }

    internal async Task WriteAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
    )
    {
        await _options
            .FileProvider.SaveToFileAsync(path, content, _options.Logger, cancellationToken)
            .ConfigureAwait(false);
    }
}
