using System.Text.Json.Serialization;
using Configuration.Writable;

namespace Example.WorkerService;

public partial class SampleSetting : IOptionsModel<SampleSetting>
{
    public string? Name { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;
