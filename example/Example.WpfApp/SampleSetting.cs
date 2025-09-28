using System.Text.Json.Serialization;

namespace Example.WpfApp;

public record SampleSetting
{
    public string? Name { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string LastUpdatedAtString
    {
        get => LastUpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");
        set => LastUpdatedAt = DateTime.Parse(value);
    }
}
