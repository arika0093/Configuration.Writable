using System;
using VYaml.Annotations;

namespace Configuration.Writable.Yaml.Tests;

[YamlObject]
public partial class TestSettings
{
    public string Name { get; set; } = "default";
    public int Value { get; set; } = 42;
    public bool IsEnabled { get; set; } = true;
    public string[] Items { get; set; } = ["item1", "item2"];
    public NestedSettings Nested { get; set; } = new();
}

[YamlObject]
public partial class NestedSettings
{
    public string Description { get; set; } = "nested_default";
    public double Price { get; set; } = 19.99;
}

[YamlObject]
public partial class TestConfiguration
{
    public string StringValue { get; set; } = "TestString";
    public int IntValue { get; set; } = 42;
    public double DoubleValue { get; set; } = 3.14159;
    public bool BoolValue { get; set; } = true;
    public string[] ArrayValue { get; set; } = ["item1", "item2", "item3"];
    public DateTime DateTimeValue { get; set; } =
        new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc);
    public NestedConfiguration Nested { get; set; } = new();
}

[YamlObject]
public partial class NestedConfiguration
{
    public string Description { get; set; } = "Nested description";
    public decimal Price { get; set; } = 99.99m;
    public bool IsActive { get; set; } = false;
}

[YamlObject]
public partial class AppSettings
{
    public string Name { get; set; } = "MyApp";
    public int Version { get; set; } = 1;
}

[YamlObject]
public partial class UserSettings
{
    public string Theme { get; set; } = "dark";
    public bool Notifications { get; set; } = true;
}