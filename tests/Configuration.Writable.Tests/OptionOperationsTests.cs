using System;
using System.Linq.Expressions;
using Configuration.Writable;

namespace Configuration.Writable.Tests;

public class OptionOperationsTests
{
    public class TestSettings
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
        public NestedSettings Nested { get; set; } = new();
    }

    public class NestedSettings
    {
        public string Description { get; set; } = "nested";
        public DeepNestedSettings Deep { get; set; } = new();
    }

    public class DeepNestedSettings
    {
        public string Data { get; set; } = "deep";
    }

    [Fact]
    public void DeleteKey_SimpleProperty_ShouldAddToKeysToDelete()
    {
        var operations = new OptionOperations<TestSettings>();
        operations.DeleteKey(s => s.Name);

        operations.KeysToDelete.Count.ShouldBe(1);
        operations.KeysToDelete[0].ShouldBe("Name");
        operations.HasOperations.ShouldBeTrue();
    }

    [Fact]
    public void DeleteKey_NestedProperty_ShouldCreateColonSeparatedPath()
    {
        var operations = new OptionOperations<TestSettings>();
        operations.DeleteKey(s => s.Nested.Description);

        operations.KeysToDelete.Count.ShouldBe(1);
        operations.KeysToDelete[0].ShouldBe("Nested:Description");
        operations.HasOperations.ShouldBeTrue();
    }

    [Fact]
    public void DeleteKey_DeepNestedProperty_ShouldCreateMultiLevelPath()
    {
        var operations = new OptionOperations<TestSettings>();
        operations.DeleteKey(s => s.Nested.Deep.Data);

        operations.KeysToDelete.Count.ShouldBe(1);
        operations.KeysToDelete[0].ShouldBe("Nested:Deep:Data");
        operations.HasOperations.ShouldBeTrue();
    }

    [Fact]
    public void DeleteKey_MultipleProperties_ShouldAddAllToList()
    {
        var operations = new OptionOperations<TestSettings>();
        operations.DeleteKey(s => s.Name);
        operations.DeleteKey(s => s.Value);
        operations.DeleteKey(s => s.Nested.Description);

        operations.KeysToDelete.Count.ShouldBe(3);
        operations.KeysToDelete[0].ShouldBe("Name");
        operations.KeysToDelete[1].ShouldBe("Value");
        operations.KeysToDelete[2].ShouldBe("Nested:Description");
        operations.HasOperations.ShouldBeTrue();
    }

    [Fact]
    public void DeleteKey_SameKeyTwice_ShouldNotDuplicate()
    {
        var operations = new OptionOperations<TestSettings>();
        operations.DeleteKey(s => s.Name);
        operations.DeleteKey(s => s.Name);

        operations.KeysToDelete.Count.ShouldBe(1);
        operations.KeysToDelete[0].ShouldBe("Name");
    }

    [Fact]
    public void DeleteKey_NullExpression_ShouldThrowArgumentNullException()
    {
        var operations = new OptionOperations<TestSettings>();
        Should.Throw<ArgumentNullException>(() => operations.DeleteKey(null!));
    }

    [Fact]
    public void HasOperations_WithNoOperations_ShouldReturnFalse()
    {
        var operations = new OptionOperations<TestSettings>();
        operations.HasOperations.ShouldBeFalse();
    }

    [Fact]
    public void HasOperations_WithOperations_ShouldReturnTrue()
    {
        var operations = new OptionOperations<TestSettings>();
        operations.DeleteKey(s => s.Name);
        operations.HasOperations.ShouldBeTrue();
    }
}
