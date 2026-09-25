using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace IDeepCloneable.Generator;

/// <summary>
/// Special type handler for ConcurrentStack&lt;T&gt; collections.
/// </summary>
internal class ConcurrentStackTypeInfo : SpecialTypeInfo
{
    public override string TargetTypeStartWith =>
        "global::System.Collections.Concurrent.ConcurrentStack<";

    public override string GetMethodName(string typeFullName)
    {
        return "CloneConcurrentStack_" + CodeGenerationUtility.SanitizeTypeName(typeFullName);
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
        var innerType = CodeGenerationUtility.ExtractGenericType(typeFullName);
        var isImmutable = CodeGenerationUtility.IsTypeImmutable(innerType);

        builder.AppendLine("");
        builder.AppendLine(CodeTemplateContents.EditorBrowsableAttribute);
        builder.AppendLine(
            $"private static {typeFullName} {methodName}{genericParams}(this {typeFullName} original)"
        );
        builder.AppendLine("{");
        builder.IncreaseIndent();
        builder.AppendLine("if (original == null) return null;");
        builder.AppendLine("var array = original.ToArray();");
        builder.AppendLine("global::System.Array.Reverse(array);");

        if (isImmutable)
        {
            builder.AppendLine($"return new {typeFullName}(array);");
        }
        else
        {
            builder.AppendLine($"var stack = new {typeFullName}();");
            builder.AppendLine("for (int i = 0; i < array.Length; i++)");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            var cloneCall = codeGenerator.GenerateTypeCloneCall(
                innerType,
                "array[i]",
                allClassInfos
            );
            builder.AppendLine($"stack.Push({cloneCall});");
            builder.DecreaseIndent();
            builder.AppendLine("}");
            builder.AppendLine("return stack;");
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");

        return builder;
    }
}
