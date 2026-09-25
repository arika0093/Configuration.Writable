using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace IDeepCloneable.Generator;

/// <summary>
/// Analyzes types and extracts metadata needed for deep clone generation.
/// </summary>
internal class TypeAnalyzer(CloneableGeneratorOptionsCore options)
{
    public EquatableArray<ClassInfo>? GetRelationalAllClassInfo(
        GeneratorAttributeSyntaxContext context
    )
    {
        // Extracts information from types marked with [DeepCloneable] and all reachable types.
        // "Reachable" is defined as:
        //   * The class itself marked with [DeepCloneable] (A)
        //   * All classes that inherit from A
        //   * All classes referenced by properties/fields of A (recursively)
        try
        {
            var targetSymbol = context.TargetSymbol as INamedTypeSymbol;
            if (targetSymbol == null)
                return null;

            var classInfoList = new List<ClassInfo>();
            var processedTypes = new HashSet<string>();
            var typesToProcess = new Queue<INamedTypeSymbol>();

            typesToProcess.Enqueue(targetSymbol);

            while (typesToProcess.Count > 0)
            {
                var currentType = typesToProcess.Dequeue();
                var fullName = GetFullTypeName(currentType);

                if (processedTypes.Contains(fullName))
                    continue;

                processedTypes.Add(fullName);

                var classInfo = CreateClassInfo(currentType, out var childTypes);
                if (classInfo != null)
                {
                    classInfoList.Add(classInfo);
                    foreach (
                        var childType in childTypes.Where(childType =>
                            !processedTypes.Contains(GetFullTypeName(childType))
                        )
                    // Enqueue child types discovered during property analysis
                    )
                    {
                        typesToProcess.Enqueue(childType);
                    }
                }
            }

            // Detect circular references after all types are collected
            DetectCircularReferences(classInfoList);

            return new EquatableArray<ClassInfo>(classInfoList);
        }
        catch
        {
            return null;
        }
    }

    public EquatableArray<ClassInfo>? GetRelationalAllClassInfoFromGenerateAttribute(
        GeneratorAttributeSyntaxContext context
    )
    {
        try
        {
            var targetTypes = GetGenerateDeepCloneableTargets(context);
            if (targetTypes.Count == 0)
                return null;

            var classInfoList = new List<ClassInfo>();
            var processedTypes = new HashSet<string>();
            var typesToProcess = new Queue<INamedTypeSymbol>();

            var rootTypeNames = new HashSet<string>(
                targetTypes.Select(target => GetFullTypeName(target))
            );
            foreach (var targetType in targetTypes)
            {
                typesToProcess.Enqueue(targetType);
            }

            while (typesToProcess.Count > 0)
            {
                var currentType = typesToProcess.Dequeue();
                var fullName = GetFullTypeName(currentType);

                if (processedTypes.Contains(fullName))
                    continue;

                processedTypes.Add(fullName);

                var classInfo = CreateClassInfo(
                    currentType,
                    out var childTypes,
                    forceNoDeepCloneMethod: rootTypeNames.Contains(fullName)
                );

                if (classInfo != null)
                {
                    classInfoList.Add(classInfo);
                    foreach (
                        var childType in childTypes.Where(childType =>
                            !processedTypes.Contains(GetFullTypeName(childType))
                        )
                    )
                    {
                        typesToProcess.Enqueue(childType);
                    }
                }
            }

            DetectCircularReferences(classInfoList);

            return new EquatableArray<ClassInfo>(classInfoList);
        }
        catch
        {
            return null;
        }
    }

