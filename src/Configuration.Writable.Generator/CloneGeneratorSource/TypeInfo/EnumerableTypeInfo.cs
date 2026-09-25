using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace IDeepCloneable.Generator;

/// <summary>
/// Special type handler for IEnumerable&lt;T&gt; collections.
/// Clones IEnumerable&lt;T&gt; by materializing to List&lt;T&gt; with deep cloned elements.
/// </summary>
internal class EnumerableTypeInfo : SpecialTypeInfo
{
    public override string TargetTypeStartWith => "global::System.Collections.Generic.IEnumerable<";

    public override bool IsMatch(ClassInfo classInfo)
    {
        // Only match if the type itself is IEnumerable<T>, not implementers
        // Implementers with [DeepCloneable] should use their own clone methods
        if (classInfo.NeedsDeepCloneMethod)
            return false;

        // Check if the type name itself is IEnumerable<T> or implements it
        return classInfo.FullClassName.StartsWith(TargetTypeStartWith, StringComparison.Ordinal)
            || classInfo.ImplementedInterfaces.Any(i =>
                i.StartsWith(TargetTypeStartWith, StringComparison.Ordinal)
            );
    }

    public override string GetMethodName(string typeFullName)
    {
        // Use the full type name for the method name, not just the inner type
        return "CloneIEnumerable_" + CodeGenerationUtility.SanitizeTypeName(typeFullName);
    }

    public override IndentedStringBuilder GenerateCloneMethod(
        string typeFullName,
        string methodName,
        List<ClassInfo> allClassInfos,
        IndentedStringBuilder builder,
        CodeGenerator codeGenerator
    )
    {
        var genericParams = CodeGenerationUtility.BuildGenericTypeParameterList(typeFullName);
        builder.AppendLine("");
        builder.AppendLine(CodeTemplateContents.EditorBrowsableAttribute);
        builder.AppendLine(
            $"private static {typeFullName} {methodName}{genericParams}(this {typeFullName} original)"
        );
        builder.AppendLine("{");
        builder.IncreaseIndent();
        ListTypeInfo.GenerateCloneMethodLogicPart(
            typeFullName,
            allClassInfos,
            builder,
            codeGenerator
        );
        builder.AppendLine("return list;");
        builder.DecreaseIndent();
        builder.AppendLine("}");

        return builder;
    }
}
