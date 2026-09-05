using System.ComponentModel.DataAnnotations;
using Configuration.Writable;

namespace Example.ConsoleApp;

[OptionsModel(Id = "SampleSetting", Version = 1)]
public partial class SampleSetting
{
    [Required]
    [MinLength(3)]
    public string? Name { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}
