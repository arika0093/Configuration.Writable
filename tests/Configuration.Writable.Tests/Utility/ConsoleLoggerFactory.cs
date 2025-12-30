using Microsoft.Extensions.Logging;

namespace Configuration.Writable.Tests;

// A simple console logger factory for tests
public static class ConsoleLoggerFactory
{
    public static ILogger Create(
        string name = "Configuration.Writable",
        LogLevel minLogLevel = LogLevel.Trace
    )
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(minLogLevel);
        });

        return loggerFactory.CreateLogger(name);
    }
}
