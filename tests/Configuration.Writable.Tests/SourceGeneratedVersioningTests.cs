using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using Configuration.Writable.Generator;
using Configuration.Writable.Migration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Configuration.Writable.Tests;

[OptionsModel(Id = "GeneratedSettings", Version = 1)]
public partial class GeneratedSettingsV1
{
    public string Name { get; set; } = "";
}

[OptionsModel(Id = "GeneratedSettings", Version = 2)]
public partial class GeneratedSettingsV2
{
    public string[] Names { get; set; } = [];

    public GeneratedSettingsV2 Migrate(GeneratedSettingsV1 source) =>
        new() { Names = [source.Name] };
}

[OptionsModel(Id = "GeneratedSettings", Version = 3)]
public partial class GeneratedSettingsV3
{
    public List<string> Names { get; set; } = [];

    public GeneratedSettingsV3 Migrate(GeneratedSettingsV2 source) =>
        new() { Names = [.. source.Names] };
}

[OptionsModel(Id = "CutoffSettings", Version = 1)]
public partial class CutoffSettingsV1
{
    public string LegacyValue { get; set; } = "";
}

[OptionsModel(Id = "CutoffSettings", Version = 2, SupportMigration = false)]
public partial class CutoffSettingsV2
{
    public string CurrentValue { get; set; } = "";
}

[OptionsModel(Id = "GeneratedDefaultVersion")]
public partial class GeneratedDefaultVersionSettings
{
    public string Value { get; set; } = "";
}

[JsonSerializable(typeof(GeneratedSettingsV1))]
[JsonSerializable(typeof(GeneratedSettingsV2))]
[JsonSerializable(typeof(GeneratedSettingsV3))]
internal partial class GeneratedVersioningJsonContext : JsonSerializerContext;

public class SourceGeneratedVersioningTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    [Fact]
    public void GeneratedMetadata_ShouldExposeIdVersionAndMigrationChain()
    {
        var metadata = (IGeneratedOptionsMetadata)new GeneratedSettingsV3();
        var builder = new WritableOptionsConfigBuilder<GeneratedSettingsV3>
        {
            FilePath = "generated.json",
            FileProvider = _fileProvider,
        };

        var options = builder.BuildOptions("");

        metadata.ModelId.ShouldBe("GeneratedSettings");
        metadata.Version.ShouldBe(3);
        options.SchemaMetadata.ShouldBe(new OptionsSchemaMetadata("GeneratedSettings", 3));
        options.MigrationSteps.Count.ShouldBe(2);
        options.MigrationSteps.Select(step => step.FromVersion).ShouldBe([1, 2]);
        options.MigrationSteps.Select(step => step.ToVersion).ShouldBe([2, 3]);
    }

    [Fact]
    public async Task GeneratedMigration_ShouldLoadLegacyNumericVersionFile()
    {
        const string fileName = "generated-legacy.json";
        await _fileProvider.SaveToFileAsync(
            fileName,
            Encoding.UTF8.GetBytes("""{"Version":1,"Name":"legacy"}""")
        );
        var builder = new WritableOptionsConfigBuilder<GeneratedSettingsV3>
        {
            FilePath = fileName,
            FileProvider = _fileProvider,
        };

        var result = new JsonFormatProvider().LoadWithMigration(builder.BuildOptions(""));

        result.Names.ShouldBe(["legacy"]);
    }

    [Fact]
    public async Task DisabledMigrationSupport_ShouldUseDefaultsForOlderVersion()
    {
        const string fileName = "generated-cutoff.json";
        await _fileProvider.SaveToFileAsync(
            fileName,
            Encoding.UTF8.GetBytes(
                """{"ModelId":"CutoffSettings","Version":1,"LegacyValue":"legacy"}"""
            )
        );
        var builder = new WritableOptionsConfigBuilder<CutoffSettingsV2>
        {
            FilePath = fileName,
            FileProvider = _fileProvider,
        };

        var result = new JsonFormatProvider().LoadWithMigration(builder.BuildOptions(""));

        result.CurrentValue.ShouldBe("");
        _fileProvider.BackupAttemptCount.ShouldBe(1);
    }

    [Fact]
    public async Task MigrationLoad_ShouldRejectNewerVersion()
    {
        const string fileName = "generated-future.json";
        await _fileProvider.SaveToFileAsync(
            fileName,
            Encoding.UTF8.GetBytes(
                """{"ModelId":"CutoffSettings","Version":3,"CurrentValue":"future"}"""
            )
        );
        var builder = new WritableOptionsConfigBuilder<CutoffSettingsV2>
        {
            FilePath = fileName,
            FileProvider = _fileProvider,
        };

        var exception = Should.Throw<InvalidOperationException>(() =>
            new JsonFormatProvider().LoadWithMigration(builder.BuildOptions(""))
        );

        exception.Message.ShouldContain("newer than supported");
    }

    [Fact]
    public async Task JsonProvider_ShouldPersistMetadataInsideConfiguredSection()
    {
        const string fileName = "generated-section.json";
        var builder = new WritableOptionsConfigBuilder<GeneratedSettingsV3>
        {
            FilePath = fileName,
            FileProvider = _fileProvider,
            SectionName = "Application:Settings",
        };
        var options = builder.BuildOptions("");

        await options.FormatProvider.SaveAsync(
            new GeneratedSettingsV3 { Names = ["one"] },
            options
        );

        using var document = JsonDocument.Parse(_fileProvider.ReadAllText(fileName));
        var section = document.RootElement.GetProperty("Application").GetProperty("Settings");
        section.GetProperty("ModelId").GetString().ShouldBe("GeneratedSettings");
        section.GetProperty("Version").GetInt32().ShouldBe(3);
        section.GetProperty("Names")[0].GetString().ShouldBe("one");
    }

    [Fact]
    public async Task JsonAotProvider_ShouldPersistAndLoadGeneratedMetadata()
    {
        const string fileName = "generated-aot.json";
        var provider = new JsonAotFormatProvider(GeneratedVersioningJsonContext.Default);
        var builder = new WritableOptionsConfigBuilder<GeneratedSettingsV3>
        {
            FilePath = fileName,
            FileProvider = _fileProvider,
            FormatProvider = provider,
        };
        var options = builder.BuildOptions("");

        await provider.SaveAsync(new GeneratedSettingsV3 { Names = ["aot"] }, options);
        var loaded = provider.LoadWithMigration(options);

        loaded.Names.ShouldBe(["aot"]);
        _fileProvider.ReadAllText(fileName).ShouldContain("\"ModelId\"");
        _fileProvider.ReadAllText(fileName).ShouldContain("\"Version\"");
    }

    [Fact]
    public async Task Load_ShouldRejectDifferentModelId()
    {
        const string fileName = "wrong-model.json";
        await _fileProvider.SaveToFileAsync(
            fileName,
            Encoding.UTF8.GetBytes("""{"ModelId":"OtherSettings","Version":3,"Names":[]}""")
        );
        var builder = new WritableOptionsConfigBuilder<GeneratedSettingsV3>
        {
            FilePath = fileName,
            FileProvider = _fileProvider,
        };

        var exception = Should.Throw<InvalidOperationException>(() =>
            new JsonFormatProvider().LoadWithMigration(builder.BuildOptions(""))
        );

        exception.Message.ShouldContain("OtherSettings");
        exception.Message.ShouldContain("GeneratedSettings");
    }

    [Fact]
    public void BuildOptions_ShouldRejectProviderWithoutSchemaMetadataCapability()
    {
        var builder = new WritableOptionsConfigBuilder<GeneratedSettingsV3>
        {
            FormatProvider = new MetadataUnsupportedProvider(),
        };

        Should
            .Throw<InvalidOperationException>(() => builder.BuildOptions(""))
            .Message.ShouldContain("does not support options schema metadata");
    }

    [Fact]
    public async Task OmittedVersion_ShouldDefaultToOneAndPersistMetadata()
    {
        var builder = new WritableOptionsConfigBuilder<GeneratedDefaultVersionSettings>
        {
            FilePath = "unversioned.json",
            FileProvider = _fileProvider,
        };
        var options = builder.BuildOptions("");

        await options.FormatProvider.SaveAsync(
            new GeneratedDefaultVersionSettings { Value = "value" },
            options
        );

        var json = _fileProvider.ReadAllText("unversioned.json");
        json.ShouldContain("\"ModelId\":\"GeneratedDefaultVersion\"");
        json.ShouldContain("\"Version\":1");
    }

    [Fact]
    public void AggregatedAndProfiledBuilders_ShouldRegisterGeneratedMetadata()
    {
        var services = new ServiceCollection();
        services.AddWritableOptions(builder =>
        {
            builder.FileProvider = _fileProvider;
            builder.Add<GeneratedSettingsV3>(options => options.FilePath = "aggregated.json");
        });
        using var provider = services.BuildServiceProvider();
        var aggregated = provider.GetRequiredService<
            WritableOptionsConfiguration<GeneratedSettingsV3>
        >();

        var profiledBuilder = new ProfiledOptionsConfigBuilder<GeneratedSettingsV3>
        {
            FilePath = "profiled.json",
            FileProvider = _fileProvider,
        };
        var profiled = profiledBuilder.Build();

        aggregated.SchemaMetadata.ShouldBe(new OptionsSchemaMetadata("GeneratedSettings", 3));
        aggregated.MigrationSteps.Count.ShouldBe(2);
        profiled.Template.SchemaMetadata.ShouldBe(aggregated.SchemaMetadata);
        profiled.Template.MigrationSteps.Count.ShouldBe(2);
    }

    [Fact]
    public async Task StaticInitialization_ShouldPersistGeneratedMetadata()
    {
        const string fileName = "static-generated.json";
        WritableOptions.Initialize<GeneratedDefaultVersionSettings>(options =>
        {
            options.FilePath = fileName;
            options.FileProvider = _fileProvider;
        });

        await WritableOptions
            .GetOptions<GeneratedDefaultVersionSettings>()
            .SaveAsync(new GeneratedDefaultVersionSettings { Value = "static" });

        _fileProvider.ReadAllText(fileName).ShouldContain("GeneratedDefaultVersion");
    }

    private sealed class MetadataUnsupportedProvider : IWritableFormatProvider
    {
        public string FileExtension => "test";

        public object LoadConfiguration(Type type, IWritableOptionsConfiguration options) =>
            new GeneratedSettingsV3();

        public ValueTask<object> LoadConfigurationAsync(
            Type type,
            PipeReader reader,
            List<string> sectionNameParts,
            CancellationToken cancellationToken = default
        ) => new(new GeneratedSettingsV3());

        public Task SaveAsync<T>(
            T config,
            IWritableOptionsConfiguration options,
            CancellationToken cancellationToken = default
        )
            where T : class, new() => Task.CompletedTask;
    }
}

