using System;

namespace Configuration.Writable;

/// <summary>
/// Indicates that the attributed class or struct is a writable options model.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    Inherited = false,
    AllowMultiple = false
)]
public sealed class OptionsModelAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the stable identifier shared by every version of this options model.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the positive schema version.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets whether migration from the immediately preceding version is supported.
    /// Set this to <see langword="false"/> to start a new compatibility chain at this version.
    /// </summary>
    public bool SupportMigration { get; set; } = true;
}
