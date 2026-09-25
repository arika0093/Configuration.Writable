using System;
using System.Collections.Generic;

namespace Configuration.Writable.FormatProvider;

internal interface IDeepMergeNode
{
    IDeepMergeNode DeepClone();
}

internal sealed class DeepMergeObjectNode : IDeepMergeNode
{
    public Dictionary<string, IDeepMergeNode> Properties { get; } = new(StringComparer.Ordinal);

    public IDeepMergeNode DeepClone()
    {
        var clone = new DeepMergeObjectNode();
        foreach (var property in Properties)
            clone.Properties.Add(property.Key, property.Value.DeepClone());
        return clone;
    }
}

internal sealed class DeepMergeArrayNode : IDeepMergeNode
{
    public List<IDeepMergeNode> Items { get; } = [];

    public IDeepMergeNode DeepClone()
    {
        var clone = new DeepMergeArrayNode();
        foreach (var item in Items)
            clone.Items.Add(item.DeepClone());
        return clone;
    }
}

internal sealed class DeepMergeScalarNode(DeepMergeScalarKind kind, string value) : IDeepMergeNode
{
    public DeepMergeScalarKind Kind { get; } = kind;
    public string Value { get; } = value;

    public IDeepMergeNode DeepClone() => new DeepMergeScalarNode(Kind, Value);
}

internal enum DeepMergeScalarKind
{
    Null,
    String,
    Number,
    Boolean,
}

internal readonly record struct DeepMergePropertyMetadata(
    Type PropertyType,
    DeepMergeArrayMode ArrayMode
);

internal static class DeepMergeDocument
{
    public static IDeepMergeNode Merge(
        IDeepMergeNode? current,
        IDeepMergeNode incoming,
        Type modelType,
        IGeneratedOptionsMergeMetadata? metadata,
        Func<Type, string, DeepMergePropertyMetadata?>? resolveProperty = null
    )
    {
        if (incoming is DeepMergeScalarNode { Kind: DeepMergeScalarKind.Null })
            return current?.DeepClone() ?? incoming.DeepClone();

        if (incoming is DeepMergeObjectNode incomingObject)
        {
            var merged = current is DeepMergeObjectNode currentObject
                ? (DeepMergeObjectNode)currentObject.DeepClone()
                : new DeepMergeObjectNode();
            foreach (var property in incomingObject.Properties)
            {
                if (property.Value is DeepMergeScalarNode { Kind: DeepMergeScalarKind.Null })
                    continue;

                var propertyMetadata =
                    resolveProperty?.Invoke(modelType, property.Key)
                    ?? ResolveGeneratedProperty(metadata, modelType, property.Key);
                merged.Properties.TryGetValue(property.Key, out var existingValue);
                merged.Properties[property.Key] = MergeProperty(
                    existingValue,
                    property.Value,
                    propertyMetadata?.PropertyType ?? typeof(object),
                    propertyMetadata?.ArrayMode ?? DeepMergeArrayMode.Replace,
                    metadata,
                    resolveProperty
                );
            }

            return merged;
        }

        return MergeProperty(
            current,
            incoming,
            modelType,
            DeepMergeArrayMode.Replace,
            metadata,
            resolveProperty
        );
    }

    private static IDeepMergeNode MergeProperty(
        IDeepMergeNode? current,
        IDeepMergeNode incoming,
        Type propertyType,
        DeepMergeArrayMode arrayMode,
        IGeneratedOptionsMergeMetadata? metadata,
        Func<Type, string, DeepMergePropertyMetadata?>? resolveProperty
    )
    {
        if (incoming is DeepMergeScalarNode { Kind: DeepMergeScalarKind.Null })
            return current?.DeepClone() ?? incoming.DeepClone();

        if (incoming is DeepMergeArrayNode incomingArray)
        {
            if (
                arrayMode
                is not (
                    DeepMergeArrayMode.Replace
                    or DeepMergeArrayMode.Append
                    or DeepMergeArrayMode.UniqueAppend
                )
            )
                throw new ArgumentOutOfRangeException(
                    nameof(arrayMode),
                    arrayMode,
                    "The array merge mode is not supported."
                );

            if (arrayMode == DeepMergeArrayMode.Replace)
                return incomingArray.DeepClone();

            var mergedArray = current is DeepMergeArrayNode currentArray
                ? (DeepMergeArrayNode)currentArray.DeepClone()
                : new DeepMergeArrayNode();
            foreach (var item in incomingArray.Items)
            {
                if (
                    arrayMode == DeepMergeArrayMode.UniqueAppend
                    && mergedArray.Items.Exists(existing => NodesEqual(existing, item))
                )
                    continue;
                mergedArray.Items.Add(item.DeepClone());
            }

            return mergedArray;
        }

        if (incoming is DeepMergeObjectNode)
            return Merge(current, incoming, propertyType, metadata, resolveProperty);

        return incoming.DeepClone();
    }

    private static DeepMergePropertyMetadata? ResolveGeneratedProperty(
        IGeneratedOptionsMergeMetadata? metadata,
        Type declaringType,
        string propertyName
    ) =>
        metadata is not null
        && metadata.TryGetPropertyMetadata(
            declaringType,
            propertyName,
            out var propertyType,
            out var arrayMode
        )
            ? new DeepMergePropertyMetadata(propertyType, arrayMode)
            : null;

    private static bool NodesEqual(IDeepMergeNode left, IDeepMergeNode right)
    {
        if (left.GetType() != right.GetType())
            return false;

        if (left is DeepMergeScalarNode leftScalar && right is DeepMergeScalarNode rightScalar)
            return leftScalar.Kind == rightScalar.Kind
                && string.Equals(leftScalar.Value, rightScalar.Value, StringComparison.Ordinal);

        if (left is DeepMergeArrayNode leftArray && right is DeepMergeArrayNode rightArray)
        {
            if (leftArray.Items.Count != rightArray.Items.Count)
                return false;
            for (var index = 0; index < leftArray.Items.Count; index++)
            {
                if (!NodesEqual(leftArray.Items[index], rightArray.Items[index]))
                    return false;
            }
            return true;
        }

        if (left is DeepMergeObjectNode leftObject && right is DeepMergeObjectNode rightObject)
        {
            if (leftObject.Properties.Count != rightObject.Properties.Count)
                return false;
            foreach (var property in leftObject.Properties)
            {
                if (
                    !rightObject.Properties.TryGetValue(property.Key, out var otherValue)
                    || !NodesEqual(property.Value, otherValue)
                )
                    return false;
            }
            return true;
        }

        return true;
    }
}
