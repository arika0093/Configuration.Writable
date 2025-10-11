using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Example.ConsoleApp;

public record SampleSetting
{
    [Required]
    [MinLength(3)]
    public string? Name { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}

[OptionsValidator]
public partial class SampleSettingValidator : IValidateOptions<SampleSetting>;
