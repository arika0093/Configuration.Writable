using System;
using System.Threading.Tasks;
using Configuration.Writable.Configure;
using Configuration.Writable.FileProvider;
using Configuration.Writable.Options;

namespace Configuration.Writable.Tests;

public class OptionsImplTests
{
    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
    }

    [Fact]
    public void Value_ShouldReturnDefaultValueFromMonitor()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var builder = new WritableOptionsConfigBuilder<TestSettings> { FilePath = "test.json" };
        builder.UseInMemoryFileProvider(FileProvider);
        var configOptions = builder.BuildOptions(Microsoft.Extensions.Options.Options.DefaultName);

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(registry);
        var options = new OptionsImpl<TestSettings>(optionsMonitor);

        // Act
        var value = options.Value;

        // Assert
        value.ShouldNotBeNull();
        value.Name.ShouldBe("default");
        value.Value.ShouldBe(42);
    }

    [Fact]
    public void Value_ShouldReturnSameValueOnMultipleCalls()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var builder = new WritableOptionsConfigBuilder<TestSettings> { FilePath = "test.json" };
        builder.UseInMemoryFileProvider(FileProvider);
        var configOptions = builder.BuildOptions(Microsoft.Extensions.Options.Options.DefaultName);

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(registry);
        var options = new OptionsImpl<TestSettings>(optionsMonitor);

        // Act
        var value1 = options.Value;
        var value2 = options.Value;

        // Assert
        value1.ShouldNotBeSameAs(value2);
        value1.ShouldBeEquivalentTo(value2);
    }

    [Fact]
    public void Value_WithCustomInstanceName_ShouldThrow()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var builder = new WritableOptionsConfigBuilder<TestSettings> { FilePath = "test.json" };
        builder.UseInMemoryFileProvider(FileProvider);
        var configOptions = builder.BuildOptions("custom");

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(registry);
        var options = new OptionsImpl<TestSettings>(optionsMonitor);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => options.Value);
    }

    [Fact]
    public async Task Value_WithPreloadedData_ShouldReturnLoadedData()
    {
        // Arrange
        var FileProvider = new InMemoryFileProvider();
        var testSettings = new TestSettings { Name = "test", Value = 100 };

        var builder = new WritableOptionsConfigBuilder<TestSettings> { FilePath = "test.json" };
        builder.UseInMemoryFileProvider(FileProvider);
        var configOptions = builder.BuildOptions(Microsoft.Extensions.Options.Options.DefaultName);

        // Preload data using the provider
        await configOptions.FormatProvider.SaveAsync(testSettings, configOptions);

        var registry = new WritableOptionsConfigRegistryImpl<TestSettings>([configOptions]);
        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(registry);
        var options = new OptionsImpl<TestSettings>(optionsMonitor);

        // Act
        var value = options.Value;

        // Assert
        value.Name.ShouldBe("test");
        value.Value.ShouldBe(100);
    }
}
