namespace Configuration.Writable.State;

/// <summary>
/// Exposes the current revision of a state endpoint without decoding its value.
/// </summary>
internal interface IRevisionProvider
{
    string? GetCurrentRevision();
}
