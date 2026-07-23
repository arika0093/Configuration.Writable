namespace Configuration.Writable.FileProvider;

/// <summary>
/// Provides the physical file path that backs a logical configuration path.
/// </summary>
internal interface IPhysicalFileProvider
{
    /// <summary>
    /// Gets the physical file path to use for file watching and conflict detection.
    /// </summary>
    string GetPhysicalFilePath(string path);
}
