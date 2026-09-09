using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FileProvider;

/// <summary>Provides optional file deletion support.</summary>
internal interface IFileDeleter
{
    /// <summary>Attempts to delete a file.</summary>
    bool TryDelete(string path, ILogger? logger = null);
}
