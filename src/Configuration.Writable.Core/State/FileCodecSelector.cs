using System.Linq;

namespace Configuration.Writable.State;

/// <summary>
/// Selects the native state codec for a file-backed registration from its
/// configured <see cref="FileFormatOptions"/>.
/// </summary>
internal static class FileCodecSelector
{
    internal static IStateCodec<T> Select<T>(WritableOptionsConfiguration<T> options)
        where T : class, new()
    {
        var primary = options.FormatOptions.CreateCodec<T>();
        if (options.FallbackFormats.Count == 0)
        {
            return primary;
        }

        var members = options
            .FallbackFormats.Select(format =>
                (format.FileExtension, Codec: format.CreateCodec<T>())
            )
            .ToList();
        return new FallbackStateCodec<T>(primary, members);
    }
}
