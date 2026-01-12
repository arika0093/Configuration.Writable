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
public class OptionsModelAttribute : Attribute;
