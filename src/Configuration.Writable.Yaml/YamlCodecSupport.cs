using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Configuration.Writable.FormatProvider;

/// <summary>
/// Shared YAML model parsing and serialization helpers for the native YAML state codec.
/// </summary>
internal static class YamlCodecSupport
{
    internal static readonly MethodInfo DeserializeMethod = typeof(YamlSerializer)
        .GetMethods()
        .First(m =>
            m.Name == nameof(YamlSerializer.Deserialize)
            && m.IsGenericMethod
            && m.GetParameters().Length == 2
            && m.GetParameters()[0].ParameterType == typeof(ReadOnlyMemory<byte>)
        );
    internal static readonly ConcurrentDictionary<Type, MethodInfo> DeserializeMethods = new();
    internal static readonly ConcurrentDictionary<
        Type,
        Func<ReadOnlyMemory<byte>, YamlSerializerOptions, object>
    > AotDeserializers = new();

    /// <summary>
    /// Registers a source-generated YAML deserializer for NativeAOT applications.
    /// </summary>
    internal static void Register<T>()
        where T : class, new()
    {
        AotDeserializers.TryAdd(
            typeof(T),
            static (bytes, options) => YamlSerializer.Deserialize<T>(bytes, options)
        );
    }

    internal static bool TryGetMetadataValue(YamlMapping metadata, string name, out string value)
    {
        if (metadata.Values.TryGetValue(name, out var yamlValue) && yamlValue is YamlScalar scalar)
        {
            value = scalar.Value;
            return true;
        }

        var camelCaseName = char.ToLowerInvariant(name[0]) + name.Substring(1);
        if (
            metadata.Values.TryGetValue(camelCaseName, out yamlValue)
            && yamlValue is YamlScalar camelCaseScalar
        )
        {
            value = camelCaseScalar.Value;
            return true;
        }

        value = string.Empty;
        return false;
    }

    internal static int? ReadOptionalVersion(
        YamlMapping metadata,
        string schemaVersionProperty,
        IReadOnlyList<string> schemaVersionFallbackProperties
    )
    {
        foreach (
            var propertyName in new[] { schemaVersionProperty }
                .Concat(schemaVersionFallbackProperties)
                .Distinct(StringComparer.Ordinal)
        )
        {
            if (!TryGetMetadataValue(metadata, propertyName, out var value))
            {
                continue;
            }

            return ConvertVersion(value, propertyName);
        }

        return null;
    }

    internal static int ConvertVersion(string value, string propertyName)
    {
        if (
            long.TryParse(
                value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var version
            )
        )
        {
            return version is >= int.MinValue and <= int.MaxValue
                ? (int)version
                : throw new FormatException(
                    $"YAML metadata property '{propertyName}' must be an integer."
                );
        }

        throw new FormatException($"YAML metadata property '{propertyName}' must be an integer.");
    }

