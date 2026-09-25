using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif

#if NET9_0_OR_GREATER
using System.Text.Json.Schema;
#endif

namespace Configuration.Writable;

/// <summary>Represents a generated JSON Schema document.</summary>
public sealed record JsonSchemaDocument(
    GeneratedOptionsModelMetadata Model,
    string FileName,
    JsonNode Schema
);

/// <summary>Describes a problem encountered while generating or writing JSON Schemas.</summary>
public sealed record JsonSchemaGenerationDiagnostic(
    string Code,
    string Message,
    Type? ModelType = null,
    string? ModelId = null,
    int? Version = null,
    string? OutputPath = null
);

/// <summary>Contains generated documents, written paths, and diagnostics.</summary>
public sealed class JsonSchemaGenerationResult
{
    internal JsonSchemaGenerationResult(
        IReadOnlyList<JsonSchemaDocument> documents,
        IReadOnlyList<string> writtenFiles,
        IReadOnlyList<JsonSchemaGenerationDiagnostic> diagnostics
    )
    {
        Documents = documents;
        WrittenFiles = writtenFiles;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the generated documents. Empty if schema generation failed.</summary>
    public IReadOnlyList<JsonSchemaDocument> Documents { get; }

    /// <summary>Gets the full paths successfully written by <see cref="JsonSchemaGenerator.Write"/>.</summary>
    public IReadOnlyList<string> WrittenFiles { get; }

    /// <summary>Gets the errors encountered during generation or output.</summary>
    public IReadOnlyList<JsonSchemaGenerationDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether generation or output completed without diagnostics.</summary>
    public bool Succeeded => Diagnostics.Count == 0;
}

/// <summary>Generates and writes JSON Schemas without requiring command-line processing.</summary>
public static class JsonSchemaGenerator
{
    /// <summary>
    /// Generates JSON Schema documents in memory for the supplied versioned options models.
    /// </summary>
    /// <param name="models">The models to generate, usually from <see cref="GeneratedOptionsSchemaRegistry.RegisteredModels"/>.</param>
    /// <param name="resolver">The JSON type-info resolver, preferably source-generated for trimming and NativeAOT.</param>
    /// <param name="schemaBaseUri">The configured base URI. When set, the exported configuration schema includes its schema-reference property.</param>
    public static JsonSchemaGenerationResult Generate(
        IEnumerable<GeneratedOptionsModelMetadata> models,
        IJsonTypeInfoResolver resolver,
        string? schemaBaseUri = null
    )
    {
        if (models is null)
            throw new ArgumentNullException(nameof(models));
        if (resolver is null)
            throw new ArgumentNullException(nameof(resolver));

        var diagnostics = new List<JsonSchemaGenerationDiagnostic>();
#if NET9_0_OR_GREATER
        var candidates = ValidateModels(models, diagnostics);
#else
        _ = ValidateModels(models, diagnostics);
#endif
        if (diagnostics.Count > 0)
            return CreateResult([], [], diagnostics);

#if !NET9_0_OR_GREATER
        diagnostics.Add(
            new JsonSchemaGenerationDiagnostic(
                "CWSC001",
                "JSON Schema export requires a target framework with System.Text.Json schema export support."
            )
        );
        return CreateResult([], [], diagnostics);
#else
        var documents = new List<JsonSchemaDocument>(candidates.Count);
        var jsonOptions = resolver is JsonSerializerContext context
            ? context.Options
            : new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = resolver };
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TransformSchemaNode = TransformSchemaNode,
        };

