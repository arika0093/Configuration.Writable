# Source generator diagnostics

The `Configuration.Writable` source generator reports the following diagnostics for
types marked with `[OptionsModel]`.

## CWWR001

**Severity:** Warning

The options model does not declare an `Id`. Set `Id` to a stable, non-empty identifier.
Keep the same value when renaming the type, namespace, or assembly and across every version of the same settings model.

```csharp
[OptionsModel(Id = "UserSetting", Version = 1)]
public partial class UserSetting;
```
## CWWR002

**Severity:** Error

The specified `Id` is empty or consists only of whitespace. Replace it with a stable,
non-empty identifier.

## CWWR003

**Severity:** Error

The explicitly specified `Version` is zero or negative. Versions must be positive integers starting at 1.
Omitting `Version` defaults it to 1 and reports CWWR010.

## CWWR004

**Severity:** Error

More than one options model uses the same `Id` and `Version`. Assign each generation of a model a unique, consecutive version.

## CWWR005

**Severity:** Error

A versioned options model has no accessible immediately preceding version with the same `Id`.
Start at version 1 and include every version in the chain.
Models in referenced assemblies must be public to participate in the chain.

## CWWR007

**Severity:** Warning

The options model implements the legacy `IHasVersion` interface.
Remove the interface and its mutable `Version` property, then specify the schema version on `OptionsModel`.

```csharp
[OptionsModel(Id = "UserSetting", Version = 1)]
public partial class UserSetting;
```

## CWWR008

**Severity:** Error

The options model is not declared `partial`.
Add the `partial` modifier so the generator can add schema metadata and migration registration.

## CWWR009

**Severity:** Error

The versioned options model cannot be generated.
It must be a non-static class with an accessible parameterless constructor.

## CWWR010

**Severity:** Warning

The options model omits `Version`. The generator treats it as schema version 1, but the
version should be explicit so future schema changes can be tracked reliably.

```csharp
[OptionsModel(Id = "UserSetting", Version = 1)]
public partial class UserSetting;
```
