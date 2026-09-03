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
