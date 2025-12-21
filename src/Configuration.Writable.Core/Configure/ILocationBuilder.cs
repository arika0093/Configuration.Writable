using System.Collections.Generic;

namespace Configuration.Writable.Configure;

/// <summary>
/// Defines methods to configure the save location for application settings.
/// </summary>
public interface ILocationBuilder
{
    /// <summary>
    /// Add a relative file path for the save location.
    /// </summary>
    /// <param name="path">The relative file path.</param>
    /// <param name="priority">The priority of this path. Higher priority paths are evaluated first.</param>
    ILocationBuilder AddFilePath(string path, int priority = 0);

    /// <summary>
    /// Gets the list of configured paths from various location providers. for internal use only.
    /// </summary>
    IEnumerable<LocationPathInfo> SaveLocationPaths { get; }
}

/// <summary>
/// Information about a location path and its priority.
/// </summary>
/// <param name="Path">The file path.</param>
/// <param name="Priority">The priority of the path.</param>
public record LocationPathInfo(string Path, int Priority);
