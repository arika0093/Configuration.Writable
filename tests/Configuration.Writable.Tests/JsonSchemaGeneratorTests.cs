#if NET9_0_OR_GREATER
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
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
    public void Generate_ShouldIncludeDataAnnotationConstraints()
    {
        var model = new GeneratedOptionsModelMetadata(typeof(AnnotatedSettings), "annotated", 1);

        var result = JsonSchemaGenerator.Generate([model], SourceGenTestConfigContext.Default);

        result.Succeeded.ShouldBeTrue();
        var schema = result.Documents[0].Schema;
        schema["properties"]!["MaxConnections"]!["minimum"]!.GetValue<decimal>().ShouldBe(1m);
        schema["properties"]!["MaxConnections"]!["maximum"]!.GetValue<decimal>().ShouldBe(1000m);
        schema["properties"]!["Name"]!["minLength"]!.GetValue<int>().ShouldBe(3);
        schema["properties"]!["Email"]!["minLength"]!.GetValue<int>().ShouldBe(1);
        schema["properties"]!["Email"]!["format"]!.GetValue<string>().ShouldBe("email");
        schema["required"]!.AsArray().Select(item => item!.GetValue<string>())
            .ShouldBe(["Email", "Name"]);
    }

    [Test]
    public void Generate_ShouldIncludeAdditionalDataAnnotationMetadata()
    {
        var model = new GeneratedOptionsModelMetadata(
            typeof(AdditionalAnnotatedSchemaModel),
            "additional-annotations",
            1
        );

        var result = JsonSchemaGenerator.Generate([model], SourceGenTestConfigContext.Default);

        result.Succeeded.ShouldBeTrue();
        var schema = result.Documents[0].Schema;
        schema["properties"]!["Label"]!["minLength"]!.GetValue<int>().ShouldBe(2);
        schema["properties"]!["Label"]!["maxLength"]!.GetValue<int>().ShouldBe(8);
        schema["properties"]!["OptionalLabel"]!.ToJsonString().ShouldContain("\"minLength\":2");
        schema["properties"]!["Tags"]!["minItems"]!.GetValue<int>().ShouldBe(1);
        schema["properties"]!["Tags"]!["maxItems"]!.GetValue<int>().ShouldBe(4);
        schema["properties"]!["AllowedState"]!["enum"]!.AsArray()
            .Select(item => item!.GetValue<string>())
            .ShouldBe(["red", "green"]);
        schema["properties"]!["EnumState"]!["enum"]!.AsArray()
            .Select(item => item!.GetValue<int>())
            .ShouldBe([1, 2]);
        schema["properties"]!["CurrentState"]!["not"]!["enum"]!.AsArray()
            .Select(item => item!.GetValue<string>())
            .ShouldBe(["retired", "legacy"]);
        schema["properties"]!["PublishedDate"]!["format"]!.GetValue<string>().ShouldBe("date");
        schema["properties"]!["PublishedDate"]!["title"]!.GetValue<string>()
            .ShouldBe("Published date");
        schema["properties"]!["PublishedDate"]!["description"]!.GetValue<string>()
            .ShouldBe("Date shown to users.");
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

[OptionsModel]
public partial class AdditionalAnnotatedSchemaModel
{
    [Length(2, 8)]
    public string Label { get; set; } = "";

    [Length(2, 8)]
    public string? OptionalLabel { get; set; }

    [MinLength(1)]
    [MaxLength(4)]
    public string[] Tags { get; set; } = [];

    [AllowedValues("red", "green")]
    public string AllowedState { get; set; } = "";

    [DeniedValues("retired", "legacy")]
    public string CurrentState { get; set; } = "";

    [AllowedValues(AdditionalAnnotatedSchemaValue.First, AdditionalAnnotatedSchemaValue.Second)]
    public AdditionalAnnotatedSchemaValue EnumState { get; set; }

    [Display(Name = "Published date", Description = "Date shown to users.")]
    [DataType(DataType.Date)]
    public string PublishedDate { get; set; } = "";
}

public enum AdditionalAnnotatedSchemaValue
{
    First = 1,
    Second = 2,
}
#endif
