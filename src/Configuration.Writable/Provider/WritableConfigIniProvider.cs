using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Configuration.Writable.Provider;

/// <summary>
/// Writable configuration implementation for INI files.
/// </summary>
/// <typeparam name="T">The type of the configuration class.</typeparam>
public class WritableConfigIniProvider<T> : IWritableConfigProvider<T>
    where T : class
{
    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <inheritdoc />
    public void AddConfigurationFile(IConfigurationBuilder configuration, string path) =>
        configuration.AddIniFile(path, optional: true, reloadOnChange: true);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetSaveContents(T config, WritableConfigurationOptions<T> options)
    {
        var sectionName = options.SectionName;
        var sb = new StringBuilder();
        var type = typeof(T);

        if (!string.IsNullOrWhiteSpace(sectionName))
            sb.AppendLine($"[{sectionName}]");

        foreach (var prop in type.GetProperties())
        {
            var value = prop.GetValue(config);
            sb.AppendLine($"{prop.Name}={value}");
        }

        var iniString = sb.ToString();
        return Encoding.GetBytes(iniString);
    }
}
