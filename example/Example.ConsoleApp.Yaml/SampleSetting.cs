using Configuration.Writable;
using VYaml.Annotations;

namespace Example.ConsoleApp.Yaml;

[OptionsModel]
[YamlObject]
public partial class SampleSetting
{
    public string? Name { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}