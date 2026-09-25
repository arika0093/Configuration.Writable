using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;

namespace Configuration.Writable.FormatProvider;

internal static class JsonDeepMergeHelper
{
    public static object Merge(
        Type type,
        IReadOnlyList<ReadOnlyMemory<byte>> documents,
        IReadOnlyList<string> sectionNameParts,
        IGeneratedOptionsMergeMetadata? metadata,
        JsonSerializerOptions serializerOptions,
        Func<Type, object> createDefault,
        CancellationToken cancellationToken
    )
    {
        IDeepMergeNode? merged = null;
        foreach (var documentBytes in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = JsonDocument.Parse(documentBytes);
            if (
                !TryGetSection(document.RootElement, sectionNameParts, out var section)
                || section.ValueKind == JsonValueKind.Null
            )
                continue;

            merged = DeepMergeDocument.Merge(
                merged,
                FromJson(section),
                type,
                metadata,
                (declaringType, propertyName) =>
                    ResolvePropertyMetadata(
                        declaringType,
                        propertyName,
                        metadata,
                        serializerOptions
                    )
            );
        }

        if (merged is null)
            return createDefault(type);

        var json = ToJson(merged);
        var typeInfo = serializerOptions.GetTypeInfo(type);
        return JsonSerializer.Deserialize(json, typeInfo) ?? createDefault(type);
    }

    private static DeepMergePropertyMetadata? ResolvePropertyMetadata(
        Type declaringType,
        string serializedName,
        IGeneratedOptionsMergeMetadata? metadata,
        JsonSerializerOptions serializerOptions
    )
    {
        var typeInfo = serializerOptions.GetTypeInfo(declaringType);
        foreach (var property in typeInfo.Properties)
        {
            if (!string.Equals(property.Name, serializedName, StringComparison.Ordinal))
                continue;

            var memberInfo = property.AttributeProvider as MemberInfo;
            var propertyDeclaringType = memberInfo?.DeclaringType ?? declaringType;
            var clrName = memberInfo?.Name ?? property.Name;
            if (
                metadata is not null
                && metadata.TryGetPropertyMetadata(
                    propertyDeclaringType,
                    clrName,
                    out var generatedType,
                    out var generatedMode
                )
            )
                return new DeepMergePropertyMetadata(generatedType, generatedMode);

            return new DeepMergePropertyMetadata(property.PropertyType, DeepMergeArrayMode.Replace);
        }

        return
            metadata is not null
            && metadata.TryGetPropertyMetadata(
                declaringType,
                serializedName,
                out var propertyType,
                out var arrayMode
            )
            ? new DeepMergePropertyMetadata(propertyType, arrayMode)
            : null;
    }

    private static bool TryGetSection(
        JsonElement root,
        IReadOnlyList<string> sections,
        out JsonElement section
    )
    {
        section = root;
        foreach (var name in sections)
        {
            if (
                section.ValueKind != JsonValueKind.Object
                || !section.TryGetProperty(name, out section)
            )
                return false;
        }

        return true;
    }

    private static IDeepMergeNode FromJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var result = new DeepMergeObjectNode();
                foreach (var property in element.EnumerateObject())
                    result.Properties[property.Name] = FromJson(property.Value);
                return result;
            }
            case JsonValueKind.Array:
            {
                var result = new DeepMergeArrayNode();
                foreach (var item in element.EnumerateArray())
                    result.Items.Add(FromJson(item));
                return result;
            }
            case JsonValueKind.String:
                return new DeepMergeScalarNode(
                    DeepMergeScalarKind.String,
                    element.GetString() ?? string.Empty
                );
            case JsonValueKind.Number:
                return new DeepMergeScalarNode(DeepMergeScalarKind.Number, element.GetRawText());
            case JsonValueKind.True:
                return new DeepMergeScalarNode(DeepMergeScalarKind.Boolean, "true");
            case JsonValueKind.False:
                return new DeepMergeScalarNode(DeepMergeScalarKind.Boolean, "false");
            default:
                return new DeepMergeScalarNode(DeepMergeScalarKind.Null, string.Empty);
        }
    }

    private static string ToJson(IDeepMergeNode node)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
            WriteJson(writer, node);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteJson(Utf8JsonWriter writer, IDeepMergeNode node)
    {
        switch (node)
        {
            case DeepMergeObjectNode objectNode:
                writer.WriteStartObject();
                foreach (var property in objectNode.Properties)
                {
                    writer.WritePropertyName(property.Key);
                    WriteJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case DeepMergeArrayNode arrayNode:
                writer.WriteStartArray();
                foreach (var item in arrayNode.Items)
                    WriteJson(writer, item);
                writer.WriteEndArray();
                break;
            case DeepMergeScalarNode { Kind: DeepMergeScalarKind.Null }:
                writer.WriteNullValue();
                break;
            case DeepMergeScalarNode { Kind: DeepMergeScalarKind.String } scalar:
                writer.WriteStringValue(scalar.Value);
                break;
            case DeepMergeScalarNode { Kind: DeepMergeScalarKind.Number } scalar:
                writer.WriteRawValue(scalar.Value, skipInputValidation: false);
                break;
            case DeepMergeScalarNode { Kind: DeepMergeScalarKind.Boolean, Value: "true" }:
                writer.WriteBooleanValue(true);
                break;
            case DeepMergeScalarNode { Kind: DeepMergeScalarKind.Boolean }:
                writer.WriteBooleanValue(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported merge document node type '{node.GetType()}'."
                );
        }
    }
}
