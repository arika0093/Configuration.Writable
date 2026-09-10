using System;

namespace Configuration.Writable;

/// <summary>
/// Provides source-generated schema metadata and migration registration for an options model.
/// </summary>
public interface IGeneratedOptionsMetadata
{
    /// <summary>Gets the stable model identifier, or <see langword="null"/> when unspecified.</summary>
    string? ModelId { get; }

    /// <summary>Gets the schema version. Models that omit it use version 1.</summary>
    int? Version { get; }

    /// <summary>Registers source-generated migrations that lead to this model.</summary>
    void RegisterMigrations(IOptionsMigrationRegistrar registrar);
}

/// <summary>
/// Identifies a source-generated options model that can have a JSON schema exported.
/// </summary>
public sealed record GeneratedOptionsModelMetadata(Type ModelType, string ModelId, int Version);

/// <summary>
/// Receives source-generated options model metadata without scanning assemblies with reflection.
/// </summary>
public static class GeneratedOptionsSchemaRegistry
{
    private static readonly System.Collections.Generic.List<GeneratedOptionsModelMetadata> Models =
    [];
    private static readonly object SyncRoot = new();

    /// <summary>Gets the models registered by source-generated metadata.</summary>
    public static System.Collections.Generic.IReadOnlyList<GeneratedOptionsModelMetadata> RegisteredModels
    {
        get
        {
            lock (SyncRoot)
                return Models.ToArray();
        }
    }

    /// <summary>Registers a source-generated options model.</summary>
    public static void Register(Type modelType, string modelId, int version)
    {
        if (modelType is null)
            throw new ArgumentNullException(nameof(modelType));
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("A model ID is required.", nameof(modelId));
        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version));

        lock (SyncRoot)
        {
            foreach (var model in Models)
            {
                if (
                    model.ModelType == modelType
                    && model.ModelId == modelId
                    && model.Version == version
                )
                    return;
            }

            Models.Add(new GeneratedOptionsModelMetadata(modelType, modelId, version));
        }
    }
}

/// <summary>
/// Receives type-safe migrations emitted by the options model source generator.
/// </summary>
public interface IOptionsMigrationRegistrar
{
    /// <summary>Registers a migration between consecutive generated model versions.</summary>
    void Register<TOld, TNew>(
        Func<TOld, TNew> migrator,
        string modelId,
        int oldVersion,
        int newVersion
    )
        where TOld : class, new()
        where TNew : class, new();
}
