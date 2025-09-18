#pragma warning disable IDE0130
using System;
using System.IO;
using System.Xml.Linq;
using Configuration.Writable;
using Configuration.Writable.Imprements;
using Configuration.Writable.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SpecialFolder = System.Environment.SpecialFolder;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for writable configuration.
/// </summary>
public static class WritableConfigurationExtensions
{
    /// <summary>
    /// Adds a writable user configuration file to the host application builder.
    /// </summary>
    /// <typeparam name="T">The type of the configuration class.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="fileName">The configuration file name.</param>
    /// <param name="folderName">The folder name for the configuration file.</param>
    /// <param name="configureOptions">An optional action to configure options.</param>
    /// <returns>The host application builder.</returns>
    public static IHostApplicationBuilder AddWritableUserConfigFile<T>(
        this IHostApplicationBuilder builder,
        string fileName,
        string folderName,
        Action<T>? configureOptions = null
    )
        where T : class
    {
        var configRoot = UserConfigurationPath.GetUserConfigRootDirectory();
        var fullPath = Path.Combine(configRoot, folderName, fileName);
        return builder.AddWritableUserConfigFile<T>(fullPath, configureOptions);
    }

    /// <summary>
    /// Adds a writable user configuration file to the host application builder, specifying a special folder.
    /// </summary>
    /// <typeparam name="T">The type of the configuration class.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="fileName">The configuration file name.</param>
    /// <param name="folderName">The folder name for the configuration file.</param>
    /// <param name="configFolder">The special folder to use as the root.</param>
    /// <param name="configureOptions">An optional action to configure options.</param>
    /// <returns>The host application builder.</returns>
    public static IHostApplicationBuilder AddWritableUserConfigFile<T>(
        this IHostApplicationBuilder builder,
        string fileName,
        string folderName,
        SpecialFolder configFolder,
        Action<T>? configureOptions = null
    )
        where T : class
    {
        var configRoot = Environment.GetFolderPath(configFolder);
        var fullPath = Path.Combine(configRoot, folderName, fileName);
        return builder.AddWritableUserConfigFile<T>(fullPath, configureOptions);
    }

    /// <summary>
    /// Adds a writable user configuration file to the host application builder, specifying the full file path.
    /// </summary>
    /// <typeparam name="T">The type of the configuration class.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="filePath">The full path to the configuration file.</param>
    /// <param name="configureOptions">An optional action to configure options.</param>
    /// <returns>The host application builder.</returns>
    public static IHostApplicationBuilder AddWritableUserConfigFile<T>(
        this IHostApplicationBuilder builder,
        string filePath,
        Action<T>? configureOptions = null
    )
        where T : class
    {
        if (configureOptions == null)
        {
            configureOptions = _ => { };
        }
        // add configuration
        builder.Configuration.AddJsonFile(filePath, optional: true, reloadOnChange: true);
        // add IOptions<T>
        builder.Services.Configure<T>(configureOptions);
        // add IWritableOptions<T>
        builder.Services.TryAddSingleton<IWritableOptions<T>, WritableJsonConfiguration<T>>();
        builder.Services.Configure<WritableJsonConfigurationOptions<T>>(options =>
        {
            options.ConfigFilePath = filePath;
        });
        return builder;
    }
}
