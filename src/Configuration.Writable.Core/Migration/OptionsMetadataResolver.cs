using System;

namespace Configuration.Writable.Migration;

internal static class OptionsMetadataResolver
{
    public static OptionsSchemaMetadata? Resolve<T>()
        where T : class, new()
    {
        var instance = new T();
        var generated = instance as IGeneratedOptionsMetadata;
        var legacy = instance as IHasVersion;
        var generatedVersion = generated?.Version;
        var legacyVersion = legacy?.Version;

        if (
            generatedVersion is not null
            && legacyVersion is not null
            && generatedVersion != legacyVersion
        )
        {
            throw new InvalidOperationException(
                $"Generated version {generatedVersion} for {typeof(T).Name} does not match its legacy IHasVersion value {legacyVersion}."
            );
        }

        var version = generatedVersion ?? legacyVersion;
        if (version is <= 0)
        {
            throw new InvalidOperationException(
                $"Schema version for {typeof(T).Name} must be greater than zero."
            );
        }

        var modelId = generated?.ModelId;
        if (modelId is not null && string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException(
                $"Schema model ID for {typeof(T).Name} cannot be empty."
            );
        }
        return modelId is null && version is null
            ? null
            : new OptionsSchemaMetadata(modelId, version);
    }
}
