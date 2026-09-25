namespace Configuration.Writable;

/// <summary>Specifies how an array property is combined when deep-merging configuration documents.</summary>
public enum DeepMergeArrayMode
{
    /// <summary>The incoming array replaces the existing array.</summary>
    Replace = 0,

    /// <summary>The incoming array is appended to the existing array.</summary>
    Append = 1,

    /// <summary>Incoming elements are appended only when an equal element is not already present.</summary>
    UniqueAppend = 2,
}
