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
internal sealed class FallbackFormatProvider : IWritableFormatProvider, IOptionsSchemaMetadataProvider
{
    private readonly List<IWritableFormatProvider> _fallbackProviders = [];

    internal FallbackFormatProvider(IWritableFormatProvider primaryProvider)
    {
        PrimaryProvider = primaryProvider ?? throw new ArgumentNullException(nameof(primaryProvider));
    }

    internal IWritableFormatProvider PrimaryProvider { get; }

    internal IReadOnlyList<IWritableFormatProvider> FallbackProviders => _fallbackProviders;

    public string FileExtension => PrimaryProvider.FileExtension;

    internal void AddFallback(IWritableFormatProvider provider)
    {
        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        var extension = NormalizeExtension(provider.FileExtension);
        if (string.IsNullOrEmpty(extension))
        {
            throw new ArgumentException("Fallback format providers must declare a file extension.", nameof(provider));
        }

        if (string.Equals(extension, NormalizeExtension(PrimaryProvider.FileExtension), StringComparison.OrdinalIgnoreCase)
            || _fallbackProviders.Any(existing => string.Equals(
                extension,
                NormalizeExtension(existing.FileExtension),
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A format provider for '.{extension}' is already registered.");
        }

        _fallbackProviders.Add(provider);
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
        where T : class, new() => PrimaryProvider.SaveAsync(config, options, cancellationToken);

    public OptionsSchemaMetadata? ReadSchemaMetadata(IWritableOptionsConfiguration options)
    {
        var source = ResolveReadSource(options);
        return source.Provider is IOptionsSchemaMetadataProvider metadataProvider
            ? metadataProvider.ReadSchemaMetadata(source.Options)
            : null;
    }

    internal void PromoteIfNeeded<T>(T config, IWritableOptionsConfiguration options)
        where T : class, new()
    {
        if (options.FileProvider.FileExists(options.ConfigFilePath))
        {
            return;
        }

        if (ResolveFallbackSource(options) is null)
        {
            return;
        }

        // Persist the fully migrated/current model with the canonical provider. The fallback
        // remains as a compatibility backup, but will no longer participate in resolution
        // while the canonical file exists.
        PrimaryProvider.SaveAsync(config, options).GetAwaiter().GetResult();
    }

    private ResolvedSource ResolveReadSource(IWritableOptionsConfiguration options)
    {
        if (options.FileProvider.FileExists(options.ConfigFilePath))
        {
            return new ResolvedSource(PrimaryProvider, options);
        }

        return ResolveFallbackSource(options)
            ?? new ResolvedSource(PrimaryProvider, options);
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
        public TimeSpan OnChangeDebounce => source.OnChangeDebounce;
        public ILogger? Logger => source.Logger;
    }
}
