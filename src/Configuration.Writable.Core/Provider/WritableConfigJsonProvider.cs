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
        where T : class
    {
        var filePath = options.ConfigFilePath;
        if (!FileProvider.FileExists(filePath))
        {
            return Activator.CreateInstance<T>();
        }

        var stream = FileProvider.GetFileStream(filePath);
        if (stream == null)
        {
            return Activator.CreateInstance<T>();
        }

        using (stream)
        {
            return LoadConfiguration(stream, options);
        }
    }

    /// <inheritdoc />
    public override T LoadConfiguration<T>(Stream stream, WritableConfigurationOptions<T> options)
        where T : class
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
                    return Activator.CreateInstance<T>();
                }
            }

            return JsonSerializer.Deserialize<T>(current.GetRawText(), JsonSerializerOptions)
                ?? Activator.CreateInstance<T>();
        }

        return JsonSerializer.Deserialize<T>(root.GetRawText(), JsonSerializerOptions)
            ?? Activator.CreateInstance<T>();
    }

    /// <inheritdoc />
    public override async Task SaveAsync<T>(
        T config,
        OptionOperations<T> operations,
        WritableConfigurationOptions<T> options,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var contents = GetSaveContentsCore(config, operations, options);
        await FileProvider
            .SaveToFileAsync(options.ConfigFilePath, contents, cancellationToken, options.Logger)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Core method to get save contents with optional operations.
    /// </summary>
    private ReadOnlyMemory<byte> GetSaveContentsCore<T>(
        T config,
        OptionOperations<T> operations,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        options.Logger?.Log(
            LogLevel.Trace,
            "Serializing configuration of type {ConfigType} to JSON",
            typeof(T).Name
        );

        // Serialize the new configuration
        var serializeNode = JsonSerializer.SerializeToNode<T>(config, JsonSerializerOptions);
        var configNode = serializeNode as JsonObject ?? new JsonObject();

        // Apply deletion operations
        if (operations.HasOperations)
        {
            foreach (var keyToDelete in operations.KeysToDelete)
            {
                DeleteKeyFromNode(configNode, keyToDelete, options);
            }
        }

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

    /// <summary>
    /// Deletes a key from the JSON node based on the property path.
    /// </summary>
    /// <param name="node">The JSON node to modify.</param>
    /// <param name="keyPath">The property path to delete (e.g., "Parent:Child").</param>
    /// <param name="options">The configuration options.</param>
    private void DeleteKeyFromNode<T>(
        JsonObject node,
        string keyPath,
        WritableConfigurationOptions<T> options
    )
        where T : class
    {
        var parts = keyPath.Split(':');
        if (parts.Length == 0)
        {
            return;
        }

        // Navigate to the parent node
        JsonObject? current = node;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            // Apply naming policy if present
            var partName = ApplyNamingPolicy(parts[i]);
            if (current.TryGetPropertyValue(partName, out var value) && value is JsonObject obj)
            {
                current = obj;
            }
            else
            {
                // Path doesn't exist, nothing to delete
                options.Logger?.LogDebug(
                    "Key path {KeyPath} not found for deletion, skipping",
                    keyPath
                );
                return;
            }
        }

        // Delete the final key
        var finalKey = ApplyNamingPolicy(parts[^1]);
        if (current.Remove(finalKey))
        {
            options.Logger?.LogDebug("Deleted key {KeyPath} from configuration", keyPath);
        }
        else
        {
            options.Logger?.LogDebug("Key {KeyPath} not found for deletion, skipping", keyPath);
        }
    }

    /// <summary>
    /// Applies the JSON naming policy to a property name.
    /// </summary>
    /// <param name="name">The property name to convert.</param>
    /// <returns>The converted property name.</returns>
    private string ApplyNamingPolicy(string name)
    {
        return JsonSerializerOptions.PropertyNamingPolicy?.ConvertName(name) ?? name;
    }
}
