using System;
using System.Linq;

namespace IDeepCloneable.Generator;

/// <summary>
/// Utility methods for code generation.
/// </summary>
internal static class CodeGenerationUtility
{
    private static readonly System.Collections.Generic.HashSet<string> CSharpKeywords = new(
        StringComparer.Ordinal
    )
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "byte",
        "case",
        "catch",
        "char",
        "checked",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
        "nint",
        "nuint",
        "var",
        "dynamic",
        "record",
        "init",
    };

    /// <summary>
    /// Sanitizes a type name to be used as a method name or identifier.
    /// </summary>
    public static string SanitizeTypeName(string typeName)
    {
        // Handle multi-dimensional arrays first (e.g., [,] -> _Array2D, [,,] -> _Array3D)
        var result = typeName;
        if (result.Contains("[,"))
        {
            // Count dimensions by counting commas and adding 1
            var bracketStart = result.IndexOf('[');
            var bracketEnd = result.IndexOf(']', bracketStart);
            if (bracketStart >= 0 && bracketEnd > bracketStart)
            {
                var bracketContent = result.Substring(
                    bracketStart + 1,
                    bracketEnd - bracketStart - 1
                );
                var dimensions = bracketContent.Count(c => c == ',') + 1;
                result =
                    result.Substring(0, bracketStart)
                    + $"_Array{dimensions}D"
                    + result.Substring(bracketEnd + 1);
            }
        }

        return result
            .Replace("global::", "")
            .Replace("::", "_")
            .Replace(".", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace(",", "_")
            .Replace(" ", "")
            .Replace("[]", "_Array")
            .Replace("?", "");
    }

    /// <summary>
    /// Checks if a type is immutable based on its full name.
    /// </summary>
    public static bool IsTypeImmutable(string typeFullName)
    {
        if (string.IsNullOrWhiteSpace(typeFullName))
            return false;

        var unwrappedNullable = UnwrapNullableType(typeFullName);
        if (!string.Equals(unwrappedNullable, typeFullName, StringComparison.Ordinal))
        {
            return IsTypeImmutable(unwrappedNullable);
        }

        // Arrays are always mutable, even if their element type is immutable
        if (typeFullName.Contains("[") && typeFullName.Contains("]"))
            return false;

        // Most collections are mutable (except for Immutable* types which are handled separately)
        if (
            typeFullName.Contains("System.Collections.Generic.List<")
            || typeFullName.Contains("System.Collections.Generic.Dictionary<")
            || typeFullName.Contains("System.Collections.Generic.HashSet<")
            || typeFullName.Contains("System.Collections.Generic.SortedSet<")
            || typeFullName.Contains("System.Collections.Generic.Stack<")
            || typeFullName.Contains("System.Collections.Generic.Queue<")
            || typeFullName.Contains("System.Collections.ObjectModel.ObservableCollection<")
            || typeFullName.Contains("System.Collections.ObjectModel.ReadOnlyCollection<")
        )
            return false;

        var normalizedType = typeFullName.Replace("global::", "").ToLowerInvariant();

        if (
            normalizedType == "string"
            || normalizedType == "int"
            || normalizedType == "long"
            || normalizedType == "short"
            || normalizedType == "uint"
            || normalizedType == "ulong"
            || normalizedType == "ushort"
            || normalizedType == "byte"
            || normalizedType == "sbyte"
            || normalizedType == "bool"
            || normalizedType == "double"
            || normalizedType == "float"
            || normalizedType == "decimal"
            || normalizedType == "char"
        )
        {
            return true;
        }

        return normalizedType.Contains("system.string")
            || normalizedType.Contains("system.int32")
            || normalizedType.Contains("system.int64")
            || normalizedType.Contains("system.int16")
            || normalizedType.Contains("system.uint32")
            || normalizedType.Contains("system.uint64")
            || normalizedType.Contains("system.uint16")
            || normalizedType.Contains("system.byte")
            || normalizedType.Contains("system.sbyte")
            || normalizedType.Contains("system.boolean")
            || normalizedType.Contains("system.double")
            || normalizedType.Contains("system.single")
            || normalizedType.Contains("system.decimal")
            || normalizedType.Contains("system.char")
            || normalizedType.Contains("system.datetime")
            || normalizedType.Contains("system.datetimeoffset")
            || normalizedType.Contains("system.timespan")
            || normalizedType.Contains("system.guid");
    }

    /// <summary>
    /// Unwraps nullable value types (e.g., int? or System.Nullable&lt;int&gt;) to their underlying type.
    /// </summary>
    private static string UnwrapNullableType(string typeFullName)
    {
        var trimmed = typeFullName.Trim();
        if (trimmed.EndsWith("?", StringComparison.Ordinal))
        {
            return trimmed.Substring(0, trimmed.Length - 1);
        }

        if (
            trimmed.StartsWith("global::System.Nullable<", StringComparison.Ordinal)
            || trimmed.StartsWith("System.Nullable<", StringComparison.Ordinal)
        )
        {
            return ExtractGenericType(trimmed);
        }

        return typeFullName;
    }

    /// <summary>
    /// Checks if a type name represents a simple type parameter (e.g., "T", "TKey", "TValue").
    /// Type parameters don't contain ".", "::", or "&lt;" characters.
    /// </summary>
    public static bool IsSimpleTypeParameter(string typeFullName)
    {
        var trimmed = typeFullName.Trim().Replace("global::", "");
        // Type parameters are single identifiers without dots, colons, brackets, or other special characters
        return !trimmed.Contains(".")
            && !trimmed.Contains("::")
            && !trimmed.Contains("<")
            && !trimmed.Contains("[")
            && !trimmed.Contains(" ")
            && !CSharpKeywords.Contains(trimmed)
            && trimmed.Length > 0
            && char.IsLetter(trimmed[0]); // Type parameters start with a letter
    }

    /// <summary>
    /// Extracts the generic type argument from a collection type.
    /// </summary>
    public static string ExtractGenericType(string fullTypeName)
    {
        var startIndex = fullTypeName.IndexOf('<');
        var endIndex = fullTypeName.LastIndexOf('>');
        if (startIndex < 0 || endIndex <= startIndex)
            return fullTypeName;

        var innerType = fullTypeName.Substring(startIndex + 1, endIndex - startIndex - 1);

        // If the inner type is a known type that should have the global:: prefix, add it
        var isCollectionType = innerType.StartsWith("System.Collections", StringComparison.Ordinal);
        var isLinqType = innerType.StartsWith("System.Linq", StringComparison.Ordinal);
        var isAlreadyGlobal = innerType.StartsWith("global::", StringComparison.Ordinal);
        if ((isCollectionType || isLinqType) && !isAlreadyGlobal)
        {
            innerType = "global::" + innerType;
        }

        return innerType;
    }

    /// <summary>
    /// Splits generic type arguments, handling nested generics correctly.
    /// </summary>
    public static System.Collections.Generic.List<string> SplitGenericArgs(string typeArgs)
    {
        var result = new System.Collections.Generic.List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;

        foreach (var c in typeArgs)
        {
            if (c == '<')
            {
                depth++;
                current.Append(c);
            }
            else if (c == '>')
            {
                depth--;
                current.Append(c);
            }
            else if (c == ',' && depth == 0)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            result.Add(current.ToString().Trim());

        return result;
    }

    /// <summary>
    /// Extracts unique generic type parameters referenced by a type name in order of appearance.
    /// </summary>
    public static System.Collections.Generic.List<string> ExtractGenericTypeParameters(
        string typeFullName
    )
    {
        var result = new System.Collections.Generic.List<string>();
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        void Collect(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return;

            var trimmed = typeName.Trim();
            if (IsSimpleTypeParameter(trimmed))
            {
                var name = trimmed.Replace("global::", string.Empty);
                if (seen.Add(name))
                {
                    result.Add(name);
                }
                return;
            }

            var arrayIndex = trimmed.IndexOf('[');
            if (arrayIndex >= 0)
            {
                var elementType = trimmed.Substring(0, arrayIndex);
                Collect(elementType);
                return;
            }

            var genericStart = trimmed.IndexOf('<');
            var genericEnd = trimmed.LastIndexOf('>');
            if (genericStart < 0 || genericEnd <= genericStart)
                return;

            var genericArgs = trimmed.Substring(genericStart + 1, genericEnd - genericStart - 1);
            foreach (var arg in SplitGenericArgs(genericArgs))
            {
                Collect(arg);
            }
        }

        Collect(typeFullName);
        return result;
    }

    /// <summary>
    /// Builds a generic parameter list for a method based on a type name.
    /// </summary>
    public static string BuildGenericTypeParameterList(string typeFullName)
    {
        var parameters = ExtractGenericTypeParameters(typeFullName);
        return parameters.Count == 0 ? string.Empty : $"<{string.Join(", ", parameters)}>";
    }
}
