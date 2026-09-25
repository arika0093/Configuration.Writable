using Configuration.Writable;

namespace Configuration.Writable.Tests;

public enum CloneTestState
{
    Disabled,
    Enabled,
}

[OptionsModel]
public partial class RequiredEnumCloneTestConfig
{
    public required CloneTestState State { get; set; }
}

public class DeepCloneGeneratorTests
{
    [Test]
    public void GeneratedDeepClone_ShouldCopyRequiredEnumProperty()
    {
        var original = new RequiredEnumCloneTestConfig { State = CloneTestState.Enabled };

        var clone = original.DeepClone();

        clone.State.ShouldBe(CloneTestState.Enabled);
    }
}
