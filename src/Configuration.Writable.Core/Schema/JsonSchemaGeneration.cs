using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
#if NET9_0_OR_GREATER
using System.Text.Json.Schema;
#endif

namespace Configuration.Writable;

internal static class JsonSchemaGeneration
{
    private const string CommandLineOption = "--cw-generate-json-schema";

    public static void GenerateIfRequested(
        bool enabled,
        IJsonTypeInfoResolver? resolver,
        string? schemaBaseUri
    )
    {
        var args = Environment.GetCommandLineArgs();
        var optionIndex = Array.IndexOf(args, CommandLineOption);
        if (!enabled || optionIndex < 0)
            return;
        if (optionIndex + 1 >= args.Length || string.IsNullOrWhiteSpace(args[optionIndex + 1]))
            throw new ArgumentException(
                $"The {CommandLineOption} option requires an output directory."
            );
#if NET
        if (!RuntimeFeature.IsDynamicCodeSupported && resolver is null)
            throw new InvalidOperationException(
                "JSON schema generation requires an IJsonTypeInfoResolver when dynamic code is unavailable."
            );
#endif
#if !NET9_0_OR_GREATER
        throw new PlatformNotSupportedException(
            "JSON schema generation requires a target framework with System.Text.Json schema export support."
        );
#else

        var outputDirectory = Path.GetFullPath(args[optionIndex + 1]);
        Directory.CreateDirectory(outputDirectory);
        var effectiveResolver = resolver ?? new DefaultJsonTypeInfoResolver();
        var jsonOptions = resolver is JsonSerializerContext context
            ? context.Options
            : new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = effectiveResolver,
            };
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TransformSchemaNode = static (context, node) =>
            {
                var description = context
                    .PropertyInfo?.AttributeProvider?.GetCustomAttributes(
                        typeof(DescriptionAttribute),
                        true
                    )
                    .OfType<DescriptionAttribute>()
                    .FirstOrDefault();
                if (description is not null && node is JsonObject schema)
                    schema["description"] = description.Description;
                return node;
            },
        };

        foreach (
            var model in GeneratedOptionsSchemaRegistry
                .RegisteredModels.OrderBy(model => model.ModelId, StringComparer.Ordinal)
                .ThenBy(model => model.Version)
        )
        {
            ValidateModelId(model.ModelId);
            var typeInfo =
                effectiveResolver.GetTypeInfo(model.ModelType, jsonOptions)
                ?? throw new InvalidOperationException(
                    $"The configured JSON type-info resolver does not provide metadata for '{model.ModelType.FullName}'."
                );
            var schema =
                JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo, exporterOptions)
                ?? throw new InvalidOperationException(
                    $"The JSON schema exporter returned no schema for '{model.ModelType.FullName}'."
                );
            AddLibraryProperties(schema, model, schemaBaseUri is not null);
            var fileName = GetSchemaFileName(model.ModelId, model.Version);
            var path = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(
                path,
                schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                    + Environment.NewLine
            );
        }
        Environment.Exit(0);
#endif
    }

    internal static string? ResolveSchemaReference(
        string? schemaBaseUri,
        OptionsSchemaMetadata? metadata
    )
    {
        if (
            string.IsNullOrWhiteSpace(schemaBaseUri)
            || metadata?.ModelId is null
            || metadata.Version is not > 0
        )
            return null;
        ValidateModelId(metadata.ModelId);
        var fileName = GetSchemaFileName(metadata.ModelId, metadata.Version.Value);
        if (Uri.TryCreate(schemaBaseUri, UriKind.Absolute, out var baseUri))
#pragma warning disable S1075
            return new Uri(
                baseUri,
                baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
                    ? fileName
                    : "/" + fileName
            ).ToString();
#pragma warning restore S1075
#if NETSTANDARD2_0
        schemaBaseUri = schemaBaseUri ?? throw new InvalidOperationException();
#endif
        return schemaBaseUri.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar
            + fileName;
    }

    internal static string GetSchemaFileName(string modelId, int version)
    {
        ValidateModelId(modelId);
        return modelId + ".v" + version + ".json";
    }

#if NET9_0_OR_GREATER
    private static void AddLibraryProperties(
        JsonNode schema,
        GeneratedOptionsModelMetadata model,
        bool includeConfigurationSchema
    )
    {
        if (schema is not JsonObject root)
            throw new InvalidOperationException("The exported JSON schema root must be an object.");
        root["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        root["$id"] = GetSchemaFileName(model.ModelId, model.Version);
        if (root["properties"] is not JsonObject properties)
        {
            properties = [];
            root["properties"] = properties;
        }
        properties["$version"] = new JsonObject { ["type"] = "integer" };
        if (includeConfigurationSchema)
            properties["$schema"] = new JsonObject { ["type"] = "string" };
    }
#endif

    private static void ValidateModelId(string modelId)
    {
        if (
            string.IsNullOrWhiteSpace(modelId)
            || modelId is "." or ".."
            || modelId.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0
        )
            throw new InvalidOperationException(
                $"Options model ID '{modelId}' cannot be used as a schema file name."
            );
    }
}
