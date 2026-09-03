using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Configuration.Writable.Yaml.Tests;

[OptionsModel(Id = "YamlGeneratedSettings", Version = 1)]
[YamlObject]
public partial class YamlGeneratedSettingsV1
{
    public string Name { get; set; } = "";
}

[OptionsModel(Id = "YamlGeneratedSettings", Version = 2)]
[YamlObject]
public partial class YamlGeneratedSettingsV2
{
    public string[] Names { get; set; } = [];

    private static partial YamlGeneratedSettingsV2 Migrate(YamlGeneratedSettingsV1 source) =>
        new() { Names = [source.Name] };
}

public class YamlSourceGeneratedVersioningTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    [Fact]
    public async Task YamlProvider_ShouldPersistMetadataInsideSection()
    {
        const string fileName = "yaml-versioned.yaml";
        var instance = CreateInstance(fileName, "Application:Settings");

        await instance.GetOptions().SaveAsync(new YamlGeneratedSettingsV2 { Names = ["yaml"] });

        var document = YamlSerializer.Deserialize<Dictionary<string, object>>(
            Encoding.UTF8.GetBytes(_fileProvider.ReadAllText(fileName))
        )!;
        var application = (Dictionary<object, object>)document["Application"];
        var section = (Dictionary<object, object>)application["Settings"];
        section["ModelId"].ShouldBe("YamlGeneratedSettings");
        section["Version"].ToString().ShouldBe("2");
    }

    [Fact]
    public async Task YamlProvider_ShouldTreatMissingVersionAsVersionOne()
    {
        const string fileName = "yaml-legacy.yaml";
        await _fileProvider.SaveToFileAsync(
            fileName,
            Encoding.UTF8.GetBytes(
                """
                name: legacy
                """
            )
        );

        var loaded = CreateInstance(fileName, "").GetOptions().CurrentValue;

        loaded.Names.ShouldBe(["legacy"]);
    }

    private WritableOptionsSimpleInstance<YamlGeneratedSettingsV2> CreateInstance(
        string fileName,
        string sectionName
    )
    {
        var instance = new WritableOptionsSimpleInstance<YamlGeneratedSettingsV2>();
        instance.Initialize(options =>
        {
            options.FilePath = fileName;
            options.SectionName = sectionName;
            options.FormatProvider = new YamlFormatProvider();
            options.UseInMemoryFileProvider(_fileProvider);
        });
        return instance;
    }
}
