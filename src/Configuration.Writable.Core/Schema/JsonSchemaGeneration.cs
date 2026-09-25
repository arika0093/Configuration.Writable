using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif

namespace Configuration.Writable;

internal static class JsonSchemaGeneration
{
    private const string CommandLineOption = "--cw-generate-json-schema";

#if NET
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The default resolver is only created when dynamic code is supported; AOT callers must provide a source-generated resolver."
    )]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The default resolver is only created when dynamic code is supported; AOT callers must provide a source-generated resolver."
    )]
#endif
    public static void GenerateIfRequested(
        bool enabled,
        IJsonTypeInfoResolver? resolver,
        string? schemaBaseUri
    )
    {
        if (!enabled)
            return;
        if (GetCommandLineOutputDirectory(Environment.GetCommandLineArgs()) is null)
            return;
#if NET
        if (!RuntimeFeature.IsDynamicCodeSupported && resolver is null)
            throw new InvalidOperationException(
                "JSON schema generation requires an IJsonTypeInfoResolver when dynamic code is unavailable."
            );
#endif
        JsonSchemaGenerator.JsonSchemaGenerationFromCommandLine(
            resolver ?? new DefaultJsonTypeInfoResolver(),
            schemaBaseUri
        );
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

    internal static bool IsValidModelId(string modelId) =>
        !string.IsNullOrWhiteSpace(modelId)
        && modelId is not "." and not ".."
        && modelId.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) < 0;

    internal static void ValidateModelId(string modelId)
    {
        if (!IsValidModelId(modelId))
            throw new InvalidOperationException(
                $"Options model ID '{modelId}' cannot be used as a schema file name."
            );
    }

    internal static string? GetCommandLineOutputDirectory(string[] arguments)
    {
        var optionIndex = Array.IndexOf(arguments, CommandLineOption);
        if (optionIndex < 0)
            return null;
        if (
            optionIndex + 1 >= arguments.Length
            || string.IsNullOrWhiteSpace(arguments[optionIndex + 1])
        )
            throw new ArgumentException(
                $"The {CommandLineOption} option requires an output directory."
            );
        return arguments[optionIndex + 1];
    }
}
