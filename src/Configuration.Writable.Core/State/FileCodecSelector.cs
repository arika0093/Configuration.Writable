using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.State;

/// <summary>
/// Selects the native state codec for a file-backed registration. Registrations
/// whose format still relies on the legacy provider pipeline fall back to
/// <see cref="LegacyFormatStateCodec{T}"/> until their native codecs land.
/// </summary>
internal static class FileCodecSelector
{
    internal static IStateCodec<T> Select<T>(WritableOptionsConfiguration<T> options)
        where T : class, new()
    {
        // Match exact built-in types only. Test doubles subclass the JSON
        // providers to override serialization; those must stay on the legacy
        // pipeline so their overrides are honored.
        var provider = options.FormatProvider;
        if (
            provider is FallbackFormatProvider fallback
            && TryBuildFallbackCodec<T>(fallback, out var fallbackCodec)
            && fallbackCodec is not null
        )
        {
            return fallbackCodec;
        }
        if (TrySelectNative<T>(provider, out var native) && native is not null)
        {
            return native;
        }
        if (provider is IStateCodecFactory factory)
        {
            return factory.CreateStateCodec<T>();
        }
        return new LegacyFormatStateCodec<T>();
    }

    private static bool TrySelectNative<T>(
        IWritableFormatProvider provider,
        [NotNullWhen(true)] out IStateCodec<T>? codec
    )
        where T : class, new()
    {
        if (provider.GetType() == typeof(JsonAotFormatProvider))
        {
            codec = CreateJsonAotCodec<T>((JsonAotFormatProvider)provider);
            return true;
        }
        if (provider.GetType() == typeof(JsonFormatProvider))
        {
            var json = (JsonFormatProvider)provider;
            codec = new JsonStateCodec<T>(
                json.JsonSerializerOptions,
                typeInfoResolver: null,
                json.SchemaVersionProperty,
                json.SchemaVersionFallbackProperties
            );
            return true;
        }
        codec = null;
        return false;
    }

    private static bool TryBuildFallbackCodec<T>(
        FallbackFormatProvider fallback,
        [NotNullWhen(true)] out IStateCodec<T>? codec
    )
        where T : class, new()
    {
        codec = null;
        if (!TrySelectMemberCodec<T>(fallback.PrimaryProvider, out var primary) || primary is null)
        {
            return false;
        }

        var members = new List<(string Extension, IStateCodec<T> Codec)>();
        foreach (var member in fallback.FallbackProviders)
        {
            if (
                string.IsNullOrWhiteSpace(member.FileExtension)
                || !TrySelectMemberCodec<T>(member, out var memberCodec)
                || memberCodec is null
            )
            {
                return false;
            }

            members.Add((member.FileExtension, memberCodec));
        }

        codec = new FallbackStateCodec<T>(primary, members);
        return true;
    }

    private static bool TrySelectMemberCodec<T>(
        IWritableFormatProvider provider,
        [NotNullWhen(true)] out IStateCodec<T>? codec
    )
        where T : class, new()
    {
        if (TrySelectNative<T>(provider, out codec))
        {
            return true;
        }

        if (
            provider is IStateCodecFactory factory
            && factory.CreateStateCodec<T>() is { } created
            && created is not LegacyFormatStateCodec<T>
        )
        {
            codec = created;
            return true;
        }

        codec = null;
        return false;
    }

    private static JsonStateCodec<T> CreateJsonAotCodec<T>(JsonAotFormatProvider provider)
        where T : class, new()
    {
        var (effectiveOptions, typeInfoResolver) = provider.GetCodecSettings();
        return new JsonStateCodec<T>(
            effectiveOptions,
            typeInfoResolver,
            provider.SchemaVersionProperty,
            provider.SchemaVersionFallbackProperties
        );
    }
}
