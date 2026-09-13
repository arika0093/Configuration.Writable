using System.Collections.Generic;
using Configuration.Writable.State;

namespace Configuration.Writable;

/// <summary>
/// XML file format settings.
/// </summary>
public class XmlFileOptions : FileFormatOptions
{
    /// <summary>
    /// Gets or sets the property name used to persist the schema version.
    /// </summary>
    public string SchemaVersionProperty { get; set; } = "Version";

    /// <summary>
    /// Gets or sets the property names accepted when reading schema versions
    /// saved with earlier settings.
    /// </summary>
    public IReadOnlyList<string> SchemaVersionFallbackProperties { get; set; } = [];

    /// <inheritdoc />
    public override string FileExtension => "xml";

    internal override IStateCodec<T> CreateCodec<T>() =>
        new XmlStateCodec<T>(SchemaVersionProperty, SchemaVersionFallbackProperties);
}
