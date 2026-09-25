using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace IDeepCloneable.Generator;

/// <summary>
/// Special type handler for ReadOnlyCollection&lt;T&gt; collections.
/// </summary>
internal class ReadOnlyCollectionTypeInfo : SpecialTypeInfo
{
    public override string TargetTypeStartWith =>
        "global::System.Collections.ObjectModel.ReadOnlyCollection<";

    public override string GetMethodName(string typeFullName)
    {
        return "CloneReadOnlyCollection_" + CodeGenerationUtility.SanitizeTypeName(typeFullName);
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
        // ReadOnlyCollection wraps a List, but we can just create a new one with the same items
        ListTypeInfo.GenerateCloneMethodLogicPart(
            typeFullName,
            allClassInfos,
            builder,
            codeGenerator
        );
        builder.AppendLine($"return new {typeFullName}(list);");
        builder.DecreaseIndent();
        builder.AppendLine("}");

        return builder;
    }
}