public class OptionsVersioningGeneratorTests
{
    [Theory]
    [InlineData("[OptionsModel] public partial class Model {}", "CWWR001")]
    [InlineData("[OptionsModel(Id = \"Model\")] public partial class Model {}", "CWWR010")]
    [InlineData(
        "[OptionsModel(Id = \"Model\", Version = 0)] public partial class Model {}",
        "CWWR003"
    )]
    [InlineData(
        "[OptionsModel(Id = \"Model\", Version = 2)] public partial class Model {}",
        "CWWR005"
    )]
    [InlineData(
        "[OptionsModel(Id = \"Model\")] public partial class Model { public int Version { get; set; } }",
        "CWWR006"
    )]
    [InlineData(
        "[OptionsModel(Id = \"Model\")] public partial class Model : IHasVersion { public int Version { get; set; } = 1; }",
        "CWWR007"
    )]
    public void Generator_ShouldReportExpectedDiagnostic(string declaration, string diagnosticId)
    {
        var result = RunGenerator(
            $$"""
            using Configuration.Writable;
            namespace GeneratorTest;
            {{declaration}}
            """
        );

        result.Diagnostics.Select(diagnostic => diagnostic.Id).ShouldContain(diagnosticId);
    }

    [Fact]
    public void Generator_ShouldDetectDuplicateAndSerializedReservedNames()
    {
        var result = RunGenerator(
            """
            using Configuration.Writable;
            using System.Text.Json.Serialization;
            [OptionsModel(Id = "Duplicate", Version = 1)]
            public partial class First {}
            [OptionsModel(Id = "Duplicate", Version = 1)]
            public partial class Second
            {
                [JsonPropertyName("ModelId")]
                public string Identifier { get; set; } = "";
            }
            """
        );

        result.Diagnostics.Select(diagnostic => diagnostic.Id).ShouldContain("CWWR004");
        result.Diagnostics.Select(diagnostic => diagnostic.Id).ShouldContain("CWWR006");
    }

    [Fact]
    public void Generator_ShouldIgnoreUnrelatedAttributeStringArguments()
    {
        var result = RunGenerator(
            """
            using System.ComponentModel;
            using Configuration.Writable;
            [OptionsModel(Id = "Model", Version = 1)]
            public partial class Model
            {
                [DefaultValue("Version")]
                public string SchemaRevision { get; set; } = "";
            }
            """
        );

        result.Diagnostics.Select(diagnostic => diagnostic.Id).ShouldNotContain("CWWR006");
    }

