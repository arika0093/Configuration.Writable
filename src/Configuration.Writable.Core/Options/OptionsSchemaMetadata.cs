namespace Configuration.Writable;

/// <summary>
/// Identifies the schema stored in a writable configuration document.
/// </summary>
/// <param name="ModelId">The stable model identifier, or <see langword="null"/> when unavailable.</param>
/// <param name="Version">The positive schema version, or <see langword="null"/> when unavailable.</param>
public sealed record OptionsSchemaMetadata(string? ModelId, int? Version) { }
