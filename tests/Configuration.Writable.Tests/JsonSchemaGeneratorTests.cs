#if NET9_0_OR_GREATER
using System;
using System.IO;
using System.Text.Json.Nodes;

namespace Configuration.Writable.Tests;

public class JsonSchemaGeneratorTests
{
    [Test]
    public void Generate_ShouldReturnSchemaDocuments()
    {
        var models = new[]
        {
            new GeneratedOptionsModelMetadata(typeof(SourceGenTestConfig), "settings", 1),
        };

        var result = JsonSchemaGenerator.Generate(
            models,
            SourceGenTestConfigContext.Default,
            "https://example.test/schemas/"
        );

        result.Succeeded.ShouldBeTrue();
        result.Documents.Count.ShouldBe(1);
        result.Documents[0].FileName.ShouldBe("settings.v1.json");
        result.Documents[0].Schema["$id"]!.GetValue<string>().ShouldBe("settings.v1.json");
        result.Documents[0].Schema["properties"]!["$schema"]!["type"]!
            .GetValue<string>()
            .ShouldBe("string");
        result.WrittenFiles.ShouldBeEmpty();
    }

    [Test]
    public void Generate_ShouldReportMissingResolverMetadataWithModelIdentity()
    {
        var model = new GeneratedOptionsModelMetadata(typeof(UnregisteredSchemaModel), "missing", 3);

        var result = JsonSchemaGenerator.Generate([model], SourceGenTestConfigContext.Default);

        result.Succeeded.ShouldBeFalse();
        result.Documents.ShouldBeEmpty();
        result.Diagnostics.Count.ShouldBe(1);
        result.Diagnostics[0].Code.ShouldBe("CWSC002");
        result.Diagnostics[0].ModelType.ShouldBe(typeof(UnregisteredSchemaModel));
        result.Diagnostics[0].ModelId.ShouldBe("missing");
        result.Diagnostics[0].Version.ShouldBe(3);
    }

    [Test]
    public void Generate_ShouldDiagnoseConflictingOutputFiles()
    {
        var models = new[]
        {
            new GeneratedOptionsModelMetadata(typeof(SourceGenTestConfig), "duplicate", 1),
            new GeneratedOptionsModelMetadata(typeof(NestedConfig), "duplicate", 1),
        };

        var result = JsonSchemaGenerator.Generate(models, SourceGenTestConfigContext.Default);

        result.Succeeded.ShouldBeFalse();
        result.Documents.ShouldBeEmpty();
        result.Diagnostics.Count.ShouldBe(2);
        result.Diagnostics.ShouldAllBe(diagnostic =>
            diagnostic.Code == "CWSC011"
            && diagnostic.ModelId == "duplicate"
            && diagnostic.Version == 1
        );
    }

    [Test]
    public void Write_ShouldWriteGeneratedSchemasAndReturnPaths()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var models = new[]
        {
            new GeneratedOptionsModelMetadata(typeof(SourceGenTestConfig), "settings", 2),
        };

        try
        {
            var result = JsonSchemaGenerator.Write(
                models,
                outputDirectory,
                SourceGenTestConfigContext.Default
            );

            result.Succeeded.ShouldBeTrue();
            result.WrittenFiles.Count.ShouldBe(1);
            result.WrittenFiles[0].ShouldBe(Path.Combine(outputDirectory, "settings.v2.json"));
            JsonNode.Parse(File.ReadAllText(result.WrittenFiles[0]))!["$id"]!
                .GetValue<string>()
                .ShouldBe("settings.v2.json");
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Test]
    public void Write_ShouldReturnDiagnosticForInvalidOutputDirectory()
    {
        var result = JsonSchemaGenerator.Write(
            new[]
            {
                new GeneratedOptionsModelMetadata(typeof(SourceGenTestConfig), "settings", 1),
            },
            "invalid\0directory",
            SourceGenTestConfigContext.Default
        );

        result.Succeeded.ShouldBeFalse();
        result.Diagnostics.Count.ShouldBe(1);
        result.Diagnostics[0].Code.ShouldBe("CWSC005");
        result.Diagnostics[0].ModelType.ShouldBeNull();
    }

    private sealed class UnregisteredSchemaModel;
}
#endif