        foreach (var model in candidates)
        {
            try
            {
                var typeInfo = resolver.GetTypeInfo(model.ModelType, jsonOptions);
                if (typeInfo is null)
                {
                    diagnostics.Add(
                        CreateModelDiagnostic(
                            "CWSC002",
                            $"The configured JSON type-info resolver does not provide metadata for '{model.ModelType.FullName}'.",
                            model
                        )
                    );
                    continue;
                }

                var schema = JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo, exporterOptions);
                if (schema is null)
                {
                    diagnostics.Add(
                        CreateModelDiagnostic(
                            "CWSC003",
                            $"The JSON schema exporter returned no schema for '{model.ModelType.FullName}'.",
                            model
                        )
                    );
                    continue;
                }

                AddLibraryProperties(schema, model, schemaBaseUri is not null);
                documents.Add(
                    new JsonSchemaDocument(
                        model,
                        JsonSchemaGeneration.GetSchemaFileName(model.ModelId, model.Version),
                        schema
                    )
                );
            }
            catch (Exception exception)
                when (exception
                        is ArgumentException
                            or InvalidOperationException
                            or JsonException
                            or NotSupportedException
                )
            {
                diagnostics.Add(
                    CreateModelDiagnostic(
                        "CWSC004",
                        $"JSON Schema export failed for '{model.ModelType.FullName}': {exception.Message}",
                        model
                    )
                );
            }
        }

        return diagnostics.Count == 0
            ? CreateResult(documents, [], diagnostics)
            : CreateResult([], [], diagnostics);
#endif
    }

    /// <summary>
    /// Generates and writes JSON Schema documents for the supplied versioned options models.
    /// </summary>
    /// <param name="models">The models to generate, usually from <see cref="GeneratedOptionsSchemaRegistry.RegisteredModels"/>.</param>
    /// <param name="outputDirectory">The directory in which schema files are written.</param>
    /// <param name="resolver">The JSON type-info resolver, preferably source-generated for trimming and NativeAOT.</param>
    /// <param name="schemaBaseUri">The configured base URI. When set, the exported configuration schema includes its schema-reference property.</param>
    public static JsonSchemaGenerationResult Write(
        IEnumerable<GeneratedOptionsModelMetadata> models,
        string outputDirectory,
        IJsonTypeInfoResolver resolver,
        string? schemaBaseUri = null
    )
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException(
                "An output directory is required.",
                nameof(outputDirectory)
            );
        var generation = Generate(models, resolver, schemaBaseUri);
        if (!generation.Succeeded)
            return generation;

        string fullOutputDirectory;
        try
        {
            fullOutputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(fullOutputDirectory);
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or IOException
                        or NotSupportedException
                        or UnauthorizedAccessException
            )
        {
            return CreateResult(
                [],
                [],
                [
                    new JsonSchemaGenerationDiagnostic(
                        "CWSC005",
                        $"The schema output directory '{outputDirectory}' could not be created: {exception.Message}",
                        OutputPath: outputDirectory
                    ),
                ]
            );
        }

        var writtenFiles = new List<string>();
        var diagnostics = new List<JsonSchemaGenerationDiagnostic>();
        foreach (var document in generation.Documents)
        {
            var outputPath = Path.Combine(fullOutputDirectory, document.FileName);
            try
            {
                File.WriteAllText(
                    outputPath,
                    document.Schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                        + Environment.NewLine
                );
                writtenFiles.Add(outputPath);
            }
            catch (Exception exception)
                when (exception
                        is ArgumentException
                            or IOException
                            or NotSupportedException
                            or UnauthorizedAccessException
                )
            {
                diagnostics.Add(
                    CreateModelDiagnostic(
                        "CWSC006",
                        $"The JSON Schema for '{document.Model.ModelType.FullName}' could not be written: {exception.Message}",
                        document.Model,
                        outputPath
                    )
                );
            }
        }

        return CreateResult(generation.Documents, writtenFiles, diagnostics);
    }

    /// <summary>
    /// Checks the process command line for <c>--cw-generate-json-schema</c>, writes all registered
    /// model schemas, and exits with code zero when generation succeeds.
    /// </summary>
    /// <remarks>
    /// This method is provided for compatibility with the original command-line integration.
    /// Applications that own command-line parsing should call <see cref="Write"/> directly instead.
    /// </remarks>
    /// <param name="resolver">The JSON type-info resolver, preferably source-generated for trimming and NativeAOT.</param>
    /// <param name="schemaBaseUri">The configured base URI.</param>
    public static void JsonSchemaGenerationFromCommandLine(
        IJsonTypeInfoResolver resolver,
        string? schemaBaseUri = null
    )
    {
        if (resolver is null)
            throw new ArgumentNullException(nameof(resolver));
        var outputDirectory = JsonSchemaGeneration.GetCommandLineOutputDirectory(
            Environment.GetCommandLineArgs()
        );
        if (outputDirectory is null)
            return;

        var result = Write(
            GeneratedOptionsSchemaRegistry.RegisteredModels,
            outputDirectory,
            resolver,
            schemaBaseUri
        );
        if (!result.Succeeded)
            throw new JsonSchemaGenerationException(result);
        Environment.Exit(0);
    }

    /// <summary>
    /// Checks the process command line for <c>--cw-generate-json-schema</c>, writes all registered
    /// model schemas using runtime JSON metadata, and exits with code zero when generation succeeds.
    /// </summary>
    /// <remarks>This overload is not compatible with trimming or NativeAOT.</remarks>
    /// <param name="schemaBaseUri">The configured base URI.</param>
