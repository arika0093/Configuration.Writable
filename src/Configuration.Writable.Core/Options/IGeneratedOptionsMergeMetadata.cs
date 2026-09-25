using System;

namespace Configuration.Writable;

/// <summary>
/// Provides source-generated property metadata used when merging sparse configuration documents.
/// </summary>
public interface IGeneratedOptionsMergeMetadata
{
    /// <summary>
    /// Gets metadata for a serialized property on a model type.
    /// </summary>
    /// <param name="declaringType">The type that declares the property.</param>
    /// <param name="propertyName">The serialized or CLR property name.</param>
    /// <param name="propertyType">The CLR type of the property.</param>
    /// <param name="arrayMode">The array merge mode configured for the property.</param>
    /// <returns><see langword="true"/> when the property is known to the generated model metadata.</returns>
    bool TryGetPropertyMetadata(
        Type declaringType,
        string propertyName,
        out Type propertyType,
        out DeepMergeArrayMode arrayMode
    );
}
