using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Options;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable.Options;

/// <summary>
/// Provides the configuration values at the time the application was started.
/// </summary>
internal class OptionsImpl<T>(OptionsMonitorImpl<T> optionsMonitor) : IOptions<T>
    where T : class, new()
{
    public T Value => optionsMonitor.GetDefaultValue(MEOptions.DefaultName);
}
