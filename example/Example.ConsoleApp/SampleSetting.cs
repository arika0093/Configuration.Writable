using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Example.ConsoleApp;

public record SampleSetting
{
    [Required]
    [MinLength(3)]
    public string? Name { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;

[OptionsValidator]
public partial class SampleSettingValidator : IValidateOptions<SampleSetting>;
