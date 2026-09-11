using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.FormatProvider;

/// <summary>
/// Wraps the canonical format provider with additional providers that may be used to read
/// an existing configuration when the canonical file does not exist.
/// </summary>
internal sealed class FallbackFormatProvider
    : IWritableFormatProvider,
        IOptionsSchemaMetadataProvider
{
    private readonly List<IWritableFormatProvider> _fallbackProviders = [];

    internal FallbackFormatProvider(IWritableFormatProvider primaryProvider)
    {
        PrimaryProvider =
            primaryProvider ?? throw new ArgumentNullException(nameof(primaryProvider));
    }

    internal IWritableFormatProvider PrimaryProvider { get; }

    internal IReadOnlyList<IWritableFormatProvider> FallbackProviders => _fallbackProviders;

    internal bool SupportsSchemaMetadata =>
        SupportsProviderSchemaMetadata(PrimaryProvider)
        && _fallbackProviders.All(SupportsProviderSchemaMetadata);

    public string SchemaVersionProperty
    {
        get => PrimaryProvider.SchemaVersionProperty;
        set => PrimaryProvider.SchemaVersionProperty = value;
    }

    public IReadOnlyList<string> SchemaVersionFallbackProperties
    {
        get => PrimaryProvider.SchemaVersionFallbackProperties;
        set => PrimaryProvider.SchemaVersionFallbackProperties = value;
    }

    public string FileExtension => PrimaryProvider.FileExtension;

    internal void RegisterType<T>()
        where T : class, new()
    {
        if (PrimaryProvider is FormatProviderBase primaryProvider)
        {
            primaryProvider.RegisterType<T>();
        }

        foreach (var fallbackProvider in _fallbackProviders)
        {
            if (fallbackProvider is FormatProviderBase provider)
            {
                provider.RegisterType<T>();
            }
        }
    }

    internal FallbackFormatProvider Clone()
    {
        var clone = new FallbackFormatProvider(PrimaryProvider);
        clone._fallbackProviders.AddRange(_fallbackProviders);
        return clone;
    }

    internal void AddFallbacks(IEnumerable<IWritableFormatProvider> providers)
    {
        var candidates = providers.ToList();
        var knownExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeExtension(PrimaryProvider.FileExtension),
        };

        foreach (var existing in _fallbackProviders)
        {
            knownExtensions.Add(NormalizeExtension(existing.FileExtension));
        }

        foreach (var provider in candidates)
        {
            var extension = NormalizeExtension(provider.FileExtension);
            if (string.IsNullOrEmpty(extension))
            {
                throw new ArgumentException(
                    "Fallback format providers must declare a file extension.",
                    nameof(providers)
                );
            }

            if (!knownExtensions.Add(extension))
            {
                throw new InvalidOperationException(
                    $"A format provider for '.{extension}' is already registered."
                );
            }
        }

        _fallbackProviders.AddRange(candidates);
    }

    public object LoadConfiguration(Type type, IWritableOptionsConfiguration options)
    {
        var source = ResolveReadSource(options);
        return source.Provider.LoadConfiguration(type, source.Options);
    }

    public ValueTask<object> LoadConfigurationAsync(
        Type type,
        PipeReader reader,
        List<string> sectionNameParts,
        CancellationToken cancellationToken = default
    ) => PrimaryProvider.LoadConfigurationAsync(type, reader, sectionNameParts, cancellationToken);

    public Task SaveAsync<T>(
        T config,
        IWritableOptionsConfiguration options,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        var source = ResolveReadSource(options);
        return options.SectionNameParts.Count > 0 && source.Provider != PrimaryProvider
            ? source.Provider.SaveAsync(config, source.Options, cancellationToken)
            : PrimaryProvider.SaveAsync(config, options, cancellationToken);
    }

    public OptionsSchemaMetadata? ReadSchemaMetadata(IWritableOptionsConfiguration options)
    {
        var source = ResolveReadSource(options);
        return source.Provider is IOptionsSchemaMetadataProvider metadataProvider
            ? FormatProviderBase.ExecuteWithBackupRecovery(
                source.Options,
                () => metadataProvider.ReadSchemaMetadata(source.Options)
            )
            : null;
    }

    internal string GetSelectedFilePath(IWritableOptionsConfiguration options) =>
        ResolveReadSource(options).Options.ConfigFilePath;

    internal void ValidateConfigurationPath(
        string canonicalPath,
        IWritableFileProvider fileProvider
    )
    {
        var pathComparison =
            fileProvider is IPhysicalFileProvider && Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        foreach (var extension in _fallbackProviders.Select(provider => provider.FileExtension))
        {
            var fallbackPath = Path.ChangeExtension(canonicalPath, extension);
            if (!string.Equals(canonicalPath, fallbackPath, pathComparison))
            {
                continue;
            }

            var canonicalExtension = NormalizeExtension(PrimaryProvider.FileExtension);
            var fallbackExtension = NormalizeExtension(extension);
            var extensionlessPath = Path.ChangeExtension(canonicalPath, null);

            throw new InvalidOperationException(
                $"""
                The canonical configuration path '{canonicalPath}' conflicts with the registered fallback format '.{fallbackExtension}'.

                Canonical path: '{canonicalPath}'
                Fallback path:  '{fallbackPath}'

                Both resolve to the same file, so the fallback provider can never be selected.

                Canonical format: '.{canonicalExtension}'
                Fallback format:  '.{fallbackExtension}'

                Use an extensionless file path such as '{extensionlessPath}', or specify a file extension that does not conflict with any registered fallback format.
                """
            );
        }
    }

    internal void PromoteIfNeeded<T>(T config, IWritableOptionsConfiguration options)
        where T : class, new()
    {
        // A partial write against a missing canonical file cannot preserve sibling sections
        // from the fallback document. Keep resolving the fallback until a complete document
        // can be promoted by the normal save path.
        if (
            options.SectionNameParts.Count > 0
            || options.FileProvider.FileExists(options.ConfigFilePath)
        )
        {
            return;
        }

        var source = ResolveFallbackSource(options);
        if (source is null)
        {
            return;
        }

        // Preserve the source document before promoting it to the canonical format. This is
        // especially important when the fallback is a versioned configuration that has just
        // been migrated: the canonical write must not be the only copy of the original data.
        var backedUp = false;
        if (
            options.FileProvider is IBackupFileProvider backupFileProvider
            && backupFileProvider.TryBackup(
                source.Options.ConfigFilePath,
                out var backupPath,
                options.Logger
            )
        )
        {
            backedUp = true;
            options.Logger?.LogDebug(
                "Backed up fallback configuration before promoting it: {BackupPath}",
                backupPath
            );
        }

        // Persist the fully migrated/current model with the canonical provider. The fallback
        // remains available for compatibility, but will no longer participate in resolution
        // while the canonical file exists.
        PrimaryProvider.SaveAsync(config, options).GetAwaiter().GetResult();

        // Once the canonical file has been written successfully, remove the source file. Keep
        // it when backup was unavailable or failed so a failed/unsupported migration remains
        // recoverable.
        if (
            backedUp
            && options.FileProvider is IFileDeleter fileDeleter
            && !fileDeleter.TryDelete(source.Options.ConfigFilePath, options.Logger)
        )
        {
            options.Logger?.LogWarning(
                "The fallback configuration was promoted, but the source file could not be deleted: {Path}",
                source.Options.ConfigFilePath
            );
        }
    }

    private ResolvedSource ResolveReadSource(IWritableOptionsConfiguration options)
    {
        RestorePrimaryBackupIfNeeded(options);
        if (options.FileProvider.FileExists(options.ConfigFilePath))
        {
            return new ResolvedSource(PrimaryProvider, options);
        }

        return ResolveFallbackSource(options) ?? new ResolvedSource(PrimaryProvider, options);
    }

    private static void RestorePrimaryBackupIfNeeded(IWritableOptionsConfiguration options)
    {
        if (
            !options.FileProvider.FileExists(options.ConfigFilePath)
            && options.FileProvider is CommonFileProvider fileProvider
        )
        {
            fileProvider.TryRestoreLatestBackup(options.ConfigFilePath, options.Logger);
        }
    }

    private ResolvedSource? ResolveFallbackSource(IWritableOptionsConfiguration options)
    {
        foreach (var provider in _fallbackProviders)
        {
            var fallbackPath = Path.ChangeExtension(options.ConfigFilePath, provider.FileExtension);
            if (!options.FileProvider.FileExists(fallbackPath))
            {
                continue;
            }

            return new ResolvedSource(
                provider,
                new ConfigurationView(options, provider, fallbackPath)
            );
        }

        return null;
    }

    private static string NormalizeExtension(string extension) => extension.Trim().TrimStart('.');

    private static bool SupportsProviderSchemaMetadata(IWritableFormatProvider provider) =>
        provider is FallbackFormatProvider fallbackProvider
            ? fallbackProvider.SupportsSchemaMetadata
            : provider is IOptionsSchemaMetadataProvider;

    private sealed record ResolvedSource(
        IWritableFormatProvider Provider,
        IWritableOptionsConfiguration Options
    );

    private sealed class ConfigurationView(
        IWritableOptionsConfiguration source,
        IWritableFormatProvider formatProvider,
        string configFilePath
    ) : IWritableOptionsConfiguration
    {
        public IWritableFileProvider FileProvider => source.FileProvider;
        public IWritableFormatProvider FormatProvider => formatProvider;
        public string ConfigFilePath => configFilePath;
        public string InstanceName => source.InstanceName;
        public List<string> SectionNameParts => source.SectionNameParts;
        public OptionsSchemaMetadata? SchemaMetadata => source.SchemaMetadata;
        public string? SchemaBaseUri => source.SchemaBaseUri;
        public TimeSpan OnChangeDebounce => source.OnChangeDebounce;
        public ILogger? Logger => source.Logger;
    }
}
