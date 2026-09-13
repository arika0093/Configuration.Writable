using System.Collections.Generic;
using System.Text;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.State;
using VYaml.Serialization;

namespace Configuration.Writable;

/// <summary>
/// YAML file format settings.
/// </summary>
public class YamlFileOptions : FileFormatOptions
{
    /// <summary>
    /// Gets or sets the serializer options used for serialization and deserialization.
    /// </summary>
    public YamlSerializerOptions SerializerOptions { get; set; } = YamlSerializerOptions.Standard;

    /// <summary>
    /// Gets or sets the text encoding used for processing text data.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

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
    public override string FileExtension => "yaml";

    internal override IStateCodec<T> CreateCodec<T>() =>
        new YamlStateCodec<T>(
            SerializerOptions,
            Encoding,
            SchemaVersionProperty,
            SchemaVersionFallbackProperties
        );

    internal override void RegisterType<T>() => YamlCodecSupport.Register<T>();
}
