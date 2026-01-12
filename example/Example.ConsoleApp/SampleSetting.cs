using System.ComponentModel.DataAnnotations;
using Configuration.Writable;

namespace Example.ConsoleApp;

[OptionsModel]
public partial class SampleSetting
{
    [Required]
    [MinLength(3)]
    public string? Name { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}