#if NET
    [RequiresUnreferencedCode(
        "The runtime JSON contract may require types that cannot be statically analyzed."
    )]
    [RequiresDynamicCode(
        "The runtime JSON contract may require runtime code generation and is not compatible with NativeAOT."
    )]
#endif
    public static void JsonSchemaGenerationFromCommandLine(string? schemaBaseUri = null)
    {
#if NET
        if (!RuntimeFeature.IsDynamicCodeSupported)
            throw new InvalidOperationException(
                "JSON schema generation requires an IJsonTypeInfoResolver when dynamic code is unavailable."
            );
#endif
        JsonSchemaGenerationFromCommandLine(new DefaultJsonTypeInfoResolver(), schemaBaseUri);
    }

    private static List<GeneratedOptionsModelMetadata> ValidateModels(
        IEnumerable<GeneratedOptionsModelMetadata> models,
        List<JsonSchemaGenerationDiagnostic> diagnostics
    )
    {
        var candidates = new List<GeneratedOptionsModelMetadata>();
        var seenModels = new HashSet<(Type ModelType, string ModelId, int Version)>();
        var outputNames = new Dictionary<string, GeneratedOptionsModelMetadata>(
            StringComparer.OrdinalIgnoreCase
        );
        foreach (var model in models)
        {
            if (model is null)
            {
                diagnostics.Add(
                    new JsonSchemaGenerationDiagnostic(
                        "CWSC007",
                        "A null options model metadata entry was supplied."
                    )
                );
                continue;
            }

            if (model.ModelType is null)
            {
                diagnostics.Add(
                    CreateModelDiagnostic("CWSC008", "The options model type is null.", model)
                );
                continue;
            }

            if (!JsonSchemaGeneration.IsValidModelId(model.ModelId))
            {
                diagnostics.Add(
                    CreateModelDiagnostic(
                        "CWSC009",
                        $"Options model ID '{model.ModelId}' cannot be used as a schema file name.",
                        model
                    )
                );
                continue;
            }

            if (model.Version <= 0)
            {
                diagnostics.Add(
                    CreateModelDiagnostic(
                        "CWSC010",
                        $"Options model version must be positive; received {model.Version}.",
                        model
                    )
                );
                continue;
            }

            if (!seenModels.Add((model.ModelType, model.ModelId, model.Version)))
                continue;

            var fileName = JsonSchemaGeneration.GetSchemaFileName(model.ModelId, model.Version);
            if (outputNames.TryGetValue(fileName, out var existing))
            {
                diagnostics.Add(
                    CreateModelDiagnostic(
                        "CWSC011",
                        $"Models '{existing.ModelType.FullName}' and '{model.ModelType.FullName}' map to the same schema file '{fileName}'.",
                        existing
                    )
                );
                diagnostics.Add(
                    CreateModelDiagnostic(
                        "CWSC011",
                        $"Models '{existing.ModelType.FullName}' and '{model.ModelType.FullName}' map to the same schema file '{fileName}'.",
                        model
                    )
                );
                continue;
            }

            outputNames.Add(fileName, model);
            candidates.Add(model);
        }

        return candidates
            .OrderBy(model => model.ModelId, StringComparer.Ordinal)
            .ThenBy(model => model.Version)
            .ToList();
    }

    private static JsonSchemaGenerationDiagnostic CreateModelDiagnostic(
        string code,
        string message,
        GeneratedOptionsModelMetadata model,
        string? outputPath = null
    ) => new(code, message, model.ModelType, model.ModelId, model.Version, outputPath);

    private static JsonSchemaGenerationResult CreateResult(
        IReadOnlyList<JsonSchemaDocument> documents,
        IReadOnlyList<string> writtenFiles,
        IReadOnlyList<JsonSchemaGenerationDiagnostic> diagnostics
    ) =>
        new(
            Array.AsReadOnly(documents.ToArray()),
            Array.AsReadOnly(writtenFiles.ToArray()),
            Array.AsReadOnly(diagnostics.ToArray())
        );