    private ClassInfo? CreateClassInfo(
        INamedTypeSymbol typeSymbol,
        out List<INamedTypeSymbol> childTypes,
        bool forceNoDeepCloneMethod = false
    )
    {
        var properties = GetProperties(typeSymbol, out childTypes);
        var fullName = GetFullTypeName(typeSymbol);

        var hasDeepCloneableAttribute = IsImplementedInterface(typeSymbol);

        // Check if this type already has a DeepClone method
        var alreadyHasDeepClone = typeSymbol
            .GetMembers(options.ImplementsMethodName)
            .OfType<IMethodSymbol>()
            .Any(m =>
                m.Parameters.Length == 0
                && m.ReturnType.Equals(typeSymbol, SymbolEqualityComparer.Default)
            );

        // Check if base class has DeepClone method (either via attribute or manual implementation)
        var baseHasDeepClone = false;
        var current = typeSymbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            // Check for [DeepCloneable] attribute
            var hasAttribute = IsImplementedInterface(current);
            if (hasAttribute)
            {
                baseHasDeepClone = true;
                break;
            }

            // Check for manual DeepClone method implementation
            // Must have no parameters and return a compatible type
            if (
                current
                    .GetMembers(options.ImplementsMethodName)
                    .OfType<IMethodSymbol>()
                    .Any(m =>
                        m.Parameters.Length == 0
                        && (
                            m.ReturnType.Equals(current, SymbolEqualityComparer.Default)
                            || SymbolEqualityComparer.Default.Equals(
                                m.ReturnType.OriginalDefinition,
                                current.OriginalDefinition
                            )
                        )
                    )
            )
            {
                baseHasDeepClone = true;
                break;
            }

            current = current.BaseType;
        }

