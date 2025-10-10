using System.ComponentModel.DataAnnotations;

namespace Example.ConsoleApp;

public record SampleSetting
{
    [Required]
    [MinLength(3)]
    public string? Name { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}
