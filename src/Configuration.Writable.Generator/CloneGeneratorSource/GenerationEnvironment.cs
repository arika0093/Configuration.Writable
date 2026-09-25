using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace IDeepCloneable.Generator;

/// <summary>
/// Captures environment details for source generation.
/// </summary>
public readonly record struct GenerationEnvironment
{
    /// <summary>
    /// Is array to Span API supported?
    /// </summary>
    public bool SupportsSpan { get; init; }

    /// <summary>
    /// Is CollectionsMarshal.SetCount API supported? (.NET 7+)
    /// </summary>
    public bool SupportsCollectionsMarshalSetCount { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationEnvironment"/> struct.
    /// </summary>
    public static GenerationEnvironment Create(Compilation compilation)
    {
        return new GenerationEnvironment
        {
            SupportsSpan = SupportsSpanApi(compilation),
            SupportsCollectionsMarshalSetCount = SupportsCollectionsMarshalSetCountApi(compilation),
        };
    }

    private static bool SupportsSpanApi(Compilation compilation)
    {
        var spanType = compilation.GetTypesByMetadataName("System.Span`1");
        return spanType.Any();
    }

    private static bool SupportsCollectionsMarshalSetCountApi(Compilation compilation)
    {
        var collectionsMarshalType = compilation.GetTypesByMetadataName(
            "System.Runtime.InteropServices.CollectionsMarshal"
        );
        if (!collectionsMarshalType.Any())
        {
            return false;
        }
        var setCountMethod = collectionsMarshalType
            .SelectMany(t => t.GetMembers("SetCount"))
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Parameters.Length == 2);
        return setCountMethod != null;
    }
}
