using Microsoft.Extensions.Logging;
using ZLogger;

namespace Configuration.Writable.Tests;

// A simple console logger factory for tests using ZLogger
public static class ConsoleLoggerFactory
{
    public static ILogger Create(
        string name = "Configuration.Writable",
        LogLevel minLogLevel = LogLevel.Trace
    )
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddZLoggerConsole().SetMinimumLevel(minLogLevel);
        });

        return loggerFactory.CreateLogger(name);
    }
}
