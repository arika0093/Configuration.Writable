using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Configuration.Writable.FileProvider;
using Configuration.Writable.FormatProvider;

namespace Configuration.Writable.Xml.Tests;

[OptionsModel(Id = "XmlGeneratedSettings", Version = 1)]
public partial class XmlGeneratedSettingsV1
{
    public string Name { get; set; } = "";
}

[OptionsModel(Id = "XmlGeneratedSettings", Version = 2)]
public partial class XmlGeneratedSettingsV2
{
    public string[] Names { get; set; } = [];

    public XmlGeneratedSettingsV2 Migrate(XmlGeneratedSettingsV1 source) =>
        new() { Names = [source.Name] };
}

public class XmlSourceGeneratedVersioningTests
{
    private readonly InMemoryFileProvider _fileProvider = new();

    [Test]
    public async Task XmlProvider_ShouldPersistMetadataInsideSection()
    {
        const string fileName = "xml-versioned.xml";
        var instance = CreateInstance(fileName, "Application:Settings");

        await instance.GetOptions().SaveAsync(new XmlGeneratedSettingsV2 { Names = ["xml"] });

        var document = XDocument.Parse(_fileProvider.ReadAllText(fileName));
        var section = document.Root!.Element("Application")!.Element("Settings")!;
        section.Element("Version")!.Value.ShouldBe("2");
        section.Element("ModelId").ShouldBeNull();
        section.Element("Names")!.Elements("string").Single().Value.ShouldBe("xml");
    }

    [Test]
    public async Task XmlProvider_ShouldTreatMissingVersionAsVersionOne()
    {
        const string fileName = "xml-legacy.xml";
        await _fileProvider.SaveToFileAsync(
            fileName,
            Encoding.UTF8.GetBytes(
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <Name>legacy</Name>
                </configuration>
                """
            )
        );

        var loaded = CreateInstance(fileName, "").GetOptions().CurrentValue;

        loaded.Names.ShouldBe(["legacy"]);
    }

    private WritableOptionsSimpleInstance<XmlGeneratedSettingsV2> CreateInstance(
        string fileName,
        string sectionName
    )
    {
        var instance = new WritableOptionsSimpleInstance<XmlGeneratedSettingsV2>();
        instance.Initialize(options =>
        {
            options.FilePath = fileName;
            options.SectionName = sectionName;
            options.FormatProvider = new XmlFormatProvider();
            options.UseInMemoryFileProvider(_fileProvider);
        });
        return instance;
    }
}
