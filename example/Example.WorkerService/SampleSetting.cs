using System.Text.Json.Serialization;
using Configuration.Writable;

namespace Example.WorkerService;

[OptionsModel(Id = "SampleSetting", Version = 1)]
public partial class SampleSetting
{
    public string? Name { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;
