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

public static class WritableConfigurationExtensions
{
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
