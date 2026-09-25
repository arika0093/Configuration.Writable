using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace IDeepCloneable.Generator;

/// <summary>
/// Base class for special collection types that require custom clone methods.
/// </summary>
internal abstract class SpecialTypeInfo
{
    /// <summary>Target type prefix to match against.</summary>
    public abstract string TargetTypeStartWith { get; }

    /// <summary>Checks if the given type matches this special type, with access to class metadata.</summary>
    public virtual bool IsMatch(ClassInfo classInfo) =>
        classInfo.FullClassName.StartsWith(TargetTypeStartWith, StringComparison.Ordinal);

    /// <summary>Generates the method name for cloning this special type.</summary>
    public abstract string GetMethodName(string typeFullName);

    /// <summary>Generates the clone method for this special type.</summary>
    public abstract IndentedStringBuilder GenerateCloneMethod(
        string typeFullName,
        string methodName,
        List<ClassInfo> allClassInfos,
        IndentedStringBuilder builder,
        CodeGenerator codeGenerator
    );
}
