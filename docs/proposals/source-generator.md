## Automatic Partial Implementation Generation via Source Generators

### Problem
Currently, the following changes are required to enable NativeAOT:

1. Change the CloneStrategy to one that does not use Reflection.
2. Use JsonSerializerContext for serialization/deserialization in JsonFormatProvider.
3. Generate validation logic for ValidationAttribute using a source generator.

Of these, 2 and 3 are highly implementation-dependent and complex, so they are not addressed here. However, 1 is relatively easy to support.
Additionally, since internal cloning is frequently used, having automatic implementation via a source generator is desirable for performance improvements.
It is not necessary to highly optimize for speed; the important point is that a basic implementation is generated automatically.
In the future, having a source generator will also broaden the range of features that can be added.

### Approach
* Provide an additional library: Configuration.Writable.Generators.
* This library will be bundled and provided together with `Configuration.Writable` (not used standalone).
* The following code will be generated:

```csharp
// in library
namespace Configuration.Writable;
// Interface used as a marker of source generation
public interface IDeepCloneable<T>
{
    T DeepClone();
}

public interface IOptionsModel<T> : IDeepCloneable<T>
{
    // and other in the future
}
public interface IVersionedOptionsModel<T> : IOptionsModel<T>, IHasVersion
{
    // ...
}
```

```csharp

// user side
// must be marked as partial. if not partial, generation will be skipped.
// if IDeepCloneable<T> is included in the base interfaces and there is no implementation, generation will be performed.
public partial class MySettings : IOptionsModel<MySettings>
{
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Tags { get; set; }
    public ChildClass Child { get; set; }

    // If the user implements this themselves, the SourceGenerator will skip generation
    // public MySettings DeepClone() => { ... }
}

// generated code
public partial class MySettings : IDeepCloneable<MySettings>
{
    public MySettings DeepClone()
    {
        return new MySettings
        {
            Name = this.Name,
            Age = this.Age,
            // should be deepcopy, not shallow copy
            Tags = this.Tags.ToList(),
            Child = new ChildClass {
                // ...
            }
        };
    }
}

// in WritableOptionsConfigBuilder
public void UseDefaultCloneStrategy()
{
    if(T is IDeepCloneable<T> cloneable)
    {
        _cloneMethod = (value) => cloneable.DeepClone();
    }
    else
    {
        UseJsonCloneStrategy();
    }
}
```

Considering the content of [migration-support.md](migration-support.md), it is better to provide this as an automatic interface implementation rather than using an attribute.