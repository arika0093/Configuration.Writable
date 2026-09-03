namespace Configuration.Writable;

/// <summary>
/// Identifies the schema stored in a writable configuration document.
/// </summary>
/// <param name="ModelId">The stable model identifier, or <see langword="null"/> when unavailable.</param>
/// <param name="Version">The positive schema version, or <see langword="null"/> for an unversioned model.</param>
public sealed record OptionsSchemaMetadata(string? ModelId, int? Version)
{
    /// <summary>The reserved persisted property name for the model identifier.</summary>
    public const string ModelIdPropertyName = "ModelId";

    /// <summary>The reserved persisted property name for the schema version.</summary>
    public const string VersionPropertyName = "Version";
}
