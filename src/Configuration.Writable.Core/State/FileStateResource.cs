using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Configuration.Writable.State;

internal sealed class FileStateResource<T> : IStateResource
    where T : class, new()
{
    private readonly WritableOptionsConfiguration<T> _options;
    private readonly FileStateWatcher<T> _watcher;

    internal FileStateResource(WritableOptionsConfiguration<T> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _watcher = new FileStateWatcher<T>(_options, options.FileBackend);
    }

    internal WritableOptionsConfiguration<T> Options => _options;

    internal IFileBackend Backend => _options.FileBackend;

    public string? GetRevision() =>
        ConfigurationFileFingerprint
            .Capture(_options.GetSelectedFilePath(), _options.FileBackend)
            ?.ToRevision();

    public ValueTask WaitForChangeAsync(
        string? observedRevision,
        CancellationToken cancellationToken = default
    ) => _watcher.WaitForChangeAsync(observedRevision, cancellationToken);

    internal string GetPhysicalPath(string path)
    {
        if (_options.FileBackend.IsPhysical)
        {
            return _options.FileBackend.GetPhysicalPath(path);
        }
        return Path.GetFullPath(path);
    }

    internal bool FileExists(string path) => _options.FileBackend.FileExists(path);

    internal Stream OpenRead(string path)
    {
        return _options.FileBackend.OpenReadStream(path)
            ?? throw new FileNotFoundException($"File not found: {path}");
    }

    internal Task WriteAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
    )
    {
        return _options.FileBackend.SaveToFileAsync(
            path,
            content,
            _options.Logger,
            cancellationToken
        );
    }
}
