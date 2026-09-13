namespace Configuration.Writable.State;

/// <summary>
/// Implemented by format providers that can build their native state codec.
/// Lets the file codec selector route to native codecs without Core
/// referencing the provider's assembly.
/// </summary>
internal interface IStateCodecFactory
{
    IStateCodec<T> CreateStateCodec<T>(WritableOptionsConfiguration<T> options)
        where T : class, new();
}
