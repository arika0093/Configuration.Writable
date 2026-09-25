#if NET9_0_OR_GREATER
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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
        schema["properties"]!["Email"]!["pattern"]!.GetValue<string>().ShouldBe("\\S");
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
    public void Generate_ShouldMapRequiredAndExclusiveRangeEdgeCases()
    {
        var model = new GeneratedOptionsModelMetadata(
            typeof(EdgeCaseAnnotatedSchemaModel),
            "edge-cases",
            1
        );

        var result = JsonSchemaGenerator.Generate([model], SourceGenTestConfigContext.Default);

        result.Succeeded.ShouldBeTrue();
        var properties = result.Documents[0].Schema["properties"]!;
        properties["OptionalName"]!["not"]!["type"]!.GetValue<string>().ShouldBe("null");
        properties["RequiredName"]!["pattern"]!.GetValue<string>().ShouldBe("\\S");
        properties["AllowEmptyName"]!["pattern"].ShouldBeNull();
        properties["RequiredCount"]!["not"]!["type"]!.GetValue<string>().ShouldBe("null");
        properties["ExclusiveRange"]!["exclusiveMinimum"]!.GetValue<decimal>().ShouldBe(1m);
        properties["ExclusiveRange"]!["exclusiveMaximum"]!.GetValue<decimal>().ShouldBe(10m);
        properties["SupportedPattern"]!["pattern"]!.GetValue<string>().ShouldBe("\\S");
        properties["SupportedPattern"]!["allOf"]![0]!["pattern"]!.GetValue<string>()
            .ShouldBe("^[A-Z]+$");
        properties["UnsupportedPattern"]!["pattern"].ShouldBeNull();
        properties["UnicodeDigitPattern"]!["pattern"].ShouldBeNull();
        properties["BooleanSchema"]!["description"]!.GetValue<string>()
            .ShouldBe("Boolean converter description.");
        properties["BooleanSchema"]!["not"]!["type"]!.GetValue<string>().ShouldBe("null");
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

[OptionsModel]
public partial class EdgeCaseAnnotatedSchemaModel
{
    [Required]
    public string? OptionalName { get; set; }

    [Required]
    public string RequiredName { get; set; } = "";

    [Required(AllowEmptyStrings = true)]
    public string AllowEmptyName { get; set; } = "";

    [Required]
    public int? RequiredCount { get; set; }

    [Range(1, 10, MinimumIsExclusive = true, MaximumIsExclusive = true)]
    public int ExclusiveRange { get; set; }

    [Required]
    [RegularExpression("^[A-Z]+$")]
    public string SupportedPattern { get; set; } = "";

    [RegularExpression(@"\A[a-z]+\z")]
    public string UnsupportedPattern { get; set; } = "";

    [RegularExpression(@"^\d+$")]
    public string UnicodeDigitPattern { get; set; } = "";

    [Required]
    [Display(Description = "Boolean converter description.")]
    [JsonConverter(typeof(BooleanSchemaStringConverter))]
    public string BooleanSchema { get; set; } = "";
}

public sealed class BooleanSchemaStringConverter : JsonConverter<string>
{
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => reader.GetString();

    public override void Write(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value);

}
#endif
