namespace Configuration.Writable.State;

/// <summary>
/// Implemented by format providers that can build their native state codec.
/// Lets the file codec selector route to native codecs without Core
/// referencing the provider's assembly. Implementations return a
/// <see cref="LegacyFormatStateCodec{T}"/> when subclassed so test doubles
/// overriding serialization stay on the legacy pipeline.
/// </summary>
internal interface IStateCodecFactory
{
    IStateCodec<T> CreateStateCodec<T>()
        where T : class, new();
}
