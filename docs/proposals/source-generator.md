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
public interface IDeepCloneable<T>
{
    T DeepClone();
}

// user side
[ConfigurationWritableModel] // add it
public partial class MySettings // mark as partial
{
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Tags { get; set; }
    public ChildClass Child { get; set; }
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
            Child = new ChildClass
            {
                // ...
            }
        };
    }
}

// Configuration.Writable.Core/WritableOptionsConfigBuilder
if(T is IDeepCloneable<T> cloneable)
{
    _cloneMethod = cloneable.DeepClone;
}
```
