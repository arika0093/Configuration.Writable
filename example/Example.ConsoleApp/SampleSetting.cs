using System.ComponentModel.DataAnnotations;
using IDeepCloneable;

namespace Example.ConsoleApp;

public partial record SampleSetting : IDeepCloneable<SampleSetting>
{
    [Required]
    [MinLength(3)]
    public string? Name { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}
