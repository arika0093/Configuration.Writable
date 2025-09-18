using System;
using System.Threading.Tasks;
using Configuration.Writable.Imprements;
using Microsoft.Extensions.Options;

namespace Configuration.Writable;

internal class WritableJsonConfiguration<T> : WritableConfigurationBase<T>
    where T : class
{
    private readonly IOptionsMonitor<WritableJsonConfigurationOptions<T>> _configOptions;
    private WritableJsonConfigurationOptions<T> Config => _configOptions.CurrentValue;

    public WritableJsonConfiguration(
        IOptionsMonitor<T> optionMonitorInstance,
        IOptionsMonitor<WritableJsonConfigurationOptions<T>> configOptions
    )
        : base(optionMonitorInstance)
    {
        _configOptions = configOptions;
    }

    public override void Save(T newConfig)
    {
        throw new NotImplementedException();
    }

    public override Task SaveAsync(T newConfig)
    {
        throw new NotImplementedException();
    }
}

internal class WritableJsonConfigurationOptions<T>
{
    public string ConfigFilePath { get; set; } = string.Empty;
}
