using System;

namespace Configuration.Writable.State;

/// <summary>
/// Specifies which non-successful read results may select a lower-priority state source.
/// </summary>
[Flags]
public enum StateFallbackConditions
{
    /// <summary>Never select a lower-priority source.</summary>
    None = 0,

    /// <summary>Select a lower-priority source when this source has no state.</summary>
    NotFound = 1,

    /// <summary>Select a lower-priority source when this source is temporarily unavailable.</summary>
    Unavailable = 2,
}
