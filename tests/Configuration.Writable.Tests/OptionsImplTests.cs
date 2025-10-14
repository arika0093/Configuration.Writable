using System;
using System.Threading.Tasks;
using Configuration.Writable.FileWriter;
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
        var fileWriter = new InMemoryFileWriter();
        var builder = new WritableConfigurationOptionsBuilder<TestSettings>
        {
            FilePath = "test.json",
            InstanceName = Microsoft.Extensions.Options.Options.DefaultName,
        };
        builder.UseInMemoryFileWriter(fileWriter);
        var configOptions = builder.BuildOptions();

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });
        var options = new OptionsImpl<TestSettings>(optionsMonitor);

        // Act
        var value = options.Value;

        // Assert
        value.ShouldNotBeNull();
        value.Name.ShouldBe("default");
        value.Value.ShouldBe(42);
    }

    [Fact]
    public void Value_ShouldReturnSameInstanceOnMultipleCalls()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();
        var builder = new WritableConfigurationOptionsBuilder<TestSettings>
        {
            FilePath = "test.json",
            InstanceName = Microsoft.Extensions.Options.Options.DefaultName,
        };
        builder.UseInMemoryFileWriter(fileWriter);
        var configOptions = builder.BuildOptions();

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });
        var options = new OptionsImpl<TestSettings>(optionsMonitor);

        // Act
        var value1 = options.Value;
        var value2 = options.Value;

        // Assert
        value1.ShouldBeSameAs(value2);
    }

    [Fact]
    public void Value_WithCustomInstanceName_ShouldThrow()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();
        var builder = new WritableConfigurationOptionsBuilder<TestSettings>
        {
            FilePath = "test.json",
            InstanceName = "custom",
        };
        builder.UseInMemoryFileWriter(fileWriter);
        var configOptions = builder.BuildOptions();

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });
        var options = new OptionsImpl<TestSettings>(optionsMonitor);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => options.Value);
    }

    [Fact]
    public async Task Value_WithPreloadedData_ShouldReturnLoadedData()
    {
        // Arrange
        var fileWriter = new InMemoryFileWriter();
        var testSettings = new TestSettings { Name = "test", Value = 100 };

        var builder = new WritableConfigurationOptionsBuilder<TestSettings>
        {
            FilePath = "test.json",
            InstanceName = Microsoft.Extensions.Options.Options.DefaultName,
        };
        builder.UseInMemoryFileWriter(fileWriter);
        var configOptions = builder.BuildOptions();

        // Preload data using the provider
        await configOptions.Provider.SaveAsync(
            testSettings,
            new OptionOperations<TestSettings>(),
            configOptions
        );

        var optionsMonitor = new OptionsMonitorImpl<TestSettings>(new[] { configOptions });
        var options = new OptionsImpl<TestSettings>(optionsMonitor);

        // Act
        var value = options.Value;

        // Assert
        value.Name.ShouldBe("test");
        value.Value.ShouldBe(100);
    }
}
