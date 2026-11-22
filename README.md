# NonBoxingUnions

A simple NuGet package which fills the gap that the .NET 11 release did not cover: Non-Boxing Unions.

Currently, if you just declare a union as `public union SomeUnion(int, double, float)`, the value will be boxed
into an `object`, which adds GC pressure and heap allocations that otherwise could have been avoided.

This package helps avoid all boxing conversions for value types with unions by creating `bool TryGetValue(out T value)` methods
for each type used by the union.

Under the hood, the C# compiler will utilize these methods when they exist, so instead of doing something like

```cs
if (union.Value is int i)
	return i;
```

It will instead call

```cs
if (union.TryGetValue(out int i))
	return i;
```

___

# A Quick Example

This package will generate 17 attributes:

- 1 attribute which allows up to `int.MaxValue` types via a `params Type[]` ctor.
- 16 generic attributes, which will allow you to use anywhere from 1-16 generic type arguments

When declaring a non-boxing union, all you have to do is include the namespace, then apply the attribute to a partial struct (or alternatively fully qualify the attribute type):

```cs
using NonBoxingUnions;

[NonBoxingUnion(typeof(int), typeof(double), typeof(float), typeof(MyCustomStruct)]
partial struct MyUnion;

// or

[NonBoxingUnion<int, double, float, MyCustomStruct>]
partial struct MyOtherUnion;
```

Note that if you declare a non boxing union as an enclosed type, the enclosing type must also be partial. If the enclosing
type is also an enclosed type, its enclosing type must be partial, etc. etc.

___

# Full Feature Set

* **Does not box value types:** Utilizes the compiler calling `union.TryGetValue(out T value)` to retrieve value types without boxing 
* **Incremental Generation:** Only re-generates the union when changes have been made
* **No External Dependencies:** The only dependency is that your consuming project targets .NET 11+
* **Compiler Errors for malformed attributes:** If your `[NonBoxingUnion]` attribute is malformed (either missing types or has duplicate types) a compiler error will be generated, pointing you to the declaring struct
* **Supports up to 2,147,483,647 (`int.MaxValue`) value-types:** Do you really need more?
	* The backing field used to track what value is assigned grows with your declared types. For all reasonable unions this will be a `byte`, but it can grow up to an `int` if needed.
* **Packs value types:** This union will pack all the value types used such that the value type field will only be as large as the largest value type declared.
	* This means if you have an `int`, `short`, `ushort`, `byte`, `sbyte`, and `char` the union will only be 8 bytes: 4 bytes for the int, 1 byte for a `byte` tracking tag, and 3 bytes of padding.