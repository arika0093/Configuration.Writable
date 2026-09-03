using Configuration.Writable;

namespace Example.AbstractionsOnly;

public sealed class SettingsConsumer(IReadOnlyOptions<ApplicationSettings> options)
{
    public string CurrentName => options.CurrentValue.Name;
}

public sealed class ApplicationSettings
{
    public string Name { get; set; } = "";
}
