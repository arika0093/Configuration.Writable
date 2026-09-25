using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace IDeepCloneable.Generator;

/// <summary>
/// Special type handler for PriorityQueue&lt;TElement, TPriority&gt; collections.
/// </summary>
internal class PriorityQueueTypeInfo : SpecialTypeInfo
{
    public override string TargetTypeStartWith =>
        "global::System.Collections.Generic.PriorityQueue<";

    public override string GetMethodName(string typeFullName)
    {
        return "ClonePriorityQueue_" + CodeGenerationUtility.SanitizeTypeName(typeFullName);
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
        var genericArgs = CodeGenerationUtility.ExtractGenericType(typeFullName);
        var parts = CodeGenerationUtility.SplitGenericArgs(genericArgs);

        if (parts.Count != 2)
            return builder;

        var elementType = parts[0];
        var priorityType = parts[1];
        var elementIsImmutable = CodeGenerationUtility.IsTypeImmutable(elementType);
        var priorityIsImmutable = CodeGenerationUtility.IsTypeImmutable(priorityType);

        builder.AppendLine("");
        builder.AppendLine(CodeTemplateContents.EditorBrowsableAttribute);
        builder.AppendLine(
            $"private static {typeFullName} {methodName}{genericParams}(this {typeFullName} original)"
        );
        builder.AppendLine("{");
        builder.IncreaseIndent();
        builder.AppendLine("if (original == null) return null;");
        builder.AppendLine($"var queue = new {typeFullName}(original.Count, original.Comparer);");
        builder.AppendLine("foreach (var item in original.UnorderedItems)");
        builder.AppendLine("{");
        builder.IncreaseIndent();

        var elementClone = elementIsImmutable
            ? "item.Element"
            : codeGenerator.GenerateTypeCloneCall(elementType, "item.Element", allClassInfos);
        var priorityClone = priorityIsImmutable
            ? "item.Priority"
            : codeGenerator.GenerateTypeCloneCall(priorityType, "item.Priority", allClassInfos);

        builder.AppendLine($"queue.Enqueue({elementClone}, {priorityClone});");
        builder.DecreaseIndent();
        builder.AppendLine("}");
        builder.AppendLine("return queue;");
        builder.DecreaseIndent();
        builder.AppendLine("}");

        return builder;
    }
}
