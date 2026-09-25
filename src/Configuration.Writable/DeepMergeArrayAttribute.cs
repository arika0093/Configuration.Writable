using System;

namespace Configuration.Writable;

/// <summary>Configures how an array property is combined during a deep merge.</summary>
/// <remarks>This attribute is read from source-generated metadata for partial <see cref="OptionsModelAttribute"/> types.</remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class DeepMergeArrayAttribute(DeepMergeArrayMode mode) : Attribute
{
    /// <summary>Gets the array merge mode.</summary>
    public DeepMergeArrayMode Mode { get; } = mode;
}
