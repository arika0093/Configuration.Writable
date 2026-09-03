namespace Configuration.Writable.FormatProvider;

/// <summary>
/// Indicates that a format provider can persist and inspect options schema metadata.
/// </summary>
public interface IOptionsSchemaMetadataProvider
{
    /// <summary>
    /// Reads schema metadata from the configured section without deserializing the options model.
    /// </summary>
    OptionsSchemaMetadata? ReadSchemaMetadata(IWritableOptionsConfiguration options);
}