        // Check if this type has a copy constructor (Type(Type other))
        var hasCopyConstructor = typeSymbol.Constructors.Any(ctor =>
            !ctor.IsStatic
            && ctor.Parameters.Length == 1
            && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, typeSymbol)
        );

        // Extract generic type parameters
        var genericTypeParameters = string.Empty;
        if (typeSymbol.IsGenericType)
        {
            var typeParams = typeSymbol.TypeParameters;
            if (typeParams.Length > 0)
            {
                genericTypeParameters = string.Join(", ", typeParams.Select(tp => tp.Name));
            }
        }

        // Extract implemented interfaces
        var implementedInterfaces = typeSymbol
            .AllInterfaces.Select(i => GetFullTypeName(i))
            .ToList();

        return new ClassInfo
        {
            ClassName = typeSymbol.Name,
            FullClassName = fullName,
            Namespace = GetNamespace(typeSymbol),
            ContainingTypeNames = new EquatableArray<string>(GetContainingTypeNames(typeSymbol)),
            Properties = new EquatableArray<PropertyInfo>(properties),
            IsNullable = typeSymbol.NullableAnnotation == NullableAnnotation.Annotated,
            IsRecord = typeSymbol.IsRecord,
            IsValueType = typeSymbol.IsValueType,
            IsAllImmutable = properties.All(p => p.IsImmutable),
            IsCollection = IsCollectionType(typeSymbol),
            NeedsDeepCloneMethod =
                !forceNoDeepCloneMethod && (hasDeepCloneableAttribute || baseHasDeepClone),
            IsAbstract = typeSymbol.IsAbstract,
            IsSealed = typeSymbol.IsSealed,
            BaseHasDeepClone = baseHasDeepClone,
            AlreadyHasDeepClone = alreadyHasDeepClone,
            HasCopyConstructor = hasCopyConstructor,
            HasCircularReference = false, // Will be updated later in circular reference detection
            GenericTypeParameters = genericTypeParameters,
            ImplementedInterfaces = new EquatableArray<string>(implementedInterfaces),
        };
    }

    private List<INamedTypeSymbol> GetGenerateDeepCloneableTargets(
        GeneratorAttributeSyntaxContext context
    )
    {
        var targets = context
            .Attributes.Where(attr =>
                attr.AttributeClass?.Name == options.GenerateDeepCloneableAttributeName
            )
            .Where(attr =>
                attr.ConstructorArguments.Length > 0
                && attr.ConstructorArguments[0].Kind == TypedConstantKind.Type
                && attr.ConstructorArguments[0].Value is INamedTypeSymbol
            )
            .Select(attr => attr.ConstructorArguments[0].Value as INamedTypeSymbol)
            .Where(target => target != null)
            .Select(target => target!)
            .ToList();

        return targets;
    }

    private List<PropertyInfo> GetProperties(
        INamedTypeSymbol typeSymbol,
        out List<INamedTypeSymbol> childTypes
    )
    {
        var properties = new List<PropertyInfo>();
        childTypes = [];

        foreach (var member in typeSymbol.GetMembers())
        {
            ITypeSymbol? memberType = null;
            string? memberName = null;
            bool isRequired = false;
            ISymbol? memberSymbol = null;

            if (member is IPropertySymbol propSymbol && !propSymbol.IsStatic)
            {
                // Skip indexers (this[])
                if (propSymbol.IsIndexer)
                    continue;

                // Skip explicitly implemented interface properties (have dots in MetadataName)
                if (propSymbol.MetadataName.Contains("."))
                    continue;

                // Skip properties without a setter (can't be cloned into)
                if (propSymbol.SetMethod == null)
                    continue;

                memberType = propSymbol.Type;
                memberName = propSymbol.Name;
                isRequired = propSymbol.IsRequired;
                memberSymbol = propSymbol;
            }
            else if (
                member is IFieldSymbol fieldSymbol
                && !fieldSymbol.IsStatic
                && !fieldSymbol.IsConst
                && !fieldSymbol.IsImplicitlyDeclared
            )
            {
                memberType = fieldSymbol.Type;
                memberName = fieldSymbol.Name;
                isRequired = fieldSymbol.IsRequired;
                memberSymbol = fieldSymbol;
            }

            if (memberType != null && memberName != null && memberSymbol != null)
            {
                // Check for custom attributes
                var attributes = memberSymbol.GetAttributes();
                var isCloneIgnored = attributes.Any(attr =>
                    attr.AttributeClass?.Name == options.CloneIgnoreAttributeName
                );
                var isShallowClone = attributes.Any(attr =>
                    attr.AttributeClass?.Name == options.ShallowCloneAttributeName
                );

                var isImmutable = IsImmutableType(memberType);
                // If marked with ShallowClone, treat as immutable (no deep clone needed)
                // If marked with CloneIgnore, it doesn't need deep clone either (will be skipped)
                var needsDeepClone = !isImmutable && !isShallowClone && !isCloneIgnored;

                properties.Add(
                    new PropertyInfo
                    {
                        Name = memberName,
                        TypeFullName = GetFullTypeName(memberType),
                        IsNullable = memberType.NullableAnnotation == NullableAnnotation.Annotated,
                        NeedsDeepClone = needsDeepClone,
                        IsImmutable = isImmutable,
                        IsRequired = isRequired,
                        IsCloneIgnored = isCloneIgnored,
                        IsShallowClone = isShallowClone,
                    }
                );

                // Extract child types for further processing
                // Only add child types if they need deep cloning (not ignored, not shallow)
                if (needsDeepClone)
                {
                    ExtractChildTypes(memberType, childTypes);
                }
            }
        }

        return properties;
    }

    private void ExtractChildTypes(ITypeSymbol typeSymbol, List<INamedTypeSymbol> childTypes)
    {
        // Handle generic types (List<T>, Dictionary<TKey, TValue>, etc.)
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            // Add all type arguments
            foreach (var typeArg in namedType.TypeArguments)
            {
                if (typeArg is INamedTypeSymbol argNamedType && !IsImmutableType(typeArg))
                {
                    childTypes.Add(argNamedType);
                }
            }
        }
        // Handle arrays
        else if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            if (
                arrayType.ElementType is INamedTypeSymbol elementType
                && !IsImmutableType(arrayType.ElementType)
            )
            {
                childTypes.Add(elementType);
            }
        }
        // Handle regular types (including nullable reference types)
        // For nullable reference types (ContactInfo?), the type symbol is still ContactInfo
        else if (typeSymbol is INamedTypeSymbol regularType && !IsImmutableType(typeSymbol))
        {
            childTypes.Add(regularType);
        }
    }

    private string GetFullTypeName(ITypeSymbol typeSymbol)
    {
        // Handle array types specially (including multi-dimensional and jagged)
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            var elementTypeName = GetFullTypeName(arrayType.ElementType);

            // Handle multi-dimensional arrays
            if (arrayType.Rank == 1)
            {
                return $"{elementTypeName}[]";
            }
            else
            {
                // For multi-dimensional arrays like int[,] or int[,,]
                var commas = new string(',', arrayType.Rank - 1);
                return $"{elementTypeName}[{commas}]";
            }
        }

        // For primitive types, use the CLR type name instead of the C# keyword
        // because global::int is invalid (must be global::System.Int32)
        var displayString = typeSymbol.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
                SymbolDisplayGlobalNamespaceStyle.Omitted
            )
        );

        // Map C# keywords to CLR type names for use with global::
        displayString = displayString switch
        {
            "bool" => "System.Boolean",
            "byte" => "System.Byte",
            "sbyte" => "System.SByte",
            "char" => "System.Char",
            "decimal" => "System.Decimal",
            "double" => "System.Double",
            "float" => "System.Single",
            "int" => "System.Int32",
            "uint" => "System.UInt32",
            "long" => "System.Int64",
            "ulong" => "System.UInt64",
            "short" => "System.Int16",
            "ushort" => "System.UInt16",
            "object" => "System.Object",
            "string" => "System.String",
            _ => displayString,
        };

        return "global::" + displayString;
    }

    private static string GetNamespace(INamedTypeSymbol typeSymbol)
    {
        // Return only the actual namespace, not containing types
        // Return empty string for global namespace
        if (
            typeSymbol.ContainingNamespace == null
            || typeSymbol.ContainingNamespace.IsGlobalNamespace
        )
            return string.Empty;

        return typeSymbol.ContainingNamespace.ToDisplayString();
    }

    private static List<string> GetContainingTypeNames(INamedTypeSymbol typeSymbol)
    {
        var containingTypes = new List<string>();
        var containingType = typeSymbol.ContainingType;
        while (containingType != null)
        {
            containingTypes.Insert(0, containingType.Name);
            containingType = containingType.ContainingType;
        }
        return containingTypes;
    }

    private bool IsImplementedInterface(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol
            .GetAttributes()
            .Select(attr =>
                attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            )
            .Any(attrName => attrName == $"global::{options.AttributeMetadataName}");
    }

    private bool IsImmutableType(ITypeSymbol typeSymbol)
    {
        // System.Object cannot be cloned (it's the base type)
        if (typeSymbol.SpecialType == SpecialType.System_Object)
            return true;

        if (typeSymbol.TypeKind == TypeKind.Enum)
            return true;

        if (typeSymbol.IsValueType && typeSymbol.SpecialType != SpecialType.None)
            return true;

        if (typeSymbol.SpecialType == SpecialType.System_String)
            return true;

        switch (typeSymbol.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Decimal:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_DateTime:
                return true;
        }

        var fullName = GetFullTypeName(typeSymbol);
        if (
            fullName.StartsWith("global::System.DateTimeOffset")
            || fullName.StartsWith("global::System.TimeSpan")
            || fullName.StartsWith("global::System.Guid")
        )
        {
            return true;
        }

        return false;
    }

    private bool IsCollectionType(ITypeSymbol typeSymbol)
    {
        var fullName = GetFullTypeName(typeSymbol);
        return fullName.Contains("System.Collections.Generic.List<")
            || fullName.Contains("System.Collections.Generic.Dictionary<")
            || fullName.Contains("System.Collections.Generic.HashSet<")
            || fullName.Contains("System.Collections.Generic.SortedSet<")
            || fullName.Contains("System.Collections.Generic.Stack<")
            || fullName.Contains("System.Collections.Generic.Queue<")
            || fullName.Contains("System.Collections.ObjectModel.ObservableCollection<")
            || fullName.Contains("System.Collections.ObjectModel.ReadOnlyCollection<")
            || fullName.Contains("System.Collections.Immutable.ImmutableList<")
            || fullName.Contains("System.Collections.Immutable.ImmutableArray<")
            || fullName.Contains("System.Collections.Immutable.ImmutableHashSet<")
            || fullName.Contains("System.Collections.Immutable.ImmutableDictionary<")
            || fullName.Contains("[]");
    }

    /// <summary>
    /// Detects circular references in the object graph.
    /// Marks types involved in circular reference patterns with HasCircularReference flag.
    /// A circular reference occurs when: T1 -> T2 -> T3 -> T1
    /// </summary>
    private void DetectCircularReferences(List<ClassInfo> allClassInfos)
    {
        // Build a type dependency graph
        var typeDependencies = new Dictionary<string, HashSet<string>>();

        foreach (var classInfo in allClassInfos)
        {
            var normalizedName = NormalizeTypeName(classInfo.FullClassName);
            if (!typeDependencies.ContainsKey(normalizedName))
            {
                typeDependencies[normalizedName] = new HashSet<string>();
            }

            // Add direct property type dependencies (for collections, add element types)
            foreach (var prop in classInfo.Properties)
            {
                if (!prop.NeedsDeepClone)
                    continue;

                if (IsCollectionPropertyType(prop.TypeFullName))
                {
                    foreach (var elementType in ExtractCollectionElementTypes(prop.TypeFullName))
                    {
                        typeDependencies[normalizedName].Add(NormalizeTypeName(elementType));
                    }
                }
                else
                {
                    typeDependencies[normalizedName].Add(NormalizeTypeName(prop.TypeFullName));
                }
            }
        }

        // Detect cycles using DFS
        var typesInCycle = new HashSet<string>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        foreach (
            var typeName in from typeName in typeDependencies.Keys
            where !visited.Contains(typeName)
            select typeName
        )
        {
            DetectCyclesDFS(typeName, typeDependencies, visited, recursionStack, typesInCycle);
        }

        // Update ClassInfo objects with circular reference flag
        for (int i = 0; i < allClassInfos.Count; i++)
        {
            if (typesInCycle.Contains(NormalizeTypeName(allClassInfos[i].FullClassName)))
            {
                // Create a new ClassInfo with updated HasCircularReference flag
                var old = allClassInfos[i];
                allClassInfos[i] = old with { HasCircularReference = true };
            }
        }
    }

    private static string NormalizeTypeName(string typeFullName)
    {
        return typeFullName.Replace("global::", string.Empty).Trim();
    }

    /// <summary>
    /// Helper method to detect cycles using depth-first search.
    /// </summary>
    private void DetectCyclesDFS(
        string typeName,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        HashSet<string> typesInCycle
    )
    {
        visited.Add(typeName);
        recursionStack.Add(typeName);

        if (graph.TryGetValue(typeName, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                if (!visited.Contains(dependency))
                {
                    DetectCyclesDFS(dependency, graph, visited, recursionStack, typesInCycle);
                }
                else if (recursionStack.Contains(dependency))
                {
                    // Cycle detected - mark all types in the recursion stack as circular
                    foreach (var typeInStack in recursionStack)
                    {
                        typesInCycle.Add(typeInStack);
                    }
                    typesInCycle.Add(dependency);
                }
            }
        }

        recursionStack.Remove(typeName);
    }

    /// <summary>
    /// Checks if a property type is a collection type (to exclude from direct circular reference detection).
    /// </summary>
    private static bool IsCollectionPropertyType(string typeFullName)
    {
        return typeFullName.Contains("System.Collections.Generic.List<")
            || typeFullName.Contains("System.Collections.Generic.Dictionary<")
            || typeFullName.Contains("System.Collections.Generic.HashSet<")
            || typeFullName.Contains("System.Collections.Generic.SortedSet<")
            || typeFullName.Contains("System.Collections.Generic.Stack<")
            || typeFullName.Contains("System.Collections.Generic.Queue<")
            || typeFullName.Contains("System.Collections.Generic.PriorityQueue<")
            || typeFullName.Contains("System.Collections.Generic.LinkedList<")
            || typeFullName.Contains("System.Collections.ObjectModel.ObservableCollection<")
            || typeFullName.Contains("System.Collections.ObjectModel.ReadOnlyCollection<")
            || typeFullName.Contains("System.Collections.Concurrent.BlockingCollection<")
            || typeFullName.Contains("System.Collections.Concurrent.ConcurrentStack<")
            || typeFullName.Contains("System.Collections.Concurrent.ConcurrentQueue<")
            || typeFullName.Contains("System.Collections.Immutable.ImmutableList<")
            || typeFullName.Contains("System.Collections.Immutable.ImmutableArray<")
            || typeFullName.Contains("System.Collections.Immutable.ImmutableHashSet<")
            || typeFullName.Contains("System.Collections.Immutable.ImmutableDictionary<")
            || typeFullName.Contains("[]");
    }

    private static IEnumerable<string> ExtractCollectionElementTypes(string typeFullName)
    {
        var arrayBracket = typeFullName.IndexOf('[');
        if (arrayBracket >= 0)
        {
            var elementType = typeFullName.Substring(0, arrayBracket);
            if (!string.IsNullOrWhiteSpace(elementType))
                return new[] { elementType };
        }

        var genericStart = typeFullName.IndexOf('<');
        var genericEnd = typeFullName.LastIndexOf('>');
        if (genericStart < 0 || genericEnd <= genericStart)
            return Array.Empty<string>();

        var genericArgs = typeFullName.Substring(genericStart + 1, genericEnd - genericStart - 1);
        return CodeGenerationUtility
            .SplitGenericArgs(genericArgs)
            .Select(arg => arg.Trim())
            .Where(arg => !string.IsNullOrWhiteSpace(arg));
    }
}
