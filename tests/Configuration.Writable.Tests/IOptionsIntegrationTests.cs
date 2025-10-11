using System;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Configuration.Writable.Tests;

public class IOptionsIntegrationTests
{
    private readonly InMemoryFileWriter _fileWriter = new();

    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
        public bool IsEnabled { get; set; } = true;
    }

    [Fact]
    public void IOptions_ShouldProvideCurrentValue()
    {
        var testFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();
        builder.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var host = builder.Build();
        var optionsService = host.Services.GetRequiredService<IOptions<TestSettings>>();

        var settings = optionsService.Value;
        settings.ShouldNotBeNull();
        settings.Name.ShouldBe("default");
        settings.Value.ShouldBe(42);
        settings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IOptionsSnapshot_ShouldProvideCurrentValue()
    {
        var testFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();
        builder.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var host = builder.Build();

        using var scope = host.Services.CreateScope();
        var optionsService = scope.ServiceProvider.GetRequiredService<
            IOptionsSnapshot<TestSettings>
        >();

        var settings = optionsService.Value;
        settings.ShouldNotBeNull();
        settings.Name.ShouldBe("default");
        settings.Value.ShouldBe(42);
        settings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IOptionsMonitor_ShouldProvideCurrentValue()
    {
        var testFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();
        builder.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var host = builder.Build();
        var optionsService = host.Services.GetRequiredService<IOptionsMonitor<TestSettings>>();

        var settings = optionsService.CurrentValue;
        settings.ShouldNotBeNull();
        settings.Name.ShouldBe("default");
        settings.Value.ShouldBe(42);
        settings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IOptionsMonitor_Get_WithName_ShouldWork()
    {
        var testFileName = Path.GetRandomFileName();

        var builder = Host.CreateApplicationBuilder();
        builder.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.InstanceName = "custom";
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var host = builder.Build();
        var optionsService = host.Services.GetRequiredService<IOptionsMonitor<TestSettings>>();

        var settings = optionsService.Get("custom");
        settings.ShouldNotBeNull();
        settings.Name.ShouldBe("default");
        settings.Value.ShouldBe(42);
        settings.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task IOptions_WithSavedConfiguration_ShouldReturnSavedValues()
    {
        var testFileName = Path.GetRandomFileName();

        // First, save some configuration
        var builder1 = Host.CreateApplicationBuilder();
        builder1.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var host1 = builder1.Build();
        var writableOptions = host1.Services.GetRequiredService<IWritableOptions<TestSettings>>();

        await writableOptions.SaveAsync(
            new TestSettings
            {
                Name = "saved_name",
                Value = 999,
                IsEnabled = false,
            }
        );

        host1.Dispose();

        var builder2 = Host.CreateApplicationBuilder();
        builder2.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = testFileName;
            options.UseInMemoryFileWriter(_fileWriter);
        });

        var host2 = builder2.Build();
        var optionsService = host2.Services.GetRequiredService<IOptionsMonitor<TestSettings>>();

        var settings = optionsService.CurrentValue;
        settings.Name.ShouldBe("saved_name");
        settings.Value.ShouldBe(999);
        settings.IsEnabled.ShouldBeFalse();

        host2.Dispose();
    }
}
