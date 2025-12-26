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
Create a dedicated library called `ICloneableGenerator` and use it as a dependency.
This library consists of the following two components:
* `ICloneableGenerator` (Abstraction)
    * `ICloneableGenerator.Generator` (Source Generator)

`ICloneableGenerator` provides the following interfaces:

```csharp
public interface IDeepCloneable<T>
{
    T DeepClone();
}

public interface IShallowCloneable<T>
{
    T ShallowClone();
}
```

`ICloneableGenerator.Generator` is a source generator that automatically generates implementations of the `DeepClone` or `ShallowClone` methods for classes that implement `IDeepCloneable<T>` or `IShallowCloneable<T>`.

```csharp
// 3rd-party library side
public interface ISomeLibraryClass<T> : IDeepCloneable<T> //, ...
{

}
public abstract class SomeLibraryBaseClass<T> : IShallowCloneable<T> //, ...
{
    public abstract T ShallowClone(); // not implemented
}

// user side

// Conditions for automatic generation:
// 1. The class must be partial.
// 2. It must implement IDeepCloneable<T> or IShallowCloneable<T> (including derived interfaces/classes).
// 3. The implementation must not already exist.

// OK
public partial class SampleSetting : IDeepCloneable<SampleSetting>
{
    public string Name { get; set; }
    // The DeepClone method implementation will be auto-generated
}

// OK
public partial class SampleSetting : ISomeLibraryClass<SampleSetting>
{
    public string Name { get; set; }
    // The DeepClone method implementation will be auto-generated
}

// OK
public partial class SampleSetting : SomeLibraryBaseClass<SampleSetting>
{
    public string Name { get; set; }
    // The ShallowClone method implementation will be auto-generated
}

// NG
public class SampleSetting : IDeepCloneable<SampleSetting> // not partial
{
    public string Name { get; set; }
    // The DeepClone method implementation will NOT be auto-generated
}

public partial class SampleSetting : IDeepCloneable<SampleSetting>
{
    public string Name { get; set; }

    public SampleSetting DeepClone() // Already implemented, so will NOT be auto-generated
    {
        return new SampleSetting { Name = this.Name };
    }
}
```

Add the above library as a dependency and specify the CloneStrategy as follows:

```csharp
if(typeof(T) is IDeepCloneable<T>)
{
    _cloneMethod = (value) => ((IDeepCloneable<T>)value).DeepClone();
}
```

Although there are many libraries that automatically generate clone methods, a dedicated library is created for the following reasons:
* To allow the auto-generated `DeepClone` method to be used within 3rd party libraries.
* To enable support without requiring extra effort from users.
