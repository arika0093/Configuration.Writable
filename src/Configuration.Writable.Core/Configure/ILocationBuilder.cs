using System;
using System.Collections.Generic;
using System.IO;

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
    ILocationBuilder AddFilePath(string path);

    /// <summary>
    /// Gets the list of configured paths from various location providers. for internal use only.
    /// </summary>
    IEnumerable<string> SaveLocationPaths { get; }
}

/// <summary>
/// Defines methods to configure the directory for saving application settings.
/// </summary>
public interface ILocationBuilderWithDirectory : ILocationBuilder
{
    /// <summary>
    /// Sets the configuration folder to the standard save location for the specified application.
    /// </summary>
    /// <remarks>
    /// in Windows: %APPDATA%/<paramref name="applicationId"/> <br/>
    /// in macOS: ~/Library/Application Support/<paramref name="applicationId"/> <br/>
    /// in Linux: $XDG_CONFIG_HOME/<paramref name="applicationId"/>
    /// </remarks>
    /// <param name="applicationId">The unique identifier of the application. This is used to determine the subdirectory within the user
    /// configuration root directory.</param>
    /// <param name="enabled">If false, this location provider will be skipped.</param>
    ILocationBuilder UseStandardSaveDirectory(string applicationId, bool enabled = true);

    /// <summary>
    /// Sets the configuration folder to the directory where the executable is located. (default behavior)
    /// </summary>
    /// <remarks>
    /// This uses <see cref="AppContext.BaseDirectory"/> to determine the executable directory.
    /// </remarks>
    /// <param name="enabled">If false, this location provider will be skipped.</param>
    ILocationBuilder UseExecutableDirectory(bool enabled = true);

    /// <summary>
    /// Sets the configuration folder to the current working directory.
    /// </summary>
    /// <remarks>
    /// This uses <see cref="Directory.GetCurrentDirectory()"/> to determine the current directory.
    /// </remarks>
    /// <param name="enabled">If false, this location provider will be skipped.</param>
    ILocationBuilder UseCurrentDirectory(bool enabled = true);


    /// <summary>
    /// Sets the configuration folder to a special folder defined by <see cref="Environment.SpecialFolder"/>.
    /// </summary>
    /// <param name="folder">The special folder to use as the configuration folder.</param>
    /// <param name="enabled">If false, this location provider will be skipped.</param>
    ILocationBuilder UseSpecialFolder(Environment.SpecialFolder folder, bool enabled = true);

}