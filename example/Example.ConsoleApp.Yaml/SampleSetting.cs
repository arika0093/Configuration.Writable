using System.Text.Json.Serialization;
using Configuration.Writable;
using VYaml.Annotations;

namespace Example.ConsoleApp.Yaml;

[OptionsModel(Id = "SampleSetting", Version = 1)]
[YamlObject]
public partial class SampleSetting
{
    public string? Name { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;