    [Fact]
    public void Generator_ShouldIgnoreReservedNameOnIgnoredMember()
    {
        var result = RunGenerator(
            """
            using System.Text.Json.Serialization;
            using Configuration.Writable;
            [OptionsModel(Id = "Model", Version = 1)]
            public partial class Model
            {
                [JsonIgnore]
                public string Version { get; set; } = "";
            }
            """
        );

        result.Diagnostics.Select(diagnostic => diagnostic.Id).ShouldNotContain("CWWR006");
    }

    [Fact]
    public void Generator_ShouldDefaultOmittedVersionToOneAndWarn()
    {
        var result = RunGenerator(
            """
            using Configuration.Writable;
            [OptionsModel(Id = "DefaultVersion")]
            public partial class Model {}
            """
        );

        result.Diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["CWWR010"]);
        result
            .Results.SelectMany(generatorResult => generatorResult.GeneratedSources)
            .Single()
            .SourceText.ToString()
            .ShouldContain(".Version => 1;");
    }

    [Fact]
    public void Generator_ShouldAllowStartingNewCompatibilityChain()
    {
        var result = RunGenerator(
            """
            using Configuration.Writable;
            [OptionsModel(Id = "Settings", Version = 10, SupportMigration = false)]
            public partial class Settings {}
            """
        );

        result.Diagnostics.Select(diagnostic => diagnostic.Id).ShouldNotContain("CWWR005");
        result
            .Results.SelectMany(generatorResult => generatorResult.GeneratedSources)
            .Single()
            .SourceText.ToString()
            .ShouldNotContain(" Migrate(");
    }

    [Fact]
    public void Generator_ShouldAddMigrationInterfaceToCurrentVersion()
    {
        var result = RunGenerator(
            """
            using Configuration.Writable;
            [OptionsModel(Id = "Settings", Version = 1)]
            public partial class SettingsV1 {}
            [OptionsModel(Id = "Settings", Version = 2)]
            public partial class SettingsV2
            {
                public SettingsV2 Migrate(SettingsV1 source) => new();
            }
            """
        );

        var generated = result
            .Results.SelectMany(generatorResult => generatorResult.GeneratedSources)
            .Single(source => source.HintName.Contains("SettingsV2", StringComparison.Ordinal))
            .SourceText.ToString();
        generated.ShouldContain(
            "global::Configuration.Writable.IOptionsMigration<global::SettingsV1, global::SettingsV2>"
        );
        generated.ShouldNotContain("partial global::SettingsV2 Migrate");
    }

    [Fact]
    public void Generator_ShouldDiscoverPreviousVersionFromReferencedAssembly()
    {
        var versionOne = CompileReference(
            "CrossAssemblyV1",
            """
            using Configuration.Writable;
            [OptionsModel(Id = "CrossAssembly", Version = 1)]
            public partial class CrossAssemblyV1 {}
            """
        );
        var versionTwo = CompileReference(
            "CrossAssemblyV2",
            """
            using Configuration.Writable;
            [OptionsModel(Id = "CrossAssembly", Version = 2)]
            public partial class CrossAssemblyV2
            {
                public CrossAssemblyV2 Migrate(CrossAssemblyV1 source) => new();
            }
            """,
            versionOne
        );
        var result = RunGenerator(
            """
            using Configuration.Writable;
            [OptionsModel(Id = "CrossAssembly", Version = 3)]
            public partial class CrossAssemblyV3
            {
                public CrossAssemblyV3 Migrate(CrossAssemblyV2 source) => new();
            }
            """,
            versionOne,
            versionTwo
        );

        result
            .Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();
        result
            .Results.SelectMany(generatorResult => generatorResult.GeneratedSources)
            .Single()
            .SourceText.ToString()
            .ShouldContain("new global::CrossAssemblyV2()");
    }

    private static GeneratorDriverRunResult RunGenerator(
        string source,
        params MetadataReference[] additionalReferences
    )
    {
        var references = GetReferences().ToList();
        references.AddRange(additionalReferences);
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new OptionsVersioningGenerator().AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview)
        );
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static MetadataReference CompileReference(
        string assemblyName,
        string source,
        params MetadataReference[] additionalReferences
    )
    {
        var references = GetReferences().Concat(additionalReferences);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new OptionsVersioningGenerator().AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview)
        );
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        using var stream = new MemoryStream();
        var emitResult = output.Emit(stream);
        emitResult.Success.ShouldBeTrue(string.Join(Environment.NewLine, emitResult.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        var platformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable.");
        var paths = platformAssemblies
            .Split(Path.PathSeparator)
            .Append(typeof(OptionsModelAttribute).Assembly.Location)
            .Append(typeof(IHasVersion).Assembly.Location);
        return paths.Distinct().Select(path => MetadataReference.CreateFromFile(path));
    }
}
