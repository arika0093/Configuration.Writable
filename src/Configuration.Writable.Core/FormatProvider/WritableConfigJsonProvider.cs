using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable;

/// <summary>
/// Writable configuration implementation for JSON files.
/// </summary>
public class WritableConfigJsonProvider : WritableConfigProviderBase
{
    /// <summary>
    /// Gets or sets the options to use when serializing and deserializing JSON data.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; init; } =
        new() { WriteIndented = false, ReferenceHandler = ReferenceHandler.IgnoreCycles };

    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; init; } = System.Text.Encoding.UTF8;

    /// <inheritdoc />
    public override string FileExtension => "json";

    /// <inheritdoc />
    public override T LoadConfiguration<T>(WritableConfigurationOptions<T> options)
    {
        var filePath = options.ConfigFilePath;
        if (!FileProvider.FileExists(filePath))
        {
            return new T();
        }

        var stream = FileProvider.GetFileStream(filePath);
        if (stream == null)
        {
            return new T();
        }

        using (stream)
        {
            return LoadConfiguration(stream, options);
        }
    }

    /// <inheritdoc />
    public override T LoadConfiguration<T>(Stream stream, WritableConfigurationOptions<T> options)
    {
        var jsonDocument = JsonDocument.Parse(stream);
        var root = jsonDocument.RootElement;

        // Navigate to the section if specified
        var sectionName = options.SectionName;
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            var sections = GetSplitedSections(sectionName);
            var current = root;

            foreach (var section in sections)
            {
                if (current.TryGetProperty(section, out var element))
                {
                    current = element;
                }
                else
                {
                    // Section not found, return default instance
                    return new T();
                }
            }

            return JsonSerializer.Deserialize<T>(current.GetRawText(), JsonSerializerOptions)
                ?? new T();
        }

        return JsonSerializer.Deserialize<T>(root.GetRawText(), JsonSerializerOptions) ?? new T();
    }

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
    {
        var contents = GetSaveContents(config, options);
        await FileProvider
            .SaveToFileAsync(options.ConfigFilePath, contents, options.Logger, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the save contents for the configuration.
    /// </summary>
    private ReadOnlyMemory<byte> GetSaveContents<T>(
        T config,
        WritableConfigurationOptions<T> options
    )
        where T : class, new()
    {
        options.Logger?.Log(
            LogLevel.Trace,
            "Serializing configuration of type {ConfigType} to JSON",
            typeof(T).Name
        );

        // Serialize the new configuration
        var serializeNode = JsonSerializer.SerializeToNode<T>(config, JsonSerializerOptions);
        var configNode = serializeNode as JsonObject ?? new JsonObject();

        options.Logger?.Log(
            LogLevel.Trace,
            "Creating nested section structure for section: {SectionName}",
            options.SectionName
        );

        // Create nested section structure if needed
        var nestedSection = CreateNestedSection(options.SectionName, configNode);
        var sNode = JsonSerializer.SerializeToNode(nestedSection, JsonSerializerOptions);
        JsonObject root = sNode as JsonObject ?? [];

        // Convert to bytes
        var jsonString = root?.ToJsonString(JsonSerializerOptions) ?? "{}";
        var bytes = Encoding.GetBytes(jsonString);

        options.Logger?.Log(
            LogLevel.Trace,
            "JSON serialization completed successfully, size: {Size} bytes",
            bytes.Length
        );

        return bytes;
    }
}
