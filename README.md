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

- 1 attribute which allows up to 255 types via a `params Type[]` ctor (`byte.MaxValue`).
	- If a request is made to allow supporting more than 255 types I can change the underlying tag tracker to a larger data type, or to have it be more dynamic and choose the lowest memory footprint possible. However, if you make a union with more than 255 types you are crazy :P
- 16 attributes, which will allow you to use up to 16 generic type arguments



When declaring a non-boxing union, all you have to do is include the namespace, then apply the attribute to a partial struct:

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

# Future Work

This NuGet package works as is. There are some optimizations to be made down the road, such as struct packing, but as
a first version I wanted to just get it working and available for everyone to use. This was my first attempt at writing
a source generator, afterall, so I didn't want to go too crazy right up front.