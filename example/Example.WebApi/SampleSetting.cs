using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Configuration.Writable;
using Microsoft.Extensions.Options;

namespace Example.WebApi;

public partial class SampleSetting : IOptionsModel<SampleSetting>
{
    [Required]
    [MinLength(3)]
    // MinLength attribute causes IL2026 warning in NativeAOT build, so suppress it here.
    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    public string? Name { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}

// Source generation context for System.Text.Json serialization
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;

// Source generation for options validation
[OptionsValidator]
public partial class SampleSettingValidator : IValidateOptions<SampleSetting>;
