using Configuration.Writable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Example.WpfApp;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // add Configuration.Writable
        builder.AddUserConfig<SampleSetting>(opt =>
        {
            opt.FilePath = "config/sample";
        });

        // setup DI for WPF
        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<MainWindow>();

        var host = builder.Build();

        var app = host.Services.GetRequiredService<App>();
        var mainWindow = host.Services.GetRequiredService<MainWindow>();

        host.Start();
        app.Run(mainWindow);

        host.StopAsync().GetAwaiter().GetResult();
    }
}
