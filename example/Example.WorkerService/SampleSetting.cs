using System.Text.Json.Serialization;

namespace Example.WorkerService;

public record SampleSetting
{
    public string? Name { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;
