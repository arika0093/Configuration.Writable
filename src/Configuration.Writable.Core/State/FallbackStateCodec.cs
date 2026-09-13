using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.State;

/// <summary>
/// Native fallback state codec. Resolves the effective document across the
/// canonical format and registered fallback formats, mirroring
/// <c>FallbackFormatProvider</c> without the legacy provider pipeline.
/// </summary>
internal sealed class FallbackStateCodec<T> : IStateCodec<T>
    where T : class, new()
{
    private readonly IStateCodec<T> _primary;
    private readonly IReadOnlyList<FallbackEntry> _fallbacks;

    internal FallbackStateCodec(
        IStateCodec<T> primary,
        IReadOnlyList<(string Extension, IStateCodec<T> Codec)> fallbacks
    )
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        var entries = new List<FallbackEntry>();
        foreach (var (extension, codec) in fallbacks)
        {
            entries.Add(
                new FallbackEntry(
                    NormalizeExtension(extension),
                    codec ?? throw new ArgumentNullException(nameof(fallbacks))
                )
            );
        }
        _fallbacks = entries;
    }

    public async ValueTask<T> ReadAsync(
        IStateResource resource,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileResource = GetFileResource(resource);
        var options = fileResource.Options;
        var canonical = ResolveCanonicalPath(fileResource, options);
        RestorePrimaryBackupIfNeeded(fileResource, canonical, options);

        var (codec, selectedPath) = ResolveCodec(fileResource, canonical);
        var selectedOptions = options with
        {
            ConfigFilePath = selectedPath,
            ReadFilePath = selectedPath,
        };
        var selectedResource = new FileStateResource<T>(selectedOptions);
        var value = await codec
            .ReadAsync(selectedResource, cancellationToken)
            .ConfigureAwait(false);

        PromoteIfNeeded(value, fileResource, canonical, selectedPath);
        return value;
    }

    public async ValueTask WriteAsync(
        T value,
        IStateResource resource,
        CancellationToken cancellationToken = default
    )
    {
        var fileResource = GetFileResource(resource);
        var options = fileResource.Options;

        // A partial write against a fallback document must target the fallback
        // file so sibling sections are preserved by the fallback codec.
        if (options.SectionNameParts.Count > 0)
        {
            var canonical = options.ConfigFilePath;
            if (!fileResource.FileExists(canonical))
            {
                foreach (var fallback in _fallbacks)
                {
                    var fallbackPath = Path.ChangeExtension(canonical, fallback.Extension);
                    if (!fileResource.FileExists(fallbackPath))
                    {
                        continue;
                    }

                    var fallbackOptions = options with
                    {
                        ConfigFilePath = fallbackPath,
                        ReadFilePath = fallbackPath,
                    };
                    await fallback
                        .Codec.WriteAsync(
                            value,
                            new FileStateResource<T>(fallbackOptions),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    return;
                }
            }
        }

        await _primary.WriteAsync(value, resource, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveCanonicalPath(
        FileStateResource<T> resource,
        WritableOptionsConfiguration<T> options
    ) =>
        options.PromoteSaveLocationEnabled && resource.FileExists(options.ConfigFilePath)
            ? options.ConfigFilePath
            : options.ReadFilePath;

    private (IStateCodec<T> Codec, string Path) ResolveCodec(
        FileStateResource<T> resource,
        string canonical
    )
    {
        if (resource.FileExists(canonical))
        {
            return (_primary, canonical);
        }

        foreach (var fallback in _fallbacks)
        {
            var fallbackPath = Path.ChangeExtension(canonical, fallback.Extension);
            if (resource.FileExists(fallbackPath))
            {
                return (fallback.Codec, fallbackPath);
            }
        }

        return (_primary, canonical);
    }

    private static void RestorePrimaryBackupIfNeeded(
        FileStateResource<T> resource,
        string canonical,
        WritableOptionsConfiguration<T> options
    )
    {
        if (!resource.FileExists(canonical))
        {
            resource.Backend.TryRestoreLatestBackup(canonical, options.Logger);
        }
    }

    private void PromoteIfNeeded(
        T value,
        FileStateResource<T> resource,
        string canonical,
        string selectedPath
    )
    {
        var options = resource.Options;
        // A partial write against a missing canonical file cannot preserve sibling
        // sections from the fallback document. Keep resolving the fallback until a
        // complete document can be promoted by the normal save path.
        if (
            options.SectionNameParts.Count > 0
            || string.Equals(selectedPath, canonical, StringComparison.Ordinal)
            || resource.FileExists(canonical)
        )
        {
            return;
        }

        // Preserve the source document before promoting it to the canonical format.
        var backedUp = false;
        if (options.FileBackend.TryBackup(selectedPath, out var backupPath, options.Logger))
        {
            backedUp = true;
            options.Logger?.LogDebug(
                "Backed up fallback configuration before promoting it: {BackupPath}",
                backupPath
            );
        }

        // Persist the fully migrated/current model with the canonical codec. The
        // fallback remains available for compatibility, but will no longer
        // participate in resolution while the canonical file exists.
        // Like the legacy pipeline, the promotion targets the resolved
        // canonical (read) path; save-location promotion is handled separately
        // by the file state source.
        var canonicalOptions = options with
        {
            ConfigFilePath = canonical,
            ReadFilePath = canonical,
        };
        _primary
            .WriteAsync(value, new FileStateResource<T>(canonicalOptions), CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        // Once the canonical file has been written successfully, remove the source
        // file. Keep it when backup was unavailable or failed so a failed
        // migration remains recoverable.
        if (backedUp && !options.FileBackend.TryDelete(selectedPath, options.Logger))
        {
            options.Logger?.LogWarning(
                "The fallback configuration was promoted, but the source file could not be deleted: {Path}",
                selectedPath
            );
        }
    }

    private static string NormalizeExtension(string extension) => extension.Trim().TrimStart('.');

    private static FileStateResource<T> GetFileResource(IStateResource resource) =>
        resource as FileStateResource<T>
        ?? throw new ArgumentException(
            "The fallback state codec requires a file state resource.",
            nameof(resource)
        );

    private sealed record FallbackEntry(string Extension, IStateCodec<T> Codec);
}