    internal static ReadOnlyMemory<byte>? GetSectionBytes(
        ReadOnlyMemory<byte> yamlBytes,
        List<string> sectionNameParts
    )
    {
        if (sectionNameParts.Count == 0)
        {
            return yamlBytes;
        }

        var value = ParseYaml(yamlBytes);
        foreach (var section in sectionNameParts)
        {
            if (value is not YamlMapping mapping || !mapping.Values.TryGetValue(section, out value))
                return null;
        }

        return SerializeYaml(value);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "The reflection fallback is used only when a type was not explicitly registered for NativeAOT."
    )]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The reflection fallback is used only when a type was not explicitly registered for NativeAOT."
    )]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2060",
        Justification = "The reflection fallback is used only when a type was not explicitly registered for NativeAOT."
    )]
    internal static object Deserialize(
        Type type,
        ReadOnlyMemory<byte> yamlBytes,
        YamlSerializerOptions serializerOptions
    )
    {
        if (AotDeserializers.TryGetValue(type, out var deserializer))
        {
            return deserializer(yamlBytes, serializerOptions);
        }

        var genericMethod = DeserializeMethods.GetOrAdd(
            type,
            static type => DeserializeMethod.MakeGenericMethod(type)
        );
        var result = genericMethod.Invoke(null, new object[] { yamlBytes, serializerOptions });
        return result ?? Activator.CreateInstance(type)!;
    }

    internal static bool HasNonUtf8Bom(ReadOnlySpan<byte> yaml)
    {
        return yaml.Length >= 2
            && (
                (yaml[0] == 0xff && yaml[1] == 0xfe)
                || (yaml[0] == 0xfe && yaml[1] == 0xff)
                || (
                    yaml.Length >= 4
                    && yaml[0] == 0x00
                    && yaml[1] == 0x00
                    && yaml[2] == 0xfe
                    && yaml[3] == 0xff
                )
            );
    }

    internal static bool IsEmptyOrWhiteSpace(ReadOnlySpan<byte> yaml)
    {
        foreach (var value in yaml)
        {
            if (value is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                return false;
            }
        }

        return true;
    }

    internal static YamlMapping CreateSchemaMetadataDictionary<T>(
        T config,
        OptionsSchemaMetadata metadata,
        YamlSerializerOptions serializerOptions,
        string schemaVersionProperty
    )
        where T : class, new()
    {
        var yamlBytes = YamlSerializer.Serialize(config, serializerOptions);
        var mapping =
            ParseYaml(yamlBytes) as YamlMapping
            ?? throw new FormatException(
                "Options schema metadata can only be written for YAML mappings."
            );
        AddSchemaMetadata(mapping, metadata, schemaVersionProperty);
        return mapping;
    }

    internal static void AddSchemaMetadata(
        YamlMapping values,
        OptionsSchemaMetadata? metadata,
        string schemaVersionProperty
    )
    {
        if (metadata == null)
        {
            return;
        }

        if (metadata.Version is not null)
        {
            values.Values[schemaVersionProperty] = new YamlScalar(
                metadata.Version.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
        }
    }

    /// <summary>
    /// Merges a new configuration into an existing mapping at the specified section path.
    /// </summary>
    internal static void MergeSection(
        YamlMapping existingMapping,
        List<string> sections,
        int currentIndex,
        IYamlValue newValue
    )
    {
        if (currentIndex >= sections.Count)
        {
            return;
        }

        var sectionName = sections[currentIndex];

        if (currentIndex == sections.Count - 1)
        {
            // This is the final section - replace or add the value
            existingMapping.Values[sectionName] = newValue;
        }
        else
        {
            if (!existingMapping.Values.TryGetValue(sectionName, out var existing))
            {
                existing = new YamlMapping();
                existingMapping.Values[sectionName] = existing;
            }

            if (existing is not YamlMapping nestedMapping)
            {
                nestedMapping = new YamlMapping();
                existingMapping.Values[sectionName] = nestedMapping;
            }

            MergeSection(nestedMapping, sections, currentIndex + 1, newValue);
        }
    }

    internal static YamlMapping CreateNestedSection(
        IReadOnlyList<string> sections,
        YamlMapping value
    )
    {
        YamlMapping current = value;
        for (var index = sections.Count - 1; index >= 0; index--)
        {
            current = new YamlMapping { Values = { [sections[index]] = current } };
        }

        return current;
    }

    internal static IYamlValue ParseYaml(ReadOnlyMemory<byte> yamlBytes)
    {
        var parser = YamlParser.FromSequence(new ReadOnlySequence<byte>(yamlBytes));
        parser.SkipHeader();
        return ParseValue(ref parser);
    }

    internal static IYamlValue ParseValue(ref YamlParser parser)
    {
        return parser.CurrentEventType switch
        {
            ParseEventType.Scalar => ParseScalar(ref parser),
            ParseEventType.MappingStart => ParseMapping(ref parser),
            ParseEventType.SequenceStart => ParseSequence(ref parser),
            _ => throw new FormatException($"Unexpected YAML event: {parser.CurrentEventType}."),
        };
    }

    internal static YamlScalar ParseScalar(ref YamlParser parser)
    {
        var value = parser.GetScalarAsString() ?? "null";
        parser.ReadWithVerify(ParseEventType.Scalar);
        return new YamlScalar(value);
    }

    internal static YamlMapping ParseMapping(ref YamlParser parser)
    {
        var mapping = new YamlMapping();
        parser.ReadWithVerify(ParseEventType.MappingStart);
        while (!parser.End && parser.CurrentEventType != ParseEventType.MappingEnd)
        {
            if (parser.CurrentEventType != ParseEventType.Scalar)
                throw new FormatException("YAML mapping keys must be scalar values.");

            var key = parser.GetScalarAsString() ?? string.Empty;
            parser.ReadWithVerify(ParseEventType.Scalar);
            mapping.Values[key] = ParseValue(ref parser);
        }

        parser.ReadWithVerify(ParseEventType.MappingEnd);
        return mapping;
    }

    internal static YamlSequence ParseSequence(ref YamlParser parser)
    {
        var sequence = new YamlSequence();
        parser.ReadWithVerify(ParseEventType.SequenceStart);
        while (!parser.End && parser.CurrentEventType != ParseEventType.SequenceEnd)
        {
            sequence.Values.Add(ParseValue(ref parser));
        }

        parser.ReadWithVerify(ParseEventType.SequenceEnd);
        return sequence;
    }

    internal static ReadOnlyMemory<byte> SerializeYaml(IYamlValue value)
    {
        var writer = new ByteBufferWriter();
        var emitter = new Utf8YamlEmitter(writer);
        WriteYaml(ref emitter, value);
        return writer.WrittenMemory;
    }

    internal sealed class ByteBufferWriter : IBufferWriter<byte>
    {
        private byte[] buffer = new byte[256];
        private int written;

        public ReadOnlyMemory<byte> WrittenMemory => buffer.AsMemory(0, written);

        public void Advance(int count)
        {
            if (count < 0 || count > buffer.Length - written)
                throw new ArgumentOutOfRangeException(nameof(count));

            written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return buffer.AsMemory(written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return buffer.AsSpan(written);
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            var requiredLength = written + Math.Max(sizeHint, 1);
            if (requiredLength <= buffer.Length)
                return;

            var newLength = Math.Max(requiredLength, buffer.Length * 2);
            Array.Resize(ref buffer, newLength);
        }
    }

    internal static void WriteYaml(ref Utf8YamlEmitter emitter, IYamlValue value)
    {
        switch (value)
        {
            case YamlScalar scalar:
                emitter.WriteScalar(Encoding.UTF8.GetBytes(scalar.Value));
                break;
            case YamlMapping mapping:
                emitter.BeginMapping();
                foreach (var item in mapping.Values)
                {
                    emitter.WriteString(item.Key);
                    WriteYaml(ref emitter, item.Value);
                }
                emitter.EndMapping();
                break;
            case YamlSequence sequence:
                emitter.BeginSequence();
                foreach (var item in sequence.Values)
                {
                    WriteYaml(ref emitter, item);
                }
                emitter.EndSequence();
                break;
            default:
                throw new InvalidOperationException($"Unsupported YAML value: {value.GetType()}.");
        }
    }

    internal interface IYamlValue;

    internal sealed class YamlScalar(string value) : IYamlValue
    {
        public string Value { get; } = value;
    }

    internal sealed class YamlMapping : IYamlValue
    {
        public Dictionary<string, IYamlValue> Values { get; } = new(StringComparer.Ordinal);
    }

    internal sealed class YamlSequence : IYamlValue
    {
        public List<IYamlValue> Values { get; } = [];
    }
}
