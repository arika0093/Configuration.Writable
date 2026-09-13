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
        if (provider.GetType() == typeof(JsonAotFormatProvider))
        {
            return CreateJsonAotCodec<T>((JsonAotFormatProvider)provider);
        }
        if (provider.GetType() == typeof(JsonFormatProvider))
        {
            var json = (JsonFormatProvider)provider;
            return new JsonStateCodec<T>(
                json.JsonSerializerOptions,
                typeInfoResolver: null,
                json.SchemaVersionProperty,
                json.SchemaVersionFallbackProperties
            );
        }
        return new LegacyFormatStateCodec<T>();
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
