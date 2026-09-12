namespace Configuration.Writable.State;

/// <summary>
/// Describes whether a state backend produced a usable snapshot.
/// </summary>
internal enum StateReadStatus
{
    /// <summary>A value was read successfully.</summary>
    Success,

    /// <summary>The backend does not currently contain a value.</summary>
    NotFound,

    /// <summary>The backend is temporarily unavailable.</summary>
    Unavailable,
}
