using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Configuration.Writable.Tests;

internal static class Host
{
    public static HostApplicationBuilder CreateApplicationBuilder()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

        // Host-based tests exercise writable-options integration, not platform logging sinks.
        // Removing the default providers prevents Windows EventLog from making these tests
        // platform-dependent while keeping ILogger<T> available through dependency injection.
        builder.Logging.ClearProviders();

        return builder;
    }
}
