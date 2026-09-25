using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace IDeepCloneable.Generator;

/// <summary>
/// Special type handler for arrays (both single and multi-dimensional).
/// </summary>
internal class ArrayTypeInfo : SpecialTypeInfo
{
    public override string TargetTypeStartWith => ""; // Arrays don't have a fixed prefix

    public override bool IsMatch(ClassInfo classInfo)
    {
        // Match if the type contains array brackets
        return classInfo.FullClassName.Contains("[") && classInfo.FullClassName.Contains("]");
    }

    public override string GetMethodName(string typeFullName)
    {
        return "CloneArray_" + CodeGenerationUtility.SanitizeTypeName(typeFullName);
    }

    public override IndentedStringBuilder GenerateCloneMethod(
        string typeFullName,
        string methodName,
        List<ClassInfo> allClassInfos,
        IndentedStringBuilder builder,
        CodeGenerator codeGenerator
    )
    {
        // Check if this is a multi-dimensional array
        if (typeFullName.Contains(","))
        {
            return GenerateMultiDimensionalArrayCloneMethod(
                typeFullName,
                methodName,
                allClassInfos,
                builder,
                codeGenerator
            );
        }
        else
        {
            return GenerateSingleDimensionalArrayCloneMethod(
                typeFullName,
                methodName,
                allClassInfos,
                builder,
                codeGenerator
            );
        }
    }

    /// <summary>
    /// Generates clone method for single-dimensional arrays.
    /// Uses AsSpan().ToArray() for immutable elements when supported, deep clones mutable elements.
    /// </summary>
    private static IndentedStringBuilder GenerateSingleDimensionalArrayCloneMethod(
        string typeFullName,
        string methodName,
        List<ClassInfo> allClassInfos,
        IndentedStringBuilder builder,
        CodeGenerator codeGenerator
    )
    {
        var genericParams = CodeGenerationUtility.BuildGenericTypeParameterList(typeFullName);
        // Extract element type (everything before the first '[')
        var bracketIndex = typeFullName.IndexOf('[');
        var elementType = typeFullName.Substring(0, bracketIndex);
        var isImmutable = CodeGenerationUtility.IsTypeImmutable(elementType);

        builder.AppendLine("");
        builder.AppendLine($"{CodeTemplateContents.EditorBrowsableAttribute}");
        builder.AppendLine(
            $"private static {typeFullName} {methodName}{genericParams}(this {typeFullName} original)"
        );
        builder.AppendLine("{");
        builder.IncreaseIndent();
        builder.AppendLine("if (original == null) return null;");

        if (isImmutable)
        {
            if (codeGenerator.SupportsSpan)
            {
                // Use AsSpan().ToArray() for fast cloning of immutable/primitive elements
                builder.AppendLine("return original.AsSpan().ToArray();");
            }
            else
            {
                builder.AppendLine($"return (original.Clone() as {typeFullName});");
            }
        }
        else
        {
            // Deep clone each mutable element
            builder.AppendLine($"var size = original.Length;");
            builder.AppendLine($"var array = new {elementType}[size];");

            if (codeGenerator.SupportsSpan)
            {
                builder.AppendLine("var arrSpan = array.AsSpan();");
            }
            builder.AppendLine("for (int i = 0; i < size; i++)");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            var cloneCall = codeGenerator.GenerateTypeCloneCall(
                elementType,
                "original[i]",
                allClassInfos
            );

            var valName = codeGenerator.SupportsSpan ? "arrSpan" : "array";
            builder.AppendLine($"{valName}[i] = {cloneCall};");
            builder.DecreaseIndent();
            builder.AppendLine("}");
            builder.AppendLine("return array;");
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");

        return builder;
    }

    /// <summary>
    /// Generates clone method for multi-dimensional arrays.
    /// Deep clones each element for mutable types, Array.Clone for immutable types.
    /// </summary>
    private static IndentedStringBuilder GenerateMultiDimensionalArrayCloneMethod(
        string typeFullName,
        string methodName,
        List<ClassInfo> allClassInfos,
        IndentedStringBuilder builder,
        CodeGenerator codeGenerator
    )
    {
        var genericParams = CodeGenerationUtility.BuildGenericTypeParameterList(typeFullName);
        // Extract element type (everything before the first '[')
        var bracketIndex = typeFullName.IndexOf('[');
        var elementType = typeFullName.Substring(0, bracketIndex);
        var isImmutable = CodeGenerationUtility.IsTypeImmutable(elementType);

        builder.AppendLine("");
        builder.AppendLine($"{CodeTemplateContents.EditorBrowsableAttribute}");
        builder.AppendLine(
            $"private static {typeFullName} {methodName}{genericParams}(this {typeFullName} original)"
        );
        builder.AppendLine("{");
        builder.IncreaseIndent();
        builder.AppendLine("if (original == null) return null;");

        if (isImmutable)
        {
            // For immutable elements, Array.Clone() is sufficient
            builder.AppendLine($"return (original.Clone() as {typeFullName});");
        }
        else
        {
            // Multi-dimensional arrays with mutable elements - deep clone each element
            // Extract dimensions from type (e.g., [,] is 2D, [,,] is 3D)
            var dimensionCount = typeFullName.Count(c => c == ',') + 1;
            builder.AppendLine($"// Dimension {dimensionCount}");

            // Get lengths for each dimension
            for (int i = 0; i < dimensionCount; i++)
            {
                builder.AppendLine($"var lb{i} = original.GetLowerBound({i});");
                builder.AppendLine($"var l{i} = original.GetLength({i});");
            }

            // Create clone array (preserve lower bounds if non-zero)
            builder.AppendLine("var hasNonZeroLowerBound = false;");
            for (int i = 0; i < dimensionCount; i++)
            {
                builder.AppendLine($"if (lb{i} != 0) hasNonZeroLowerBound = true;");
            }

            var lengthsPart = string.Join(
                ",",
                Enumerable.Range(0, dimensionCount).Select(i => $"l{i}")
            );
            var lowerBoundsPart = string.Join(
                ",",
                Enumerable.Range(0, dimensionCount).Select(i => $"lb{i}")
            );
            builder.AppendLine(
                $"var clone = hasNonZeroLowerBound"
                    + $" ? ({typeFullName})global::System.Array.CreateInstance(typeof({elementType}), new[] {{ {lengthsPart} }}, new[] {{ {lowerBoundsPart} }})"
                    + $" : new {elementType}[{lengthsPart}];"
            );

            // Create clone each element
            for (int i = 0; i < dimensionCount; i++)
            {
                builder.AppendLine($"for (int i{i} = lb{i}; i{i} < lb{i} + l{i}; i{i}++)");
                builder.AppendLine("{");
                builder.IncreaseIndent();
            }

            var originalAccessors = Enumerable.Range(0, dimensionCount).Select(i => $"i{i}");
            var originalAccessorText = $"[{string.Join(",", originalAccessors)}]";
            var generateCall = codeGenerator.GenerateTypeCloneCall(
                elementType,
                $"original{originalAccessorText}",
                allClassInfos
            );
            builder.AppendLine($"clone{originalAccessorText} = {generateCall};");

            for (int i = 0; i < dimensionCount; i++)
            {
                builder.DecreaseIndent();
                builder.AppendLine("}");
            }
            builder.AppendLine("return clone;");
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");

        return builder;
    }
}
