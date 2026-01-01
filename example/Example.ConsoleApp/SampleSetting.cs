using System.ComponentModel.DataAnnotations;
using Configuration.Writable;

namespace Example.ConsoleApp;

public partial class SampleSetting : IOptionsModel<SampleSetting>
{
    [Required]
    [MinLength(3)]
    public string? Name { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}