#if NET9_0_OR_GREATER
    private static JsonNode TransformSchemaNode(JsonSchemaExporterContext context, JsonNode node)
    {
        if (node is not JsonObject schema)
        {
            if (node is not JsonValue value || !value.TryGetValue<bool>(out var booleanSchema))
                return node;

            schema = booleanSchema
                ? new JsonObject()
                : new JsonObject { ["not"] = new JsonObject() };
        }

        var description = GetAttributes<DescriptionAttribute>(
                context.PropertyInfo?.AttributeProvider
            )
            .FirstOrDefault();
        if (description is not null)
            schema["description"] = description.Description;
        else
        {
            var displayDescription = GetAttributes<DisplayAttribute>(
                    context.PropertyInfo?.AttributeProvider
                )
                .FirstOrDefault()
                ?.Description;
            if (displayDescription is not null)
                schema["description"] = displayDescription;
        }

        var displayName = GetAttributes<DisplayAttribute>(context.PropertyInfo?.AttributeProvider)
            .FirstOrDefault()
            ?.Name;
        if (displayName is not null)
            schema["title"] = displayName;

        JsonSchemaValidationAttributeMapper.AddRequiredProperties(schema, context.TypeInfo);
        foreach (
            var attribute in GetAttributes<ValidationAttribute>(
                context.PropertyInfo?.AttributeProvider
            )
        )
            JsonSchemaValidationAttributeMapper.Apply(schema, attribute, context.TypeInfo);
        return schema;
    }

    private static IEnumerable<TAttribute> GetAttributes<TAttribute>(
        ICustomAttributeProvider? attributeProvider
    )
        where TAttribute : Attribute =>
        attributeProvider
            ?.GetCustomAttributes(typeof(TAttribute), inherit: true)
            .OfType<TAttribute>()
        ?? [];

    private static void AddLibraryProperties(
        JsonNode schema,
        GeneratedOptionsModelMetadata model,
        bool includeConfigurationSchema
    )
    {
        if (schema is not JsonObject root)
            throw new InvalidOperationException("The exported JSON schema root must be an object.");
        root["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        root["$id"] = JsonSchemaGeneration.GetSchemaFileName(model.ModelId, model.Version);
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
}

/// <summary>Thrown when the compatibility command-line schema generation fails.</summary>
public sealed class JsonSchemaGenerationException : InvalidOperationException
{
    internal JsonSchemaGenerationException(JsonSchemaGenerationResult result)
        : base(
            "JSON Schema generation failed: "
                + string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"
                    )
                )
        )
    {
        Result = result;
    }

    /// <summary>Gets the diagnostics produced by the failed operation.</summary>
    public JsonSchemaGenerationResult Result { get; }
}
