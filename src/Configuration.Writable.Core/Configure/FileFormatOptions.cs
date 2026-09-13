using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Configuration.Writable.State;

namespace Configuration.Writable;

/// <summary>
/// Selects the file format used to persist writable options. Replaces the
/// former format-provider objects; the State source runtime builds a native
/// codec from these settings.
/// </summary>
public abstract class FileFormatOptions
{
    /// <summary>
    /// Gets the file extension associated with the format, excluding the leading period.
    /// </summary>
    public abstract string FileExtension { get; }

    internal abstract IStateCodec<T> CreateCodec<T>()
        where T : class, new();

    internal virtual void RegisterType<T>()
        where T : class, new() { }
}

/// <summary>
/// JSON file format settings. Uses runtime JSON metadata by default; set
/// <see cref="TypeInfoResolver"/> to a source-generated context for NativeAOT.
/// </summary>
public class JsonFileOptions : FileFormatOptions
{
    /// <summary>
    /// Gets or sets the options used for JSON serialization. When null, the
    /// options are derived from <see cref="TypeInfoResolver"/> (or defaults).
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets the source-generated type info resolver for NativeAOT.
    /// When null, runtime JSON metadata is used.
    /// </summary>
    public IJsonTypeInfoResolver? TypeInfoResolver { get; set; }

    /// <summary>
    /// Gets or sets the property name used to persist the schema version.
    /// </summary>
    public string SchemaVersionProperty { get; set; } = "$version";

    /// <summary>
    /// Gets or sets the property names accepted when reading schema versions
    /// saved with earlier settings.
    /// </summary>
    public IReadOnlyList<string> SchemaVersionFallbackProperties { get; set; } = ["Version"];

    /// <inheritdoc />
    public override string FileExtension => "json";

    internal override IStateCodec<T> CreateCodec<T>()
    {
        var (effectiveOptions, resolver) = GetEffectiveSettings();
        return new JsonStateCodec<T>(
            effectiveOptions,
            resolver,
            SchemaVersionProperty,
            SchemaVersionFallbackProperties
        );
    }

    internal (
        JsonSerializerOptions EffectiveOptions,
        IJsonTypeInfoResolver? Resolver
    ) GetEffectiveSettings()
    {
        if (JsonSerializerOptions != null)
        {
            return (JsonSerializerOptions, TypeInfoResolver);
        }

        if (TypeInfoResolver is JsonSerializerContext context)
        {
            return (context.Options, TypeInfoResolver);
        }

        if (TypeInfoResolver != null)
        {
            return (
                new JsonSerializerOptions
                {
                    TypeInfoResolver = TypeInfoResolver,
                    WriteIndented = false,
                },
                TypeInfoResolver
            );
        }

        return (new JsonSerializerOptions { WriteIndented = false }, null);
    }
}
